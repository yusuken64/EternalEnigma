#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in art verification in a development player, using the isolated demo save store.</summary>
public sealed class DioramaPlayerValidation : MonoBehaviour
{
    [Serializable] public sealed class Sample
    {
        public string scene;
        public int triangles,renderers,materials,errorMaterials;
        public float medianMs,p95Ms;
    }
    [Serializable] public sealed class Report
    {
        public string platform,gpu,api,unity;
        public string protocol="Seed 12345; production town, starter dungeon and overworld; 1280x800 world-camera rendering with synchronous one-pixel GPU readback, 45 warmup + 180 samples. Includes update/readback overhead; excludes scene generation and is not normal backbuffer FPS.";
        public List<Sample> samples=new();
        public List<string> errors=new();
    }
    readonly Report report=new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartIfRequested()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if(!Application.absoluteURL.Contains("dioramaValidation=1"))return;
#else
        if(!Environment.GetCommandLineArgs().Contains("--diorama-validation"))return;
#endif
        if(FindFirstObjectByType<DioramaPlayerValidation>()==null)
        {var obj=new GameObject("Diorama player validation");DontDestroyOnLoad(obj);obj.AddComponent<DioramaPlayerValidation>();}
    }
    IEnumerator Start()
    {
        Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
        report.platform=Application.platform.ToString();report.gpu=SystemInfo.graphicsDeviceName;
        report.api=SystemInfo.graphicsDeviceType.ToString();report.unity=Application.unityVersion;
        Application.logMessageReceived+=Log;
        yield return Wait(()=>FindFirstObjectByType<MainMenu>()?.IsReady==true,"main menu");
        AutoplayRunner.WatchDemo(new AutoplayOptions{Seed=12345,DebugPlaythrough=true,Speed=1});
        var run=AutoplayRunner.Active;run.Report.ValidationOnly=true;
        yield return Wait(()=>FindFirstObjectByType<Town>()?.IsReady==true,"town");
        run.Stop();run.enabled=false;Time.timeScale=1;run.gameObject.GetComponentInChildren<AutoplayPanel>(true).gameObject.SetActive(false);
        yield return Wait(()=>!Common.Instance.ScreenTransition.BlockScreen.activeSelf,"town reveal");
        yield return Measure("Town");
        var town=FindFirstObjectByType<Town>();var common=Common.Instance;
        string dungeon=common.CampaignContext.Campaign.Locations.First(l=>l.ParentTownId==town.Configuration.Id).Id;
        if(!common.Travel.EnterTownDungeon(town,dungeon))throw new InvalidOperationException("Validation could not enter starter dungeon.");
        yield return Wait(()=>FindFirstObjectByType<Game>()?.IsReady==true&&!common.ScreenTransition.BlockScreen.activeSelf,"dungeon");
        yield return Measure("Dungeon");
        if(!common.Travel.FinishDungeon(true,Game.Instance.PlayerController))throw new InvalidOperationException("Validation could not return to town.");
        yield return Wait(()=>FindFirstObjectByType<Town>()?.IsReady==true&&!common.ScreenTransition.BlockScreen.activeSelf,"town return");
        if(!common.Travel.ExitTown(FindFirstObjectByType<Town>()))throw new InvalidOperationException("Validation could not leave town.");
        yield return Wait(()=>FindFirstObjectByType<OverworldScene>()?.IsReady==true&&!common.ScreenTransition.BlockScreen.activeSelf,"overworld");
        yield return Measure("Overworld");
        string json=JsonUtility.ToJson(report,true);
        File.WriteAllText(Path.Combine(Application.persistentDataPath,"DioramaPlayerValidation.json"),json);
        Debug.Log("DIORAMA_PLAYER_JSON "+JsonUtility.ToJson(report));
#if !UNITY_WEBGL && !UNITY_EDITOR
        Application.Quit(report.errors.Count==0&&report.samples.All(s=>s.errorMaterials==0)?0:1);
#endif
    }
    IEnumerator Wait(Func<bool> ready,string label)
    {
        double deadline=Time.realtimeSinceStartupAsDouble+180;
        while(!ready())
        {
            if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException("Diorama validation: "+label);
            // The production clear flow waits for acknowledgement before travelling.
            if(label=="town return"&&Common.InstanceOrNull?.MessageDialog?.gameObject.activeInHierarchy==true)
                Common.InstanceOrNull.MessageDialog.Ok_Clicked();
            yield return null;
        }
        yield return null;
    }
    IEnumerator Measure(string label)
    {
        var camera=Camera.main;if(camera==null)throw new InvalidOperationException("Missing gameplay camera: "+label);
        var renderers=FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&(r is MeshRenderer||r is SkinnedMeshRenderer)).ToArray();
        var materials=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().ToArray();
        var sample=new Sample{scene=label,renderers=renderers.Length,materials=materials.Length,
            errorMaterials=materials.Count(m=>m.shader==null||!m.shader.isSupported||m.shader.name=="Hidden/InternalErrorShader"),
            triangles=renderers.Sum(r=>{var mesh=r is SkinnedMeshRenderer skin?skin.sharedMesh:r.GetComponent<MeshFilter>()?.sharedMesh;return mesh==null?0:(int)Enumerable.Range(0,mesh.subMeshCount).Sum(s=>(long)mesh.GetIndexCount(s))/3;})};
        var target=new RenderTexture(1280,800,24);var readback=new Texture2D(1,1,TextureFormat.RGB24,false);
        var oldTarget=camera.targetTexture;bool oldEnabled=camera.enabled;
        camera.enabled=false;camera.targetTexture=target;
        void Render()
        {
            var old=RenderTexture.active;camera.Render();RenderTexture.active=target;
            readback.ReadPixels(new Rect(0,0,1,1),0,0);readback.Apply();RenderTexture.active=old;
        }
        try
        {
            for(int i=0;i<45;i++){yield return null;Render();}
            var frames=new float[180];for(int i=0;i<frames.Length;i++){yield return null;Render();frames[i]=Time.unscaledDeltaTime*1000;}
            Array.Sort(frames);sample.medianMs=frames[90];sample.p95Ms=frames[171];report.samples.Add(sample);
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(Application.persistentDataPath,"DioramaPlayer_"+label+".png"),image.EncodeToPNG());
            RenderTexture.active=previous;Destroy(image);
        }
        finally {camera.targetTexture=oldTarget;camera.enabled=oldEnabled;Destroy(target);Destroy(readback);}
        yield return null;yield return null;
        Debug.Log("DIORAMA_PLAYER_STAGE "+label);yield return new WaitForSecondsRealtime(1);
    }
    void Log(string message,string stack,LogType type)
    {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.errors.Add(message);}
    void OnDestroy()=>Application.logMessageReceived-=Log;
}
#endif
