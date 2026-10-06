using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum DungeonVisualRole { Wood, Heading, Paper, WoodButton, Primary, Secondary, Close, IconFrame, Selection, Track, HP, SP, Hunger, InputSurface }

// Serialized visual intent survives generic UI baking and background migration.
[RequireComponent(typeof(Image))]
public sealed class DungeonUIRole : MonoBehaviour
{
    public DungeonVisualRole Role;
    public bool IsValid()
    {
        var image=GetComponent<Image>();
        if(Role==DungeonVisualRole.InputSurface)return image.sprite==null && image.color.a==0;
        bool square=Role==DungeonVisualRole.Close || Role==DungeonVisualRole.IconFrame || Role==DungeonVisualRole.Selection;
        return image.sprite==GameUITheme.Current.DungeonSprite(Role) && image.pixelsPerUnitMultiplier==1 &&
            image.type==(square?Image.Type.Simple:Image.Type.Sliced) && (!square || image.preserveAspect);
    }
    public void Apply()
    {
        var image = GetComponent<Image>();
        if(Role==DungeonVisualRole.InputSurface){image.sprite=null;image.overrideSprite=null;image.color=Color.clear;image.type=Image.Type.Simple;return;}
        GameUITheme.Current.Surface(image, GameUITheme.Current.DungeonSprite(Role), 1);
        image.preserveAspect = Role == DungeonVisualRole.Close || Role == DungeonVisualRole.IconFrame || Role == DungeonVisualRole.Selection;
        if (image.preserveAspect) image.type = Image.Type.Simple;
        var button = GetComponentInParent<Button>(true);
        if (button != null && button.targetGraphic == image)
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
                label.color = Role == DungeonVisualRole.WoodButton ? GameUITheme.LightInk : GameUITheme.Ink;
    }
}
