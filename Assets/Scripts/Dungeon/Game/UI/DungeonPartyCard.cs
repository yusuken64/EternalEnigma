using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DungeonPartyCard : MonoBehaviour
{
    [SerializeField] private CharacterStatsDisplay display;
    [SerializeField] private Image panel;
    [SerializeField] private Image portraitHighlight;
    public Button[] StatusButtons;
    [SerializeField] private TMP_Text order;
    [SerializeField] private Transform statuses;
    private string statusKey;
#if UNITY_EDITOR
    public static DungeonPartyCard AuthorLayout(CharacterStatsDisplay display, DungeonHud hud, int slot)
    {

        foreach (Transform child in display.transform) child.gameObject.SetActive(false);
        var oldBackground = display.GetComponent<Image>();
        if (oldBackground != null) oldBackground.enabled = false;
        display.transform.SetParent(hud.PartyRoot,false);
        var rect = (RectTransform)display.transform;
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;

        rect.anchorMin = new Vector2(0,1-(slot+1)*.25f+.008f); rect.anchorMax = new Vector2(1,1-slot*.25f);
        rect.pivot = new Vector2(.5f,1); rect.offsetMin = rect.offsetMax = Vector2.zero;
        var card = display.gameObject.AddComponent<DungeonPartyCard>(); card.display=display;
        card.panel=GameUISkin.Panel(display.transform,Vector2.zero,Vector2.one);
        card.panel.color = new Color(.09f,.17f,.17f,.88f);
        card.portraitHighlight = GameUISkin.Panel(card.panel.transform,new Vector2(0,.105f),new Vector2(.36f,1));
        card.portraitHighlight.name = "Turn highlight";
        card.portraitHighlight.type = Image.Type.Sliced;
        card.portraitHighlight.fillCenter = false;
        card.portraitHighlight.pixelsPerUnitMultiplier = .5f;
        card.portraitHighlight.raycastTarget = false;
        card.portraitHighlight.enabled = false;
        display.PortraitImage = GameUISkin.Rect("Portrait",card.panel.transform,new Vector2(.005f,.12f),new Vector2(.35f,.99f)).gameObject.AddComponent<Image>();
        display.PortraitImage.preserveAspect=true; display.PortraitImage.raycastTarget=false;
        display.NameText=GameUISkin.Label(card.panel.transform,"",new Vector2(.37f,.79f),new Vector2(.99f,.99f),26);
        DungeonHud.WorldLabel(display.NameText);
        display.HpDisplay=Bar(card.panel.transform,.49f,new Color(.37f,.80f,.20f));
        display.SpDisplay=Bar(card.panel.transform,.31f,new Color(.19f,.53f,.86f));
        display.HungerDisplay=Bar(card.panel.transform,.13f,new Color(.92f,.61f,.14f));
        display.LevelDisplay=Bar(card.panel.transform,.02f,new Color(.63f,.49f,.28f),.06f);
        display.LevelDisplay.ValueText.gameObject.SetActive(false);
        card.order=GameUISkin.Label(card.panel.transform,"",new Vector2(.37f,.66f),new Vector2(.99f,.80f),18);
        DungeonHud.WorldLabel(card.order);
        card.statuses=GameUISkin.Rect("Statuses",card.panel.transform,new Vector2(.01f,.01f),new Vector2(.34f,.17f));
        card.StatusButtons=new Button[4];
        for(int i=0;i<4;i++)
        {
            var button=GameUISkin.Button(card.statuses,"Status",new Vector2(i*.25f,0),new Vector2((i+1)*.25f-.01f,1),null);
            button.GetComponentInChildren<TMP_Text>().fontSize=17;
            button.navigation=new Navigation { mode=Navigation.Mode.None }; button.gameObject.SetActive(false);
            card.StatusButtons[i]=button;
        }
        return card;
    }
    private static StatsDisplay Bar(Transform parent,float y,Color color,float height=.16f)
    {
        var background=GameUISkin.Panel(parent,new Vector2(.37f,y),new Vector2(.99f,y+height));
        GameUITheme.Current.Surface(background, GameUITheme.Current.Track);
        var fill=GameUISkin.Panel(background.transform,Vector2.zero,Vector2.one);GameUITheme.Current.Surface(fill, GameUITheme.Current.Field);fill.color=color;
        var slider=background.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;slider.fillRect=fill.rectTransform;slider.interactable=false;
        var data=background.gameObject.AddComponent<StatsDisplay>();data.StatValueSlider=slider;
        data.ValueText=GameUISkin.Label(background.transform,"",new Vector2(.04f,0),new Vector2(.96f,1),22);
        data.ValueText.color=GameUITheme.LightInk; data.ValueText.alignment=TextAlignmentOptions.Midline;
        return data;
    }
#endif
    public void Bind(CharacterStatsDisplay value) { display=value; statusKey=null; }
    private void Update()
    {
        if (display == null || display.Character is not Ally ally || Game.Instance == null) return;
        portraitHighlight.enabled = ally.IsTurnHighlighted;
        portraitHighlight.color = ally.TurnHighlightColor;
        order.text = ally == Game.Instance.PlayerController.PartyLeader ? "Leader" :
            ally.AllyStrategy.ToString().Replace("Aggresive","Aggressive").Replace("HoldPosition","Hold");
        if (ally.PendingCast != null) order.text = ally.PendingCast.Name + (ally.PendingCast.Remaining == 0 ? " ready" : $" ({ally.PendingCast.Remaining} charge)");
        if (ArrowSupply.HasBow(ally)) order.text += $"\n{ally.Equipment.EquippedShield?.ItemName ?? "Arrows"} x{ArrowSupply.Count(ally)}";
        order.enableAutoSizing = true; order.fontSizeMin = 10; order.fontSizeMax = 18;
        string key=DungeonHud.StatusSummary(ally);
        if (key==statusKey) return; statusKey=key;
        foreach(var button in StatusButtons)button.gameObject.SetActive(false);
        var effects=ally.StatusEffects.Where(s=>s!=null&&!s.IsExpired()).ToArray();
        for(int i=0;i<Mathf.Min(4,effects.Length);i++)
        {
            var effect=effects[i];string name=effect.GetEffectName();
            string badge=i==3&&effects.Length>4 ? "+"+(effects.Length-3) : new string(name.Where(char.IsUpper).Take(2).ToArray());
            if (badge.Length<2) badge=name.Substring(0,Mathf.Min(2,name.Length));
            var button=StatusButtons[i]; button.gameObject.SetActive(true);
			var label=button.GetComponentInChildren<TMP_Text>();
			var icon=button.transform.Find("Status icon")?.GetComponent<Image>();
			var sprite=CombatVisualCatalog.Instance?.ForStatus(effect)?.Icon;
			if (sprite != null && icon == null)
			{
				var image=new GameObject("Status icon",typeof(RectTransform),typeof(Image));
				image.transform.SetParent(button.transform,false);
				var rect=(RectTransform)image.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
				rect.offsetMin=rect.offsetMax=Vector2.zero;
				icon=image.GetComponent<Image>(); icon.preserveAspect=true; icon.raycastTarget=false;
				label.transform.SetAsLastSibling();
			}
			if (icon != null) { icon.enabled=sprite != null; icon.sprite=sprite; }
			label.text=sprite != null ? effect.TurnsLeft.ToString() : badge;
			var labelRect=(RectTransform)label.transform;
			labelRect.anchorMin=sprite != null ? new Vector2(.52f,0) : Vector2.zero;
			labelRect.anchorMax=Vector2.one;
			labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(()=>DungeonHud.Ensure(Game.Instance).Inspect(ally.CharacterName+"\n"+DungeonHud.StatusSummary(ally)));
        }
    }
}
