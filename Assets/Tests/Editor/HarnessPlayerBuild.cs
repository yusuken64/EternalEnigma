using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class HarnessPlayerBuild
{
    [MenuItem("Tools/Eternal Enigma/Tests/Select WebGL Build Target")]
    public static void SelectWebGLBuildTarget()
    {
        if(EditorUserBuildSettings.activeBuildTarget==BuildTarget.WebGL)return;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL,BuildTarget.WebGL);
    }
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
                    locationPathName="Builds/UnifiedPresentationWebGL",target=BuildTarget.WebGL,options=BuildOptions.Development|BuildOptions.DetailedBuildReport });
                File.WriteAllText(result,$"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}\nBytes: {report.summary.totalSize}");
                WriteArtInclusion(report,"WebGL");
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
                    options = BuildOptions.Development | BuildOptions.DetailedBuildReport
                });
                File.WriteAllText("Temp/HarnessResults/Build.txt",
                    $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}\nBytes: {report.summary.totalSize}");
                WriteArtInclusion(report,"Windows");
            }
            catch (Exception exception)
            {
                File.WriteAllText("Temp/HarnessResults/Build.txt", "Failed\n" + exception);
                Debug.LogException(exception);
            }
    }

    private static void WriteArtInclusion(BuildReport report,string platform)
    {
        const string folder="Docs/Art/Previews/Diorama/Verification";
        Directory.CreateDirectory(folder);
        var content=report.packedAssets.SelectMany(p=>p.contents).ToArray();
        var lines=new System.Collections.Generic.List<string>{"pack,packedBytes,sourceAssets"};
        foreach(string prefix in new[]{"Assets/RPG Tiny Fantasy World 01 PA/","Assets/RPGMonsterWave4Polyart/",
            "Assets/Art/Diorama/","Assets/Art/EnvironmentKit/TownPreview/","Assets/TileWorldCreator/Tiles/",
            "Assets/Art/3D Props - Adorable Items/Adorable 3D Items/","Assets/Art/RPGHero/"})
        {
            var entries=content.Where(c=>c.sourceAssetPath.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
            lines.Add(prefix+","+entries.Sum(c=>(long)c.packedSize)+","+entries.Select(c=>c.sourceAssetPath).Distinct().Count());
        }
        File.WriteAllLines(folder+"/"+platform+"ArtInclusion.csv",lines);
    }
}
