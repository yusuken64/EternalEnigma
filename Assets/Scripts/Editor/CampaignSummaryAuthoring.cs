using UnityEditor;
using UnityEngine;

public static class CampaignSummaryAuthoring
{
    [MenuItem("Tools/Eternal Enigma/UI/Author Campaign Summary")]
    public static void Author()
    {
        var dialog = CampaignSummaryDialog.AuthorLayout();
        try { PrefabUtility.SaveAsPrefabAsset(dialog.gameObject, "Assets/Resources/UI/CampaignSummary.prefab"); }
        finally { Object.DestroyImmediate(dialog.gameObject); }
        AssetDatabase.SaveAssets();
    }
}
