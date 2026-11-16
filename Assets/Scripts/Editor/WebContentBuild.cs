using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the web player with deferred content: the player holds only a bootstrap scene, the menu
/// arrives in "boot" bundles, and the rest of the game in "game" bundles that download behind the
/// menu. The project on disk is unchanged afterwards; other platforms keep their normal build.
/// </summary>
public static class WebContentBuild
{
    private const string ResourcesFolder = "Assets/Resources";
    private const string DeferredFolder = "Assets/DeferredResources";
    private const string BootstrapScene = "Assets/Scenes/WebBootstrap.unity";
    private const string BootstrapMaterial = "Assets/Scenes/WebBootstrapStandard.mat";
    private const string LinkFile = "Assets/link.xml";
    private const string StreamingFolder = "Assets/StreamingAssets";
    private const string Staging = "Temp/WebContent";
    private const string BackupFolder = "Temp/WebContentBackup";
    private const string MusicFolder = "Assets/Sounds/Music/";
    // Files rewritten for the build, by GUID, with the copy to put back afterwards.
    private static readonly List<(string Guid, string Backup)> Backups = new();
    // Scan tag only: lets build hooks that inspect named bundles still see the moved folder.
    private const string ScanTag = "deferred-resources";

    private static readonly string[] BootScenes = { "Assets/Scenes/Common.unity", "Assets/Scenes/MainMenu.unity" };
    // Resources the menu reads before the game bundles arrive.
    private static readonly string[] BootResourceFolders = { "UI/", "Presentation/", "Terminal/" };
    // Read through Resources by code this project does not own.
    private static readonly string[] PlayerResources = { "DOTweenSettings.asset" };

    /// <summary>CI entry point: -executeMethod WebContentBuild.BuildFromCommandLine -customBuildPath &lt;folder&gt;.</summary>
    public static void BuildFromCommandLine()
    {
        try
        {
            var arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-customBuildPath");
            if (index < 0 || index + 1 >= arguments.Length) throw new ArgumentException("-customBuildPath <folder> is required.");
            Build(arguments[index + 1]);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
            return;
        }
        EditorApplication.Exit(0);
    }

    [MenuItem("Tools/Eternal Enigma/Build/WebGL With Deferred Content")]
    private static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build("Builds/WebGL");
    }

    public static void Build(string outputFolder)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            throw new InvalidOperationException("Switch the active build target to WebGL before building deferred content.");
        if (AssetDatabase.IsValidFolder(DeferredFolder))
            throw new InvalidOperationException(DeferredFolder + " exists; restore it to " + ResourcesFolder + " before building.");

        var gameScenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).Except(BootScenes).ToArray();
        // Resources paths are captured by GUID so they survive the folder move.
        var resourceNames = new Dictionary<string, string>();
        var bootResources = new HashSet<string>();
        foreach (var file in Directory.GetFiles(ResourcesFolder, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            string path = file.Replace('\\', '/');
            string relative = path.Substring(ResourcesFolder.Length + 1);
            if (PlayerResources.Contains(relative)) continue;
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) continue;
            resourceNames[guid] = GameContent.ResourcePrefix + relative.ToLowerInvariant();
            if (BootResourceFolders.Any(folder => relative.StartsWith(folder, StringComparison.Ordinal))) bootResources.Add(guid);
        }

        try
        {
            MoveResourcesOut();
            var resources = resourceNames.Keys.Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var tracks = SeparateMusic(BootScenes.Concat(gameScenes).Concat(resources).ToArray());
            var atlases = AssetDatabase.FindAssets("t:SpriteAtlas", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath);
            var bootRoots = BootScenes.Concat(bootResources.Select(AssetDatabase.GUIDToAssetPath)).Concat(atlases).ToArray();
            var gameRoots = gameScenes.Concat(resources).ToArray();
            var boot = Bundled(AssetDatabase.GetDependencies(bootRoots, true)).ToArray();
            var game = Bundled(AssetDatabase.GetDependencies(gameRoots, true)).Except(boot).ToArray();
            string Name(string path) => resourceNames.TryGetValue(AssetDatabase.AssetPathToGUID(path), out var name) ? name : path.ToLowerInvariant();
            var builds = new[]
            {
                new AssetBundleBuild { assetBundleName = "boot_assets", assetNames = boot, addressableNames = boot.Select(Name).ToArray() },
                new AssetBundleBuild { assetBundleName = "boot_scenes", assetNames = BootScenes },
                new AssetBundleBuild { assetBundleName = "game_assets", assetNames = game, addressableNames = game.Select(Name).ToArray() },
                new AssetBundleBuild { assetBundleName = "game_scenes", assetNames = gameScenes },
            }.Concat(tracks.Select((path, i) => new AssetBundleBuild { assetBundleName = "music_" + i, assetNames = new[] { path } })).ToArray();
            Debug.Log($"Deferred content: {boot.Length} boot assets, {game.Length} game assets, {gameScenes.Length} game scenes, {tracks.Count} music tracks.");

            if (Directory.Exists(Staging)) Directory.Delete(Staging, true);
            Directory.CreateDirectory(Staging);
            var manifest = BuildPipeline.BuildAssetBundles(Staging, builds,
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.WebGL);
            if (manifest == null) throw new InvalidOperationException("Asset bundle build failed.");

            string content = StreamingFolder + "/" + GameContent.Folder;
            Directory.CreateDirectory(content);
            WebBootstrap.Bundle Publish(string name)
            {
                string source = Staging + "/" + name;
                File.Copy(source, content + "/" + name, true);
                long bytes = new FileInfo(source).Length;
                Debug.Log($"Deferred content: {name} is {bytes / 1048576f:0.0} MB.");
                return new WebBootstrap.Bundle { Name = name, Hash = manifest.GetAssetBundleHash(name).ToString(), Bytes = bytes };
            }
            var bootBundles = new[] { Publish("boot_assets"), Publish("boot_scenes") };
            var gameBundles = new[] { Publish("game_assets"), Publish("game_scenes") };
            var music = tracks.Select((path, i) => new WebBootstrap.Track
                { Name = Path.GetFileNameWithoutExtension(path), Bundle = Publish("music_" + i) }).ToArray();

            WriteLinkFile(bootRoots.Concat(gameRoots).ToArray());
            CreateBootstrapScene(bootBundles, gameBundles, music);
            AssetDatabase.Refresh();

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { BootstrapScene },
                locationPathName = outputFolder,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
                // Keeps engine classes that only the bundles use.
                assetBundleManifestPath = Staging + "/" + Path.GetFileName(Staging) + ".manifest",
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Player build " + report.summary.result + " with " + report.summary.totalErrors + " errors.");
            Debug.Log($"Deferred web build succeeded: {report.summary.totalSize / 1048576f:0.0} MB player at {outputFolder}.");
        }
        finally
        {
            Restore();
        }
    }

    private static readonly HashSet<Type> EditorOnlyTypes = new()
        { typeof(SceneAsset), typeof(DefaultAsset), typeof(LightingDataAsset), typeof(LightingSettings) };

    /// <summary>Assets that can be placed in a bundle explicitly: project assets that are not code, scenes or editor data.</summary>
    private static IEnumerable<string> Bundled(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("/Editor/")) continue;
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (type == null || EditorOnlyTypes.Contains(type) || typeof(MonoScript).IsAssignableFrom(type)) continue;
            if (type.FullName.StartsWith("UnityEditorInternal.", StringComparison.Ordinal) || type.FullName == "UnityEditor.Presets.Preset") continue;
            // Everything else is named explicitly, including mixers and animator controllers, so the
            // boot and game bundles share one copy instead of each embedding its own.
            yield return path;
        }
    }

    private static void MoveResourcesOut()
    {
        Check(AssetDatabase.MoveAsset(ResourcesFolder, DeferredFolder));
        AssetDatabase.CreateFolder("Assets", "Resources");
        foreach (var keep in PlayerResources)
            Check(AssetDatabase.MoveAsset(DeferredFolder + "/" + keep, ResourcesFolder + "/" + keep));
        var importer = AssetImporter.GetAtPath(DeferredFolder);
        importer.assetBundleName = ScanTag;
        importer.SaveAndReimport();
    }

    private static void Restore()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var (guid, backup) in Backups)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !File.Exists(backup)) continue;
                File.Copy(backup, path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            Backups.Clear();
            AssetDatabase.DeleteAsset(BootstrapScene);
            AssetDatabase.DeleteAsset(BootstrapMaterial);
            AssetDatabase.DeleteAsset(LinkFile);
            AssetDatabase.DeleteAsset(StreamingFolder + "/" + GameContent.Folder);
            if (AssetDatabase.IsValidFolder(StreamingFolder) && Directory.GetFileSystemEntries(StreamingFolder).Length == 0)
                AssetDatabase.DeleteAsset(StreamingFolder);
            if (!AssetDatabase.IsValidFolder(DeferredFolder)) return;
            var importer = AssetImporter.GetAtPath(DeferredFolder);
            importer.assetBundleName = null;
            importer.SaveAndReimport();
            AssetDatabase.RemoveUnusedAssetBundleNames();
            foreach (var keep in PlayerResources)
                if (File.Exists(ResourcesFolder + "/" + keep))
                    Check(AssetDatabase.MoveAsset(ResourcesFolder + "/" + keep, DeferredFolder + "/" + keep));
            AssetDatabase.DeleteAsset(ResourcesFolder);
            Check(AssetDatabase.MoveAsset(DeferredFolder, ResourcesFolder));
        }
        catch (Exception exception)
        {
            Debug.LogError("Could not restore the project after the deferred web build: " + exception);
        }
    }

    private static void Check(string moveError)
    {
        if (!string.IsNullOrEmpty(moveError)) throw new InvalidOperationException(moveError);
    }

    /// <summary>
    /// The player contains no game scenes, so the code stripper cannot see which script assemblies
    /// the bundles need. Preserve every assembly that owns a script used by bundled content.
    /// </summary>
    private static void WriteLinkFile(string[] roots)
    {
        var assemblies = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var path in AssetDatabase.GetDependencies(roots, true))
        {
            if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) assemblies.Add(Path.GetFileNameWithoutExtension(path));
            else if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                var type = AssetDatabase.LoadAssetAtPath<MonoScript>(path)?.GetClass();
                if (type != null) assemblies.Add(type.Assembly.GetName().Name);
            }
        }
        var lines = new List<string> { "<linker>" };
        lines.AddRange(assemblies.Select(name => $"  <assembly fullname=\"{name}\" preserve=\"all\" ignoreIfMissing=\"1\"/>"));
        lines.Add("</linker>");
        File.WriteAllLines(LinkFile, lines);
        Debug.Log("Deferred content preserves: " + string.Join(", ", assemblies));
    }

    /// <summary>
    /// Music ships as one bundle per track. Every reference to a track is cleared from the content
    /// about to be bundled; where the field has a "...Track" companion, the track's name is recorded
    /// there so the game can ask for it by name. Returns the tracks that were named.
    /// </summary>
    private static List<string> SeparateMusic(string[] roots)
    {
        bool IsMusic(string path) => path.StartsWith(MusicFolder, StringComparison.Ordinal);
        if (Directory.Exists(BackupFolder)) Directory.Delete(BackupFolder, true);
        Directory.CreateDirectory(BackupFolder);
        var named = new SortedSet<string>(StringComparer.Ordinal);
        var holders = AssetDatabase.GetDependencies(roots, true)
            .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) && !IsMusic(path))
            .Where(path => AssetDatabase.GetDependencies(path, false).Any(IsMusic)).ToArray();
        foreach (var path in holders)
        {
            string backup = BackupFolder + "/" + Backups.Count;
            File.Copy(path, backup, true);
            Backups.Add((AssetDatabase.AssetPathToGUID(path), backup));
            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool changed = false;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var component in root.GetComponentsInChildren<Component>(true))
                        if (component != null) changed |= ClearMusic(component, named);
                if (changed && !EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + path);
            }
            else
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset != null && ClearMusic(asset, named)) EditorUtility.SetDirty(asset);
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        var remaining = AssetDatabase.GetDependencies(roots, true).Where(IsMusic).ToArray();
        if (remaining.Length > 0) throw new InvalidOperationException("Music is still referenced by bundled content: " + string.Join(", ", remaining));
        Debug.Log($"Deferred content: separated music from {holders.Length} assets; named tracks: {string.Join(", ", named.Select(Path.GetFileNameWithoutExtension))}.");
        return named.ToList();
    }

    private static bool ClearMusic(UnityEngine.Object target, ISet<string> named)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.GetIterator();
        bool changed = false, enterChildren = true;
        while (property.Next(enterChildren))
        {
            // Strings expose their characters as children; there is nothing to find inside them.
            enterChildren = property.propertyType != SerializedPropertyType.String;
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (!(property.objectReferenceValue is AudioClip clip)) continue;
            string path = AssetDatabase.GetAssetPath(clip);
            if (!path.StartsWith(MusicFolder, StringComparison.Ordinal)) continue;
            var track = serialized.FindProperty(property.propertyPath + "Track");
            if (track != null && track.propertyType == SerializedPropertyType.String)
            {
                track.stringValue = clip.name;
                named.Add(path);
            }
            property.objectReferenceValue = null;
            changed = true;
        }
        if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
        return changed;
    }

    private static void CreateBootstrapScene(WebBootstrap.Bundle[] boot, WebBootstrap.Bundle[] game, WebBootstrap.Track[] music)
    {
        var material = new Material(Shader.Find("Standard"));
        AssetDatabase.CreateAsset(material, BootstrapMaterial);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = 0;
        var bootstrap = new GameObject("Web Bootstrap").AddComponent<WebBootstrap>();
        bootstrap.Boot = boot;
        bootstrap.Game = game;
        bootstrap.Music = music;
        bootstrap.FirstScene = Path.GetFileNameWithoutExtension(BootScenes[0]);
        bootstrap.StandardMaterial = material;
        if (!EditorSceneManager.SaveScene(scene, BootstrapScene))
            throw new InvalidOperationException("Could not save " + BootstrapScene);
    }
}
