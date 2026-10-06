using System.Collections;
using System.Linq;
using UnityEngine;
using JuicyChickenGames.Menu;
using UnityEngine.UI;

public sealed class HomeBed : MonoBehaviour
{
    public Vector3Int Tile;
    public Transform SleepAnchor;
    public RuntimeAnimatorController SleepingController;
    public bool IsSleeping { get; private set; }
    private Town town;
    private void Awake() { town = GetComponentInParent<Town>(); SleepingController = Resources.Load<RuntimeAnimatorController>("HomeSleeping"); }
    private int activationFrame = -1;
    private readonly System.Collections.Generic.List<Material> materials = new();
    private void OnDestroy() { foreach(var material in materials) if(material != null) Destroy(material); }
    public static HomeBed Create(Town town, Vector3Int tile)
    {
        var root = new GameObject("Home bed"); root.transform.SetParent(town.transform);
        root.transform.position = town.WalkableMap.CellToWorld(tile);
        var bed = root.AddComponent<HomeBed>(); bed.town = town; bed.Tile = tile;
        float size = town.WalkableMap.TileWorldCreator.twcAsset.cellSize;
        root.transform.position += new Vector3(.5f,.5f,0)*size;
        // Bed occupies one tile. Doorway, bedside and party trail remain on the carved walkable floor.
        Part("Frame", new Vector3(0,0,-.13f), new Vector3(.8f,.95f,.2f), new Color(.25f,.13f,.07f));
        Part("Mattress", new Vector3(0,0,-.28f), new Vector3(.72f,.88f,.15f), new Color(.55f,.65f,.76f));
        Part("Pillow", new Vector3(0,.28f,-.39f), new Vector3(.6f,.25f,.12f), new Color(.9f,.86f,.73f));
        bed.SleepAnchor = new GameObject("Sleeping pose anchor").transform;
        bed.SleepAnchor.SetParent(root.transform, false); bed.SleepAnchor.localPosition = new Vector3(0,0,-.42f)*size;
        bed.SleepingController = Resources.Load<RuntimeAnimatorController>("HomeSleeping");
        return bed;
        void Part(string name, Vector3 position, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name; part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position*size; part.transform.localScale = scale*size;
            Object.Destroy(part.GetComponent<Collider>());
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")); material.color = color;
            part.GetComponent<Renderer>().sharedMaterial = material; bed.materials.Add(material);
        }
    }
    public void Interact()
    {
        if (IsSleeping || town.TownPlayer.CutsceneLocked) return;
        var menu = Object.FindFirstObjectByType<TownMenu>();
        var dialog = menu.BuildingDialogs.First(b => b.Id == "home").Dialog;
        Object.FindFirstObjectByType<TownMenuManager>().Open(dialog);
    }
    public void SleepAndSave()
    {
        if (IsSleeping || town.TownPlayer.IsBusy || activationFrame == Time.frameCount) return;
        activationFrame = Time.frameCount;
        IsSleeping = true; town.TownPlayer.CutsceneLocked = true;
        StartCoroutine(Sleep());
    }
    private IEnumerator Sleep()
    {
        var player = town.TownPlayer; var hero = player.ControllingTownAlly;
        var tile = hero.TilemapPosition; var position = hero.transform.position; var facing = hero.CurrentFacing;
        var camera = player.CameraController; bool cameraEnabled = camera.enabled; camera.enabled = false;
        var animation = hero.HeroAnimator; var controller = animation.Animator.runtimeAnimatorController;
        var weapons = animation.RightHandObjects.Concat(animation.LeftHandObjects).Where(w => w != null).Distinct().ToArray();
        var visible = weapons.Select(w => w.activeSelf).ToArray();
        var transition = Common.Instance.ScreenTransition;
        bool animated = DungeonPreferences.AnimationMode != DungeonAnimationMode.NoAnimations;
        string error = null;
        var bedEntry = SleepAnchor.position - (hero.VisualParent.transform.position - hero.transform.position);
        try
        {
            if (animated)
            {
                foreach (var weapon in weapons) weapon.SetActive(false);
                hero.SetFacing(Facing.Up); animation.PlayWalkAnimation();
                var fadeOut = StartCoroutine(transition.FadeTo(1, .6f));
                yield return Move(hero.transform, position, bedEntry, .25f);
                if (SleepingController != null)
                {
                    animation.Animator.runtimeAnimatorController = SleepingController;
                    animation.Animator.Play("Sleeping_NoWeapon",0,0); animation.Animator.Update(0);
                    AlignSleepingBody(hero);
                }
                yield return fadeOut;
            }
            foreach (var ally in player.RecruitedAllies) { ally.Hp = -1; ally.Sp = -1; ally.HasHunger = false; ally.Hunger = 0; ally.HungerAccumulate = 0; }
            // Tile remains reserved at the awake position for the entire sequence.
            town.WriteSaveData();
            CampaignSaving.Commit(Common.Instance, "town-0/home", tile, facing, out error);
            if (animated)
            {
                yield return new WaitForSecondsRealtime(.4f);
                animation.Animator.runtimeAnimatorController = controller; animation.PlayWalkAnimation();
                hero.transform.position = bedEntry;
                var fade = StartCoroutine(transition.FadeTo(0, .6f));
                yield return Move(hero.transform, bedEntry, position, .6f);
                yield return fade;
            }
        }
        finally
        {
            hero.transform.position = position; hero.TilemapPosition = tile; hero.SetFacing(facing);
            animation.Animator.runtimeAnimatorController = controller; animation.PlayIdleAnimation();
            for (int i=0;i<weapons.Length;i++) weapons[i].SetActive(visible[i]);
            camera.enabled = cameraEnabled; player.CutsceneLocked = false; IsSleeping = false;
            transition.ReleaseFade();
        }
        TownMenu.ShowMessage(error ?? "Party rested. Game saved.");
    }
    private void AlignSleepingBody(TownAlly hero)
    {
        var mesh = new Mesh();
        Bounds? body = null;
        try
        {
            foreach(var renderer in hero.HeroAnimator.Animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(!renderer.enabled) continue;
                renderer.BakeMesh(mesh);
                foreach(var vertex in mesh.vertices)
                {
                    var point=renderer.transform.TransformPoint(vertex);
                    if(body.HasValue) { var bounds=body.Value; bounds.Encapsulate(point); body=bounds; }
                    else body=new Bounds(point,Vector3.zero);
                }
            }
            if(body.HasValue) hero.transform.position += SleepAnchor.position - body.Value.center;
        }
        finally { Destroy(mesh); }
    }
    private static IEnumerator Move(Transform actor, Vector3 from, Vector3 to, float duration)
    {
        for (float elapsed=0;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
        { actor.position = Vector3.Lerp(from,to,elapsed/duration); yield return null; }
        actor.position = to;
    }
}
