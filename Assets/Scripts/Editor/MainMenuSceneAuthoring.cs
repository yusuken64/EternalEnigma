#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Authors ordinary scene objects; the menu never constructs its scenery at runtime.</summary>
public static class MainMenuSceneAuthoring
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string Materials = "Assets/Art/MainMenu";
    const string Monsters = "Assets/Art/RPGMonsterBundlePolyart/";
    const string Heroes = "Assets/Art/RPGTinyHeroWavePolyart/";

    [MenuItem("Tools/Eternal Enigma/Main Menu/Build Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Build the menu in Edit Mode.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        var previous = SceneManager.GetActiveScene();
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        if (scene.isDirty) throw new InvalidOperationException("Save the menu scene before rebuilding its scenery.");
        SceneManager.SetActiveScene(scene);
        try
        {
            Directory.CreateDirectory(Materials); AssetDatabase.Refresh();
            foreach (var root in scene.GetRootGameObjects().Where(r => r.name == "Menu Stage")) Object.DestroyImmediate(root);
            foreach (var image in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Image>(true)).ToArray())
                if (image.sprite != null && AssetDatabase.GetAssetPath(image.sprite).Contains("Movie_002")) Object.DestroyImmediate(image.gameObject);
            var stage = new GameObject("Menu Stage").transform;
            RenderSettings.skybox = SaveMaterial(new Material(Shader.Find("EternalEnigma/Menu Twilight Sky")), "TwilightSky");
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.42f, .42f, .46f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.14f, .12f, .22f);
            RenderSettings.fogStartDistance = 22; RenderSettings.fogEndDistance = 105;
            var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>()).First(c => c.CompareTag("MainCamera"));
            camera.orthographic = false;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = .1f; camera.farClipPlane = 140;
            camera.transform.position = new Vector3(0, .65f, 10);
            camera.transform.rotation = Quaternion.Euler(-15, 180, 0);
            var framing = camera.GetComponent<MenuCameraFraming>() ?? camera.gameObject.AddComponent<MenuCameraFraming>();
            framing.VerticalFieldOfView = 45; framing.Apply();
            Light(stage, "Warm key", new Vector3(28, 155, 0), 1.1f, new Color(1, .91f, .79f));
            Light(stage, "Cool fill", new Vector3(15, 230, 0), .35f, new Color(.65f, .73f, 1));

            var heroPrefab = AssetDatabase.LoadAssetAtPath<TownAlly>("Assets/Prefabs/Town/Allies/Ally_MC03.prefab");
            var hero = Visual(heroPrefab.AnimatedModel, stage, "Hero - staff and shield");
            foreach (var t in hero.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("AC11_Mustache")) t.gameObject.SetActive(false);
                if (t.name == "OHS01_Stick") t.gameObject.SetActive(false);
                if (t.name == "Shield01") t.gameObject.SetActive(true);
            }
            Pose(hero, Clip(Heroes + "Animation/SwordAndShield/Idle_Normal_SwordAndShield.fbx"));
            var staff = hero.GetComponentsInChildren<Transform>(true).First(t => t.name == "OHS01_Stick");
            staff.SetParent(hero.GetComponentsInChildren<Transform>(true).First(t => t.name == "hand_r"), false);
            staff.localPosition = Vector3.zero;
            staff.gameObject.SetActive(true);
            staff.localScale *= 1.55f;
            Fit(hero, new Vector3(3.3f, 0, 0), 3.4f, -12);
            staff.rotation = Quaternion.Euler(0, 0, -30);
            Variants(hero, "Hero", null);
            Motion(hero, Clip(Heroes + "Animation/SwordAndShield/Idle_Normal_SwordAndShield.fbx"), .7f, 0, 0, 0);

            var slime = Visual(AssetDatabase.LoadAssetAtPath<GameObject>(Monsters + "CommonStuffs/Prefab/Wave01/CharacterMaskTint/SlimePAMaskTint.prefab"), stage, "Oversized red slime");
            var slimeClip = Clip(Monsters + "RPGMonsterWave01Polyart/Animations/Slime/IdleNormal_Slime_Anim.fbx");
            Pose(slime, slimeClip); Fit(slime, new Vector3(-3.25f, -.1f, .5f), 5.35f, 45);
            slime.transform.localScale = Vector3.Scale(slime.transform.localScale, new Vector3(1.05f, 1, 1));
            Variants(slime, "Slime", new Color(.64f, .018f, .008f));
            Motion(slime, slimeClip, .65f, 0, 0, .018f);
            var batClip = Clip(Monsters + "RPGMonsterWave01Polyart/Animations/Bat/IdleNormal_Bat_Anim.fbx");
            var batPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Monsters + "CommonStuffs/Prefab/Wave01/CharacterMaskTint/BatPAMaskTint.prefab");
            var positions = new[] { new Vector3(5.4f, 5.6f, -.5f), new Vector3(4.15f, 6.7f, -1), new Vector3(2.5f, 5.1f, 0) };
            for (int i = 0; i < positions.Length; i++)
            {
                var bat = Visual(batPrefab, stage, "Purple bat " + (i + 1));
                Pose(bat, batClip); Fit(bat, positions[i], i == 2 ? 1.05f : .7f, -25);
                Variants(bat, "Bat", new Color(.3f, .045f, .58f));
                Motion(bat, batClip, .85f + i * .13f, i * .47f, .08f, 0);
            }

            var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TileWorldCreator/Tiles/Version 2 Tiles/Dungeon/_prefabs/01_dungeon_edgeTile.prefab");
            for (int i = -4; i <= 4; i++)
            {
                var wall = Visual(wallPrefab, stage, "Back wall " + i);
                Fit(wall, new Vector3(i * 1.65f, -.2f, -4), 1.7f, 90);
                Variants(wall, "Wall", null);
            }
            for (int i = 0; i < 4; i++)
            {
                var wall = Visual(wallPrefab, stage, "Angled left wall " + i);
                Fit(wall, new Vector3(7, -.2f, -8 + i * 3.1f), 3.2f, 0);
                Variants(wall, "Wall", null);
            }
            var floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TileWorldCreator/Tiles/Version 2 Tiles/Dungeon/_prefabs/05_dungeon_groundTile.prefab");
            for (int x = -6; x <= 6; x++) for (int z = -4; z <= 3; z++)
            {
                var floor = Visual(floorPrefab, stage, $"Stone floor {x},{z}");
                var bounds = Bounds(floor);
                floor.transform.localScale *= 2.1f / Mathf.Max(bounds.size.x, bounds.size.z);
                bounds = Bounds(floor);
                floor.transform.position += new Vector3(x * 2.1f - bounds.center.x, -.15f - bounds.max.y, z * 2.1f - bounds.center.z);
                Variants(floor, "Floor", null);
            }
            AddScenery(stage);
            AddCast(stage);
            AddCastle(stage);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Capture();
        }
        finally
        {
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    static void AddCast(Transform stage)
    {
        var cast = new GameObject("Heroes and looming enemies").transform;
        cast.SetParent(stage, false);
        var idle = Clip(Heroes + "Animation/NoWeapon/Idle_Normal_NoWeapon.fbx");
        foreach (var entry in new[] {
            (Prefab: "Ally_MC01", Name: "Golden warrior", Position: new Vector3(1.1f, 0, -.4f), Height: 2.65f, Yaw: 12f),
            (Prefab: "Ally_MC13", Name: "Mage companion", Position: new Vector3(.2f, 0, -3.5f), Height: 3.6f, Yaw: -18f) })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<TownAlly>("Assets/Prefabs/Town/Allies/" + entry.Prefab + ".prefab");
            var hero = Visual(prefab.AnimatedModel, cast, entry.Name);
            Pose(hero, idle); Fit(hero, entry.Position, entry.Height, entry.Yaw);
            Variants(hero, "Companion", null);
            Motion(hero, idle, .65f, entry.Height, 0, 0);
        }
        Monster(cast, "Demon king above the ramparts", "Wave03/CharacterPA/DemonKingPADefault.prefab",
            "RPGMonsterWave03Polyart/Animation/DemonKing/DemonKing_IdleNormal.fbx", new Vector3(10, 0, -16), 15, -20, 0);
        Monster(cast, "Dragon over the castle", "Wave01/CharacterPA/DragonPADefault.prefab",
            "RPGMonsterWave01Polyart/Animations/Dragon/FlyForward_Dragon_Anim.fbx", new Vector3(-9, 11, -23), 9, 30, .06f);
        for (int i = 0; i < 2; i++)
            Monster(cast, "Skeleton sentinel " + (i + 1), "Wave01/CharacterPA/SkeletonPADefault.prefab",
                "RPGMonsterWave01Polyart/Animations/Skeleton/IdleNormal_Skeleton_Anim.fbx",
                new Vector3(i == 0 ? 7.8f : -7.8f, -.1f, -6), 3.4f, i == 0 ? -20 : 20, 0);
    }

    static void Monster(Transform parent, string name, string prefabPath, string animationPath, Vector3 position, float height, float yaw, float hover)
    {
        var model = Visual(AssetDatabase.LoadAssetAtPath<GameObject>(Monsters + "CommonStuffs/Prefab/" + prefabPath), parent, name);
        var clip = Clip(Monsters + animationPath);
        Pose(model, clip); Fit(model, position, height, yaw);
        Variants(model, "BackdropEnemy", null);
        Motion(model, clip, .6f, .35f, hover, 0);
    }

    static void AddCastle(Transform stage)
    {
        var castle = new GameObject("Distant castle and mountains").transform;
        castle.SetParent(stage, false);
        const string kit = "Assets/Art/KennyNL/Castle Kit/Models/";
        GameObject Part(string mesh, string name, Vector3 position, float height, bool roof = false)
        {
            var part = Visual(AssetDatabase.LoadAssetAtPath<GameObject>(kit + mesh + ".fbx"), castle, name);
            Fit(part, position, height, 0);
            foreach (var renderer in part.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                {
                    var material = source != null ? new Material(source) : new Material(Shader.Find("Standard"));
                    if (material.HasProperty("_Color")) material.SetColor("_Color", roof ? new Color(.3f, .4f, .65f) : new Color(.46f, .49f, .64f));
                    return SaveMaterial(material, (roof ? "CastleRoof_" : "CastleStone_") + (source != null ? source.name : "Default"));
                }).ToArray();
            return part;
        }
        for (int i = -4; i <= 4; i++)
            Part("wall", "Castle curtain wall " + i, new Vector3(i * 4.5f, 0, -35), 9);
        foreach (int i in new[] { -2, -1, 0, 1, 2 })
        {
            float x = i * 9, z = i == 0 ? -39 : -35;
            int levels = i == 0 ? 5 : (Mathf.Abs(i) == 1 ? 3 : 4);
            Part("towerSquareBase", "Tower " + i + " foundation", new Vector3(x, 0, z), 4);
            for (int level = 1; level < levels; level++)
                Part("towerSquareMidWindows", "Tower " + i + " level " + level, new Vector3(x, level * 4, z), 4);
            Part("towerSquareTopRoofHigh", "Tower " + i + " spire", new Vector3(x, levels * 4, z), i == 0 ? 7 : 5, true);
            Part("flagBlue", "Tower " + i + " banner", new Vector3(x, levels * 4 + (i == 0 ? 6 : 4), z), 2.5f, true);
        }
        Part("gate", "Castle gate", new Vector3(0, 0, -32), 8);
        var rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/EnvironmentKit/Prefabs/Mountain/Rock.prefab");
        var mountainMaterial = new Material(Shader.Find("Standard")) { color = new Color(.12f, .15f, .23f) };
        mountainMaterial.SetFloat("_Glossiness", 0);
        mountainMaterial = SaveMaterial(mountainMaterial, "DistantMountains");
        for (int i = -3; i <= 3; i++)
        {
            var rock = Visual(rockPrefab, castle, "Distant mountain " + i);
            Fit(rock, new Vector3(i * 17, -3, -59 - Mathf.Abs(i) * 2), 17 + Mathf.Abs(i % 2) * 7, i * 37);
            foreach (var renderer in rock.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = mountainMaterial;
        }
    }

    static void AddScenery(Transform stage)
    {
        const string dungeon = "Assets/TileWorldCreator/Tiles/Version 2 Tiles/Dungeon/_prefabs/";
        var scenery = new GameObject("Dungeon dressing").transform;
        scenery.SetParent(stage, false);
        Prop(dungeon + "dungeon_column.prefab", scenery, "Left stone column", new Vector3(7.2f, -.1f, -1), 4.3f, 0);
        Prop(dungeon + "dungeon_column.prefab", scenery, "Right stone column", new Vector3(-6.7f, -.1f, -5), 3.2f, 0);
        Prop(dungeon + "treasureChest.prefab", scenery, "Forgotten treasure chest", new Vector3(1.8f, -.1f, -1.8f), 1.05f, 75);
        PaletteProp("Crate", scenery, "Crates beside the wall", new Vector3(5.7f, -.1f, -2.1f), .95f, -15);
        PaletteProp("Crate", scenery, "Stacked crate", new Vector3(5.8f, .85f, -2.1f), .7f, 8);
        PaletteProp("Urn", scenery, "Old stoneware urn", new Vector3(3, -.1f, 2), .6f, 15);
        Prop("Assets/Art/3D Props - Adorable Items/Adorable 3D Items/Prefabs/skull.prefab", scenery,
            "Skull on the floor", new Vector3(2, -.1f, 2), .28f, 25);
        for (int i = 0; i < 5; i++)
            Prop("Assets/Art/EnvironmentKit/Prefabs/Mountain/Rock.prefab", scenery, "Fallen masonry " + (i + 1),
                new Vector3(i < 3 ? 4.8f + i * .35f : -5.3f - (i - 3) * .5f, -.12f, i < 3 ? 1.2f + i * .2f : -.6f),
                .2f + (i % 3) * .08f, i * 47);
        foreach (var position in new[] { new Vector3(6.5f, 2.3f, -1.2f), new Vector3(-6.35f, 1.65f, -4.1f) })
        {
            var torch = Prop(dungeon + "dungeon_torchlight.prefab", scenery, "Wall torch", position, .85f, 90);
            foreach (var light in torch.GetComponentsInChildren<Light>())
            { light.intensity = 1.5f; light.range = 5; light.color = new Color(1, .48f, .14f); }
            foreach (var particles in torch.GetComponentsInChildren<ParticleSystem>())
            { var main = particles.main; main.useUnscaledTime = true; }
        }
    }

    static GameObject Prop(string path, Transform parent, string name, Vector3 position, float height, float yaw)
    {
        var prop = Visual(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent, name);
        Fit(prop, position, height, yaw);
        Variants(prop, "Scenery", null);
        return prop;
    }

    static void PaletteProp(string meshName, Transform parent, string name, Vector3 position, float height, float yaw)
    {
        // These existing dungeon meshes are authored with Z up.
        var prop = new GameObject(name);
        prop.transform.SetParent(parent, false);
        var mesh = new GameObject("Model", typeof(MeshFilter), typeof(MeshRenderer));
        mesh.transform.SetParent(prop.transform, false);
        mesh.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        mesh.GetComponent<MeshFilter>().sharedMesh = Resources.Load<Mesh>("DungeonProps/" + meshName);
        mesh.GetComponent<MeshRenderer>().sharedMaterial = Resources.Load<Material>("DungeonProps/Palette");
        Fit(prop, position, height, yaw);
        Variants(prop, "Scenery", null);
    }

    static GameObject Visual(GameObject prefab, Transform parent, string name)
    {
        if (prefab == null) throw new InvalidOperationException("Missing visual asset for " + name);
        var result = Object.Instantiate(prefab, parent); result.name = name;
        foreach (var component in result.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(component);
        foreach (var component in result.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(component);
        foreach (var component in result.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(component);
        foreach (var animator in result.GetComponentsInChildren<Animator>(true))
        { animator.applyRootMotion = false; animator.runtimeAnimatorController = null; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
        return result;
    }

    static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
    static void Pose(GameObject model, AnimationClip clip)
    {
        var animator = model.GetComponentInChildren<Animator>();
        if (animator != null) { animator.Rebind(); clip.SampleAnimation(animator.gameObject, 0); }
    }
    static Bounds Bounds(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
        var bounds = new Bounds();
        bool first = true;
        foreach (var r in renderers)
        {
            if (r is SkinnedMeshRenderer skin)
            {
                var mesh = new Mesh(); skin.BakeMesh(mesh);
                foreach (var vertex in mesh.vertices)
                {
                    var point = skin.transform.TransformPoint(vertex);
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
                Object.DestroyImmediate(mesh);
            }
            else if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }
        return bounds;
    }
    static void Fit(GameObject model, Vector3 bottomCenter, float height, float yaw)
    {
        model.transform.rotation = Quaternion.Euler(0, yaw, 0);
        var bounds = Bounds(model);
        float scale = height / bounds.size.y;
        var pivot = model.transform.position;
        var bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        // Measure once, then scale about the model pivot. Baking a skinned mesh
        // again before Unity has refreshed its skin matrices gives stale bounds.
        var scaledBottom = pivot + (bottom - pivot) * scale;
        model.transform.localScale *= scale;
        model.transform.position += bottomCenter - scaledBottom;
    }
    static void Motion(GameObject model, AnimationClip clip, float speed, float phase, float hover, float wobble)
    {
        // Keep composition transforms separate from the animated model's root.
        var anchor = new GameObject(model.name + " motion").transform;
        anchor.SetParent(model.transform.parent, false); anchor.position = model.transform.position;
        model.transform.SetParent(anchor, true);
        var motion = anchor.gameObject.AddComponent<MenuSceneMotion>();
        motion.Animator = model.GetComponentInChildren<Animator>(); motion.Clip = clip;
        motion.Speed = speed; motion.Phase = phase; motion.Hover = hover; motion.Wobble = wobble;
    }
    static void Variants(GameObject model, string name, Color? tint)
    {
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
            {
                // Particle renderers can have an unused, empty trail material slot.
                if (source == null) return null;
                var variant = new Material(source);
                if (tint.HasValue)
                {
                    for (int i = 1; i <= 7; i++)
                    {
                        if (variant.HasProperty("_Color0" + i)) variant.SetColor("_Color0" + i, tint.Value);
                        if (variant.HasProperty("_Color0" + i + "Power")) variant.SetFloat("_Color0" + i + "Power", 8);
                    }
                    if (variant.HasProperty("_EmissionPower")) variant.SetColor("_EmissionPower", Color.black);
                }
                return SaveMaterial(variant, name + "_" + source.name);
            }).ToArray();
    }
    static Material SaveMaterial(Material material, string name)
    {
        string path = Materials + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) { EditorUtility.CopySerialized(material, existing); Object.DestroyImmediate(material); return existing; }
        AssetDatabase.CreateAsset(material, path); return material;
    }
    static void Light(Transform parent, string name, Vector3 angles, float intensity, Color color)
    {
        var light = new GameObject(name).AddComponent<Light>(); light.transform.SetParent(parent, false);
        light.transform.rotation = Quaternion.Euler(angles); light.type = LightType.Directional;
        light.intensity = intensity; light.color = color;
    }

    [MenuItem("Tools/Eternal Enigma/Main Menu/Capture Scene")]
    public static void Capture()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>()).First();
        Directory.CreateDirectory("Temp/MainMenuValidation");
        foreach (var particles in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ParticleSystem>()))
            particles.Simulate(2, true, true);
        foreach (var size in new[] { new Vector2Int(598,336), new Vector2Int(1280,720), new Vector2Int(1280,800) })
        {
            var target = new RenderTexture(size.x, size.y, 24);
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.aspect = (float)size.x / size.y;
                camera.GetComponent<MenuCameraFraming>().Apply(); camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                File.WriteAllBytes($"Temp/MainMenuValidation/scene-{size.x}x{size.y}.png", image.EncodeToPNG());
            }
            finally { camera.targetTexture = previousTarget; camera.ResetAspect(); RenderTexture.active = previous; Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
        }
        camera.GetComponent<MenuCameraFraming>().Apply();
        Debug.Log("Main menu scene captures saved to Temp/MainMenuValidation.");
    }
}
#endif
