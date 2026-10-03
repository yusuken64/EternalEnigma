using System;
using System.Linq;
using EternalEnigma.Core.World;
using TWC;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DungeonThemeExplorerAuthoring
{
    [MenuItem("Tools/Eternal Enigma/Art/Preview Dungeon Themes")]
    public static void Preview()=>UnityEngine.Object.FindFirstObjectByType<EnvironmentPlayground>().ShowDungeon();
}

[CustomEditor(typeof(DungeonThemeExplorer))]
public sealed class DungeonThemeExplorerInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();var explorer=(DungeonThemeExplorer)target;
        using(new EditorGUI.DisabledScope(explorer.IsBuilding))
        {
            if(GUILayout.Button("Show / rebuild dungeon")) {explorer.Playground.ShowDungeon();if(explorer.IsReady)explorer.Rebuild();}
            if(GUILayout.Button("Frame dungeon")) explorer.Frame();
        }
    }
}
