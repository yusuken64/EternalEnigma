using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class MainMenuPolish
{
    public static void Apply(MainMenu menu)
    {
        var roots=menu.gameObject.scene.GetRootGameObjects();
        var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c=>c.CompareTag("MainCamera"));
        if(camera!=null)camera.rect=new Rect(0,0,.76f,1);
        var column=GameUISkin.Canvas("Menu button column",menu.transform,-10);
        var background=GameUISkin.Rect("Backing",column.transform,new Vector2(.76f,0),Vector2.one).gameObject.AddComponent<Image>();
        background.color=new Color(.035f,.045f,.075f,1);background.raycastTarget=false;
        Object.Destroy(column.GetComponent<GraphicRaycaster>());
        var labels=roots.SelectMany(r=>r.GetComponentsInChildren<TMP_Text>(true));
        var title=labels.FirstOrDefault(t=>t.text=="Eternal Enigma");
        if(title!=null)
        {
            title.fontSize*=.85f;
            title.rectTransform.anchorMin=new Vector2(.035f,.78f);title.rectTransform.anchorMax=new Vector2(.75f,.95f);
            title.rectTransform.offsetMin=title.rectTransform.offsetMax=Vector2.zero;
        }
        var container=menu.StartButton.transform.parent as RectTransform;
        if(container!=null)
        {
            container.anchorMin=new Vector2(.77f,.28f);container.anchorMax=new Vector2(.97f,.69f);
            container.offsetMin=container.offsetMax=Vector2.zero;
            var layout=container.GetComponent<VerticalLayoutGroup>();if(layout!=null){layout.spacing=8;layout.childControlWidth=true;layout.childForceExpandWidth=true;}
        }
        var developer=roots.SelectMany(r=>r.GetComponentsInChildren<Button>(true)).Where(b=>b==menu.TestDungeonButton || b.name.Contains("Autoplay")).ToArray();
        foreach(var button in developer)button.gameObject.SetActive(false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var canvas=GameUISkin.Canvas("Developer controls",menu.transform,10);
        bool open=false;
        GameUISkin.Button(canvas.transform,"Developer",new Vector2(.84f,.025f),new Vector2(.97f,.085f),()=>
        {open=!open;foreach(var button in developer)button.gameObject.SetActive(open);});
#endif
    }
}
