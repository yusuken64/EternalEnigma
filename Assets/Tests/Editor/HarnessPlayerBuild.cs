using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class HarnessPlayerBuild
{
    [MenuItem("Tools/Eternal Enigma/Tests/Restore Windows Build Target")]
    public static void RestoreWindowsBuildTarget()
    {
        if(EditorUserBuildSettings.activeBuildTarget==BuildTarget.StandaloneWindows64)return;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64);
    }
    [MenuItem("Tools/Eternal Enigma/Tests/Build Presentation WebGL")]
    public static void BuildPresentationWebGL()
    {
        const string result="Temp/UnifiedPresentation/WebGLBuild.txt";
        Directory.CreateDirectory("Temp/UnifiedPresentation");
        if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL,BuildTarget.WebGL))
        {File.WriteAllText(result,"Unavailable: Unity WebGL build support is not installed.");return;}
        File.WriteAllText(result,"Running");
        // Run inside the menu invocation; an unfocused Editor need not service delayCall.
            try
            {
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                    locationPathName="Builds/UnifiedPresentationWebGL",target=BuildTarget.WebGL,options=BuildOptions.Development });
                File.WriteAllText(result,$"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}");
            }
            catch(Exception e){File.WriteAllText(result,"Failed\n"+e);Debug.LogException(e);}
    }
    [MenuItem("Tools/Eternal Enigma/Tests/Build Windows Player")]
    public static void BuildWindowsPlayer()
    {
        Directory.CreateDirectory("Temp/HarnessResults");
        File.WriteAllText("Temp/HarnessResults/Build.txt", "Running");
        // Keep completion observable even when the MCP request itself times out.
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
    }
}
