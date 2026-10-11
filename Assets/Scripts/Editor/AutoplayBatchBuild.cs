using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class AutoplayBatchBuild
{
    [MenuItem("Tools/Eternal Enigma/Playthrough/Build Debug Windows")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before building.");
        const string resultPath = "Temp/DebugAutoplay/Build.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
        File.WriteAllText(resultPath, "Running");
        try
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                target = BuildTarget.StandaloneWindows64,
                locationPathName = "Builds/DebugAutoplay/DebugAutoplay.exe", options = BuildOptions.Development });
            File.WriteAllText(resultPath, $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}\nBytes: {report.summary.totalSize}");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Debug autoplay build failed.");
        }
        catch (Exception error) { File.WriteAllText(resultPath, "Failed\n" + error); throw; }
    }
}
