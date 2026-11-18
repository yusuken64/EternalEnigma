using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// CI entry point for a development Windows player: full stack traces, the command-line debug
/// autoplay (AutoplayBatchPlayer) and live-memory [Memory] logs. Never published.
/// -executeMethod DevelopmentBuild.BuildWindowsFromCommandLine -customBuildPath &lt;path&gt;
/// </summary>
public static class DevelopmentBuild
{
    public static void BuildWindowsFromCommandLine()
    {
        try
        {
            var arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-customBuildPath");
            if (index < 0 || index + 1 >= arguments.Length) throw new ArgumentException("-customBuildPath <path> is required.");
            string path = arguments[index + 1];
            if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) path = Path.Combine(path, "Eternal Enigma.exe");
            var scenes = Array.ConvertAll(Array.FindAll(EditorBuildSettings.scenes, s => s.enabled), s => s.path);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Build " + report.summary.result);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
