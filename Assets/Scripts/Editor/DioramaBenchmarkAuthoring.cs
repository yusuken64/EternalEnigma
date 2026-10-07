using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DioramaBenchmarkAuthoring
{
    const string ScenePath = "Assets/Scenes/DioramaBenchmark.unity";
    const string Evidence = "Docs/Art/Previews/Diorama/Fit";
    [MenuItem("Tools/Eternal Enigma/Diorama/Build Fit Windows")]
    public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Builds/DioramaFitWindows/DioramaFit.exe", "Windows");
    [MenuItem("Tools/Eternal Enigma/Diorama/Build Fit WebGL")]
    public static void WebGL() => Build(BuildTarget.WebGL, "Builds/DioramaFitWebGL", "WebGL");

    public static void Build(BuildTarget target, string path, string label)
    {
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Evidence + "/Build_" + label + ".txt", "Running");
        try
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new InvalidOperationException(target + " module is unavailable.");
            Author();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath },
                target = target, locationPathName = path, options = BuildOptions.Development });
            File.WriteAllText(Evidence + "/Build_" + label + ".txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}\nBytes: {report.summary.totalSize}");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Diorama " + label + " build failed.");
        }
        catch (Exception e) { File.WriteAllText(Evidence + "/Build_" + label + ".txt", "Failed\n" + e); throw; }
    }

    static void Author()
    {
        var original = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root = new GameObject("Diorama fit benchmark"); var benchmark = root.AddComponent<DioramaBenchmark>();
            benchmark.Candidates = DioramaFitAuthoring.Prefabs(DioramaFitAuthoring.Destination + "/Wave4")
                .Concat(new[] { DioramaFitAuthoring.Destination + "/Prefabs/Tree01.prefab", DioramaFitAuthoring.Destination + "/Prefabs/Tree03.prefab" })
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();
            var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 11; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            camera.backgroundColor = new Color(.40f,.53f,.28f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(0,-14,-20); camera.transform.LookAt(Vector3.zero, Vector3.up); benchmark.ViewCamera = camera;
            var light = new GameObject("Daylight", typeof(Light)).GetComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 1.15f; light.transform.rotation = Quaternion.Euler(28,-25,0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.4f,.43f,.46f);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene, true); }
    }
}
