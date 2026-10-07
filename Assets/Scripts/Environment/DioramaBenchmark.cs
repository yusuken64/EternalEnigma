using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Isolated, reproducible rendering workload used by the art fit scene.</summary>
public sealed class DioramaBenchmark : MonoBehaviour
{
    public GameObject[] Candidates;
    public int Copies = 12;
    public int WarmupFrames = 45;
    public int SampleFrames = 180;
    public Camera ViewCamera;
    [Serializable] public sealed class Sample
    {
        public string name;
        public int copies, triangles;
        public float meanMs, medianMs, p95Ms;
    }
    [Serializable] public sealed class Results
    {
        public string unity, platform, gpu, api;
        public string protocol = "1280x800 offscreen render with synchronous GPU readback each frame; includes animation/update and readback overhead";
        public int width, height;
        public List<Sample> samples = new();
    }
    string status = "Preparing diorama workload";
    IEnumerator Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Application.runInBackground = true;
        // A hidden Windows player can skip its normal backbuffer rendering. An explicit
        // target and readback force the same real GPU workload in Windows and WebGL.
        var target = new RenderTexture(1280,800,24);
        var readback = new Texture2D(1,1,TextureFormat.RGB24,false);
        ViewCamera.enabled = false; ViewCamera.targetTexture = target;
        void RenderFrame()
        {
            var previous = RenderTexture.active;
            ViewCamera.Render(); RenderTexture.active = target;
            readback.ReadPixels(new Rect(0,0,1,1),0,0); readback.Apply();
            RenderTexture.active = previous;
        }
        var results = new Results { unity = Application.unityVersion, platform = Application.platform.ToString(),
            gpu = SystemInfo.graphicsDeviceName, api = SystemInfo.graphicsDeviceType.ToString(), width = Screen.width, height = Screen.height };
        for (int candidate = -1; candidate < Candidates.Length; candidate++)
        {
            var root = new GameObject("Measured workload");
            string name = candidate < 0 ? "Empty baseline" : Candidates[candidate].name;
            int triangles = 0;
            if (candidate >= 0)
            {
                for (int i = 0; i < Copies; i++)
                {
                    var instance = Instantiate(Candidates[candidate], root.transform);
                    foreach (var lod in instance.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(0);
                    foreach (var r in instance.GetComponentsInChildren<Renderer>())
                    {
                        var mesh = r is SkinnedMeshRenderer skin ? skin.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                        if (mesh != null && !r.name.Contains("LOD1") && !r.name.Contains("LOD2"))
                            for (int part = 0; part < mesh.subMeshCount; part++) triangles += (int)mesh.GetIndexCount(part) / 3;
                    }
                    instance.transform.localPosition = new Vector3((i % 4 - 1.5f) * 3.5f, (i / 4 - 1) * 3.5f, 0);
                }
            }
            status = name + " — warmup";
            for (int i = 0; i < WarmupFrames; i++) { yield return null; RenderFrame(); }
            var elapsed = new float[SampleFrames];
            status = name + " — sampling";
            for (int i = 0; i < SampleFrames; i++) { yield return null; RenderFrame(); elapsed[i] = Time.unscaledDeltaTime * 1000; }
            Array.Sort(elapsed);
            results.samples.Add(new Sample { name = name, copies = candidate < 0 ? 0 : Copies, triangles = triangles,
                meanMs = elapsed.Average(), medianMs = elapsed[elapsed.Length / 2], p95Ms = elapsed[Mathf.Min(elapsed.Length - 1, Mathf.FloorToInt(elapsed.Length * .95f))] });
            Destroy(root); yield return null;
        }
        string json = JsonUtility.ToJson(results, true);
        Debug.Log("DIORAMA_BENCHMARK_JSON " + JsonUtility.ToJson(results));
        File.WriteAllText(Path.Combine(Application.persistentDataPath, "DioramaBenchmark.json"), json);
        ViewCamera.targetTexture = null; Destroy(target); Destroy(readback);
        status = "Completed — " + results.samples.Count + " workloads";
#if !UNITY_WEBGL && !UNITY_EDITOR
        Application.Quit();
#endif
    }
    void OnGUI() { GUI.Label(new Rect(12, 12, 800, 30), status); }
}
