using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WindowsFrameProfileBuild
{
    [MenuItem("Tools/Eternal Enigma/Performance/Build Windows Player")]
    public static void Build()
    {
        const string directory = "Builds/WindowsPerformance";
        const string status = "Temp/PCFrameAudit/build.txt";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(status));
            File.WriteAllText(status, "Building");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                target = BuildTarget.StandaloneWindows64,
                locationPathName = directory + "/Eternal Enigma.exe",
                options = BuildOptions.None,
                extraScriptingDefines = new[] { "PC_FRAME_PROFILE" }
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows player build " + report.summary.result + ", errors " + report.summary.totalErrors);
            File.WriteAllText(status, $"Succeeded\nWarnings {report.summary.totalWarnings}\nBuild GUID {report.summary.guid}\nDuration {report.summary.totalTime}");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            File.WriteAllText(status, "Failed\n" + error);
            throw;
        }
    }
}
