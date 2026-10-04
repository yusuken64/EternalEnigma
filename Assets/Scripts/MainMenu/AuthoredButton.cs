using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Data binding for a row whose geometry and components are authored in its prefab.</summary>
public sealed class AuthoredButton : MonoBehaviour
{
    public Button Button;
    public TMP_Text Label;
    public Image Icon;
    public AuthoredButton Spawn(Transform parent, string caption, Action clicked, Sprite icon = null)
    {
        var row=Instantiate(this,parent);
        row.gameObject.SetActive(true);
        row.Label.text=caption;
        row.Button.onClick.RemoveAllListeners();
        row.Button.onClick.AddListener(()=>clicked?.Invoke());
        if(row.Icon!=null){row.Icon.sprite=icon;row.Icon.gameObject.SetActive(icon!=null);}
        return row;
    }
}
