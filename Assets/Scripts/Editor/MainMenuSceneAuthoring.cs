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
