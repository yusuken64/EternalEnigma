using System;
using NUnit.Framework;
using UnityEngine;

public sealed class CampaignSlotStoreTests
{
    private sealed class Store : ISaveStore
    {
        public string[] Slots = new string[3];
        public bool Fail;
        public string Read(int slot) => Slots[slot];
        public void Write(int slot,string json) { if(Fail) throw new InvalidOperationException("disk full"); Slots[slot]=json; }
        public void Clear(int slot) => Slots[slot]=null;
    }
    [Test] public void SlotsAreIndependentAndStoreScopeRestoresSelection()
    {
        var store=new Store();
        using(SaveSystem.UseStore(store))
        {
            for(int i=0;i<3;i++) { var data=new GameSaveData();data.TownSaveData.Gold=100+i;SaveSystem.SaveData(i,data); }
            SaveSystem.ActiveSlot=2;
            using(SaveSystem.UseStore(new Store())) { Assert.That(SaveSystem.ActiveSlot,Is.Zero);SaveSystem.ActiveSlot=1; }
            Assert.That(SaveSystem.ActiveSlot,Is.EqualTo(2));
            for(int i=0;i<3;i++) Assert.That(SaveSystem.LoadData(i).TownSaveData.Gold,Is.EqualTo(100+i));
            SaveSystem.ClearData();Assert.That(SaveSystem.LoadData(2),Is.Null);Assert.That(SaveSystem.LoadData(0),Is.Not.Null);
        }
    }
    [Test] public void FailedWritesAndUnreadableBrowsingPreserveExistingBytes()
    {
        var store=new Store();
        using(SaveSystem.UseStore(store))
        {
            SaveSystem.SaveData(new GameSaveData());string previous=store.Slots[0];store.Fail=true;
            Assert.Throws<InvalidOperationException>(()=>SaveSystem.SaveData(new GameSaveData()));Assert.That(store.Slots[0],Is.EqualTo(previous));
            store.Slots[1]="not json";Assert.That(SaveSystem.Inspect(1,out var error),Is.Null);Assert.That(error,Is.Not.Null);Assert.That(store.Slots[1],Is.EqualTo("not json"));
        }
    }
    [Test] public void SnapshotRoundTripPreservesExactBenchedProgressionAndAwakeArrival()
    {
        using(SaveSystem.UseStore(new Store()))
        {
            var data=new GameSaveData { SavePointId="town-0/home",HasArrival=true,ArrivalX=7,ArrivalY=11,ArrivalFacing=Facing.Left };
            data.Roster.Add(new TownAllyData { AllyId="benched",Level=9,Experience=583,Hp=17,Sp=3 });
            SaveSystem.SaveData(data);data.Roster[0].Experience=999;
            var saved=SaveSystem.LoadData();Assert.That(saved.Roster[0].Experience,Is.EqualTo(583));Assert.That(saved.Roster[0].Level,Is.EqualTo(9));
            Assert.That(saved.ArrivalFacing,Is.EqualTo(Facing.Left));Assert.That(saved.ArrivalY,Is.EqualTo(11));
        }
    }
    [Test] public void BakedCatalogHasAllEightBackgroundsAndNeutral()
    {
        var catalog=Resources.Load<BiomeBackgroundCatalog>("CampaignBackgrounds");Assert.That(catalog,Is.Not.Null);catalog.Validate();
        foreach(var entry in catalog.Backgrounds) { Assert.That(entry.Texture.width,Is.EqualTo(1920));Assert.That(entry.Texture.height,Is.EqualTo(1080));Assert.That(catalog.For(new CampaignSaveSummary{Biome=entry.Biome}),Is.SameAs(entry.Texture)); }
        Assert.That(catalog.For(null),Is.SameAs(catalog.Neutral));
    }
    [Test] public void SelectedAndAllBakesPreserveSceneCameraAndRenderState()
    {
        var previousScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Editor/BiomeBackgrounds.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
        var author=scene.GetRootGameObjects()[0].GetComponent<BiomeBackgroundAuthoring>();
        var render=new RenderTexture(32,32,24);render.Create();var previousRender=RenderTexture.active;
        try
        {
            author.Preview(3);author.Sets[1].Root.SetActive(true);
            var before=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(author.Sets,s=>s.Root.activeSelf));
            var camera=author.Sets[3].Camera;camera.targetTexture=render;camera.aspect=1.25f;RenderTexture.active=render;
            var position=camera.transform.position;
            author.Bake(false);author.Bake(true);
            Assert.That(System.Linq.Enumerable.Select(author.Sets,s=>s.Root.activeSelf),Is.EqualTo(before));
            Assert.That(camera.targetTexture,Is.SameAs(render));Assert.That(camera.aspect,Is.EqualTo(1.25f));Assert.That(camera.transform.position,Is.EqualTo(position));
            Assert.That(RenderTexture.active,Is.SameAs(render));Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Is.EqualTo(previousScene));
            Assert.That(System.Linq.Enumerable.Any(UnityEditor.EditorBuildSettings.scenes,s=>s.path==scene.path),Is.False);
            camera.targetTexture=null;
        }
        finally
        {
            RenderTexture.active=previousRender;UnityEngine.Object.DestroyImmediate(render);
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
        }
    }

}
