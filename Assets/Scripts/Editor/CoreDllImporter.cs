using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class CoreDllImporter
{
    private const string ToolsMenu = "Tools/Eternal Enigma/Core/Build and Import DLL";
    private const string AssetsMenu = "Assets/Eternal Enigma/Build and Import Core DLL";
    private const string ProjectPath = "EternalEnigma.Core/EternalEnigma.Core/EternalEnigma.Core.csproj";
    private const string DllName = "EternalEnigma.Core.dll";
    private const string Destination = "Assets/Plugins/EternalEnigma.Core/" + DllName;
    private static bool running;

    [MenuItem(ToolsMenu)]
    [MenuItem(AssetsMenu, false, 2000)]
    public static async void BuildAndImport()
    {
        if (!CanBuild()) return;
        running = true;
        EditorApplication.LockReloadAssemblies();
        try
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var coreDirectory = Path.Combine(root, "Core");
            if (!File.Exists(Path.Combine(coreDirectory, ProjectPath)))
                throw new FileNotFoundException("Core project is missing: " + ProjectPath);

            Debug.Log("Building EternalEnigma.Core (Release, netstandard2.1)...");
            // Work outside Assets so a failed or partial build cannot replace the imported DLL.
            var output = Path.Combine(root, "Temp", "CoreDllImport");
            Directory.CreateDirectory(output);
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = FindDotnet(),
                    Arguments = $"build \"{ProjectPath}\" --configuration Release --framework netstandard2.1 --output \"{output}\" --nologo",
                    WorkingDirectory = coreDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                if (!process.Start()) throw new InvalidOperationException("Could not start dotnet.");
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!await Task.Run(() => process.WaitForExit(60000)))
                {
                    process.Kill();
                    throw new TimeoutException("Core build exceeded 60 seconds. The imported DLL was not changed.");
                }
                var log = await stdout + Environment.NewLine + await stderr;
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Core build failed; the imported DLL was not changed.\n" + log);
                Debug.Log(log.Trim());
            }

            var source = Path.Combine(output, DllName);
            if (!File.Exists(source)) throw new FileNotFoundException("Build produced no core DLL.", source);
            var destination = Path.Combine(root, Destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            // Avoid recompilation on repeated imports of an identical deterministic build.
            if (!File.Exists(destination) || !File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(destination)))
                File.Copy(source, destination, true);

            AssetDatabase.ImportAsset(Destination, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(Destination) as PluginImporter;
            if (importer == null) throw new InvalidOperationException("Unity did not recognize the core DLL as a plugin.");
            if (!importer.GetCompatibleWithAnyPlatform() || !importer.GetCompatibleWithEditor())
            {
                importer.SetCompatibleWithAnyPlatform(true);
                importer.SetCompatibleWithEditor(true);
                importer.SaveAndReimport();
            }
            Debug.Log("Core DLL imported: " + Destination);
        }
        catch (Exception exception)
        {
            Debug.LogError("Core DLL import failed. Install the .NET 10 SDK and check the build output.\n" + exception);
        }
        finally
        {
            running = false;
            EditorApplication.UnlockReloadAssemblies();
        }
    }

    [MenuItem(ToolsMenu, true)]
    [MenuItem(AssetsMenu, true)]
    private static bool CanBuild() => !running && !EditorApplication.isCompiling &&
        !EditorApplication.isPlayingOrWillChangePlaymode;

    private static string FindDotnet()
    {
        // Unity Hub can inherit a PATH from before the SDK was installed.
        var executable = Application.platform == RuntimePlatform.WindowsEditor ? "dotnet.exe" : "dotnet";
        var sdkRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(sdkRoot) && File.Exists(Path.Combine(sdkRoot, executable)))
            return Path.Combine(sdkRoot, executable);
        var defaultPath = Application.platform == RuntimePlatform.WindowsEditor
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", executable)
            : "/usr/local/share/dotnet/dotnet";
        return File.Exists(defaultPath) ? defaultPath : executable;
    }
}
