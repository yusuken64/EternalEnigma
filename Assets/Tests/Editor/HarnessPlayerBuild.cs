using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class HarnessPlayerBuild
{
    [MenuItem("Tools/Eternal Enigma/Tests/Build Windows Player")]
    public static void BuildWindowsPlayer()
    {
        Directory.CreateDirectory("Temp/HarnessResults");
        File.WriteAllText("Temp/HarnessResults/Build.txt", "Running");
        EditorApplication.delayCall += () =>
        {
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = "Builds/AuditVerification/EternalEnigma.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                File.WriteAllText("Temp/HarnessResults/Build.txt",
                    $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}");
            }
            catch (Exception exception)
            {
                File.WriteAllText("Temp/HarnessResults/Build.txt", "Failed\n" + exception);
                Debug.LogException(exception);
            }
        };
    }
}
