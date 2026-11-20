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
    [SerializeField] private RectTransform portraitRoot;
    [SerializeField] private Image nameBacking, orderBacking;
    private string statusKey;
    private void Awake() => ApplyStatusPresentation();

    public void ApplyStatusPresentation()
    {
        if (StatusButtons == null) return;
        foreach (var button in StatusButtons)
        {
            var image = (Image)button.targetGraphic;
            var role = image.GetComponent<DungeonUIRole>() ?? image.gameObject.AddComponent<DungeonUIRole>();
            // Compact icon counters need a flat square face, without the thick
            // bevel and shadow used by the full-size command buttons.
            role.Role = DungeonVisualRole.IconFrame;
            role.Apply();
            var label = button.GetComponentInChildren<TMP_Text>(true);
            label.margin = Vector4.zero;
            label.alignment = TextAlignmentOptions.Midline;
            label.color = GameUITheme.LightInk;
        }
    }
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
        display.ManaDisplay=Bar(card.panel.transform,.31f,new Color(.19f,.53f,.86f));
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
        card.AuthorCompactLayout();
        return card;
    }
    // Keep bindings, font sizes and semantic roles from the authored card.
    public void AuthorCompactLayout()
    {
        var maskSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/PartyPortraitMask.png");
        var frameSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/PartyPortraitFrame.png");
        if (maskSprite == null || frameSprite == null)
            throw new System.InvalidOperationException("Apply Compact Party HUD to author the portrait shapes first.");
        panel.enabled = false;
        var oldBackground = GetComponent<Image>();
        if (oldBackground != null) oldBackground.enabled = false;
        foreach (var image in GetComponentsInChildren<Image>(true).Where(i => i.name == "Dungeon parchment"))
            image.gameObject.SetActive(false);

        if (portraitRoot == null)
        {
            portraitRoot = GameUISkin.Rect("Portrait control", panel.transform, Vector2.zero, Vector2.one);
            var mask = GameUISkin.Rect("Portrait mask", portraitRoot, Vector2.zero, Vector2.one);
            var surface = mask.gameObject.AddComponent<Image>();
            surface.sprite = maskSprite; surface.type = Image.Type.Sliced; surface.preserveAspect = true;
            surface.color = new Color(.08f, .14f, .14f, 1); surface.raycastTarget = false;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            display.PortraitImage.transform.SetParent(mask, false);
            var frame = GameUISkin.Rect("Portrait gold frame", portraitRoot, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            frame.sprite = frameSprite; frame.type = Image.Type.Sliced; frame.preserveAspect = true;
            frame.color = new Color(.8f, .63f, .29f); frame.raycastTarget = false;
        }
        // Sorting belongs above the mask, never on the clipped image itself.
        foreach (var component in new Component[] { display.PortraitImage.GetComponent<DungeonPortraitControl>(),
            display.PortraitImage.GetComponent<GraphicRaycaster>(), display.PortraitImage.GetComponent<Canvas>() })
            if (component != null) UnityEngine.Object.DestroyImmediate(component);
        bool interactive = GetComponentInParent<DungeonHud>(true) != null;
        if (interactive)
        {
            if (portraitRoot.GetComponent<Canvas>() == null) portraitRoot.gameObject.AddComponent<Canvas>();
            if (portraitRoot.GetComponent<GraphicRaycaster>() == null) portraitRoot.gameObject.AddComponent<GraphicRaycaster>();
            if (portraitRoot.GetComponent<DungeonPortraitControl>() == null) portraitRoot.gameObject.AddComponent<DungeonPortraitControl>();
        }
        else
        {
            // Travel portraits retain their original noninteractive presentation.
            foreach (var component in new Component[] { portraitRoot.GetComponent<DungeonPortraitControl>(),
                portraitRoot.GetComponent<GraphicRaycaster>(), portraitRoot.GetComponent<Canvas>() })
                if (component != null) UnityEngine.Object.DestroyImmediate(component);
        }
        display.PortraitImage.preserveAspect = true; display.PortraitImage.raycastTarget = interactive;
        display.PortraitImage.maskable = true;
        portraitHighlight.transform.SetParent(portraitRoot, false);
        portraitHighlight.sprite = frameSprite; portraitHighlight.overrideSprite = null;
        portraitHighlight.type = Image.Type.Sliced; portraitHighlight.fillCenter = false;
        portraitHighlight.pixelsPerUnitMultiplier = 1; portraitHighlight.raycastTarget = false;
        portraitHighlight.rectTransform.anchorMin = Vector2.zero; portraitHighlight.rectTransform.anchorMax = Vector2.one;
        portraitHighlight.rectTransform.offsetMin = new Vector2(-3, -3);
        portraitHighlight.rectTransform.offsetMax = new Vector2(3, 3);

        nameBacking = TextBacking(nameBacking, "Name backing");
        orderBacking = TextBacking(orderBacking, "Action backing");
        foreach (var label in new[] { display.NameText, order })
        {
            label.margin = Vector4.zero; label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Overflow;
            DungeonHud.WorldLabel(label);
            var contrast = label.GetComponent<DungeonTextStyle>();
            if (contrast != null) contrast.OnWood = true;
        }
        GameTypography.Apply(transform);
        ApplyStatusPresentation();
        Place((RectTransform)transform, 0, 0, 280, LayoutCompact(280));
        if (transform.parent is RectTransform column && (column.GetComponentInParent<DungeonHud>() != null ||
            column.GetComponentInParent<TravelPartyHud>() != null))
        {
            // Leave the bottom controls clear, while allowing content to use
            // the empty space below the former quarter-height card slots.
            column.anchorMin = new Vector2(column.anchorMin.x, .14f);
            if (column.GetComponent<CompactPartyHudLayout>() == null) column.gameObject.AddComponent<CompactPartyHudLayout>();
            UnityEditor.EditorUtility.SetDirty(column);
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(column))
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(column);
        }
        foreach (var component in GetComponentsInChildren<Component>(true))
        {
            UnityEditor.EditorUtility.SetDirty(component);
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(component))
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }
    private Image TextBacking(Image image, string title)
    {
        if (image == null) image = GameUISkin.Rect(title, panel.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
        image.transform.SetAsFirstSibling();
        image.sprite = GameUITheme.Current.DungeonTrack; image.type = Image.Type.Sliced;
        image.color = new Color(.23f, .28f, .24f, .94f); image.raycastTarget = false;
        return image;
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
    internal int LayoutKey
    {
        get
        {
            int TextKey(TMP_Text label) => System.HashCode.Combine(label.text, label.font, label.enableAutoSizing ? label.fontSizeMax : label.fontSize);
            int key = System.HashCode.Combine(TextKey(display.NameText), TextKey(order), TextKey(display.HpDisplay.ValueText),
                TextKey(display.ManaDisplay.ValueText), TextKey(display.HungerDisplay.ValueText), display.PortraitImage.sprite);
            foreach (var button in StatusButtons) key = unchecked(key * 31 + (button.gameObject.activeSelf ? 1 : 0));
            return key;
        }
    }
    internal float LayoutCompact(float width)
    {
        if (portraitRoot == null) return ((RectTransform)transform).rect.height;
        const float textLeft = 84;
        float textWidth = width - textLeft - 4;
        Place(portraitRoot, 4, 4, 72, 72);
        var portrait = display.PortraitImage.rectTransform;
        var sprite = display.PortraitImage.sprite;
        float aspect = sprite != null ? sprite.rect.width / sprite.rect.height : .8f;
        var portraitSize = aspect < 1 ? new Vector2(72, 72 / aspect) : new Vector2(72 * aspect, 72);
        portrait.anchorMin = portrait.anchorMax = portrait.pivot = new Vector2(.5f, .5f);
        portrait.sizeDelta = portraitSize;
        portrait.anchoredPosition = new Vector2(0, portraitSize.y * .08f);

        float top = 2;
        float height = TextHeight(display.NameText, textWidth);
        Place(display.NameText.rectTransform, textLeft, top, textWidth, height);
        Place(nameBacking.rectTransform, textLeft - 3, top - 1, textWidth + 6, height + 2);
        top += height + 3;
        foreach (var bar in new[] { display.HpDisplay, display.ManaDisplay, display.HungerDisplay })
        {
            height = Mathf.Max(24, TextHeight(bar.ValueText, textWidth - 8));
            Place((RectTransform)bar.transform, textLeft, top, textWidth, height);
            top += height + 1;
        }
        Place((RectTransform)display.LevelDisplay.transform, textLeft, top + 1, textWidth, 5);
        top += 9;
        height = TextHeight(order, textWidth);
        Place(order.rectTransform, textLeft, top, textWidth, height);
        Place(orderBacking.rectTransform, textLeft - 3, top - 1, textWidth + 6, height + 2);
        orderBacking.enabled = !string.IsNullOrEmpty(order.text);

        Place((RectTransform)statuses, 2, 82, 76, 64);
        int count = 0;
        foreach (var button in StatusButtons)
        {
            if (!button.gameObject.activeSelf) continue;
            Place((RectTransform)button.transform, count % 2 * 38, count / 2 * 32, 36, 30);
            count++;
        }
        return Mathf.Max(104, top + height + 3, count > 0 ? 82 + Mathf.Ceil(count / 2f) * 32 : 0);
    }
    private static float TextHeight(TMP_Text label, float width) =>
        Mathf.Ceil(label.GetPreferredValues(label.text, width, Mathf.Infinity).y) + 3;
    private static void Place(RectTransform rect, float x, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -top);
        rect.sizeDelta = new Vector2(width, height);
    }
    public void Bind(CharacterStatsDisplay value) { display=value; statusKey=null; HudRefresh.PartyChanged(); }
    private void OnEnable() { HudRefresh.Changed += Refresh; HudRefresh.PartyChanged(); }
    private void OnDisable() => HudRefresh.Changed -= Refresh;
    internal void ShowTravelState(TownAlly hero,bool leader,bool selected)
    {
        portraitHighlight.enabled=selected;
        portraitHighlight.color=new Color(1,.84f,.3f,1);
        order.text=GameplayText.Format((leader?"Leader":"Following")+
            (hero.PendingAttributePoints>0?$"  ▲ {hero.PendingAttributePoints}":""));
        foreach(var button in StatusButtons)button.gameObject.SetActive(false);
    }
    private void Update()
    {
        // Only the turn-highlight pulse animates between model changes.
        if (portraitHighlight != null && display?.Character is Ally ally && Game.Instance != null)
            portraitHighlight.color = ally.TurnHighlightColor;
    }
    private void Refresh(HudChange changes)
    {
        if ((changes & HudChange.Party) == 0) return;
        if (display == null || display.Character is not Ally ally || Game.Instance == null) return;
        portraitHighlight.enabled = ally.IsTurnHighlighted;
        portraitHighlight.color = ally.TurnHighlightColor;
        order.text = GameplayText.Format(ally == Game.Instance.PlayerController.PartyLeader ? "Leader" :
            ally.AllyStrategy.ToString().Replace("Aggresive","Aggressive").Replace("HoldPosition","Hold"));
        if (ally.PendingCast != null) order.text = GameplayText.Format(ally.PendingCast.Name + (ally.PendingCast.Remaining == 0 ? " ready" : $"  Charging for {ally.PendingCast.Remaining} more actions"));
        if (ArrowSupply.HasBow(ally)) order.text += GameplayText.Format($"\n{ally.Equipment.EquippedShield?.DisplayName ?? "Arrows"} x{ArrowSupply.Count(ally)}");
        if (ally.PendingAttributePoints > 0) order.text += GameplayText.Format($"  ▲ {ally.PendingAttributePoints}");
        order.enableAutoSizing = true; order.fontSizeMin = 10; order.fontSizeMax = 18;
        string key=DungeonHud.StatusSummary(ally);
        if (key==statusKey) return; statusKey=key;
        foreach(var button in StatusButtons)button.gameObject.SetActive(false);
        var effects=ally.StatusEffects.Where(s=>s!=null&&!s.IsExpired()).ToArray();
        for(int i=0;i<Mathf.Min(4,effects.Length);i++)
        {
            var effect=effects[i];string name=effect.GetEffectName();
            bool overflow=i==3&&effects.Length>4;
            string badge=overflow ? "+"+(effects.Length-3) : new string(name.Where(char.IsUpper).Take(2).ToArray());
            if (!overflow && badge.Length<2) badge=name.Substring(0,Mathf.Min(2,name.Length));
            var button=StatusButtons[i]; button.gameObject.SetActive(true);
			var label=button.GetComponentInChildren<TMP_Text>();
			var icon=button.transform.Find("Status icon")?.GetComponent<Image>();
			var sprite=overflow ? null : CombatVisualCatalog.Instance?.ForStatus(effect)?.Icon;
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
			label.text=GameplayText.Format(sprite != null ? StatusPresentation.Badge(effect) : badge);
			label.textWrappingMode=TextWrappingModes.NoWrap;
			label.enableAutoSizing=true; label.fontSizeMin=10; label.fontSizeMax=17;
			var labelRect=(RectTransform)label.transform;
			labelRect.anchorMin=sprite != null ? new Vector2(.52f,0) : Vector2.zero;
			labelRect.anchorMax=Vector2.one;
			labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(()=>DungeonHud.Ensure(Game.Instance).Inspect(ally.CharacterName+"\n"+DungeonHud.StatusSummary(ally)));
        }
    }
}
