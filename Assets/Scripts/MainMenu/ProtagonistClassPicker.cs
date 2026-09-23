using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ProtagonistClassPicker : MonoBehaviour
{
	private IReadOnlyList<ClassDefinition> classes;
	private Action<ClassDefinition, ClassDefinition> confirmed;
	private Action cancelled;
	private TextMeshProUGUI detailsText;
	private Transform leftColumn;
	private Transform rightColumn;
	private int step = 1; // 1 = primary, 2 = secondary
	private ClassDefinition primaryClass;
	private bool finished = false;

	/// <summary>
	/// Builds the picker on its own overlay canvas. `confirmed(primary, secondaryOrNull)` or `cancelled()` is
	/// invoked exactly once, then the picker destroys itself.
	/// </summary>
	public static ProtagonistClassPicker Show(IReadOnlyList<ClassDefinition> classes,
		Action<ClassDefinition, ClassDefinition> confirmed, Action cancelled)
	{
		if (classes == null || classes.Count == 0)
		{
			confirmed?.Invoke(null, null);
			return null;
		}

		// Warn if no EventSystem exists, but continue
		if (EventSystem.current == null)
		{
			Debug.LogWarning("ProtagonistClassPicker: no EventSystem in scene.");
		}

		// Create root GameObject with Canvas
		var rootGO = new GameObject("ProtagonistClassPicker", typeof(RectTransform));
		var canvas = rootGO.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 1000;

		// Add CanvasScaler
		var scaler = rootGO.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);

		// Add GraphicRaycaster
		rootGO.AddComponent<GraphicRaycaster>();

		// Add ProtagonistClassPicker component
		var picker = rootGO.AddComponent<ProtagonistClassPicker>();
		picker.classes = classes;
		picker.confirmed = confirmed;
		picker.cancelled = cancelled;

		// Create full-screen dark panel
		var panelGO = new GameObject("Panel", typeof(RectTransform));
		panelGO.transform.SetParent(rootGO.transform, false);
		var panelImage = panelGO.AddComponent<Image>();
		panelImage.color = new Color(0, 0, 0, 0.85f);
		var panelRect = panelGO.GetComponent<RectTransform>();
		panelRect.anchorMin = Vector2.zero;
		panelRect.anchorMax = Vector2.one;
		panelRect.offsetMin = Vector2.zero;
		panelRect.offsetMax = Vector2.zero;

		// Create title text
		var titleGO = new GameObject("Title", typeof(RectTransform));
		titleGO.transform.SetParent(rootGO.transform, false);
		var titleText = titleGO.AddComponent<TextMeshProUGUI>();
		titleText.text = "Choose your class";
		titleText.fontSize = 48;
		titleText.alignment = TextAlignmentOptions.Center;
		var titleRect = titleGO.GetComponent<RectTransform>();
		titleRect.anchorMin = new Vector2(0.5f, 1);
		titleRect.anchorMax = new Vector2(0.5f, 1);
		titleRect.anchoredPosition = new Vector2(0, -50);
		titleRect.sizeDelta = new Vector2(1920, 100);

		// Create left column with buttons
		var leftColumnGO = new GameObject("LeftColumn", typeof(RectTransform));
		leftColumnGO.transform.SetParent(rootGO.transform, false);
		var leftLayout = leftColumnGO.AddComponent<VerticalLayoutGroup>();
		leftLayout.spacing = 8;
		leftLayout.childControlHeight = true;
		leftLayout.childControlWidth = true;
		leftLayout.childForceExpandHeight = false;
		var leftRect = leftColumnGO.GetComponent<RectTransform>();
		leftRect.anchorMin = new Vector2(0.05f, 0.1f);
		leftRect.anchorMax = new Vector2(0.45f, 0.85f);
		leftRect.offsetMin = Vector2.zero;
		leftRect.offsetMax = Vector2.zero;
		picker.leftColumn = leftColumnGO.transform;

		// Create right column with details
		var rightColumnGO = new GameObject("RightColumn", typeof(RectTransform));
		rightColumnGO.transform.SetParent(rootGO.transform, false);
		var detailsGO = new GameObject("Details", typeof(RectTransform));
		detailsGO.transform.SetParent(rightColumnGO.transform, false);
		var details = detailsGO.AddComponent<TextMeshProUGUI>();
		details.fontSize = 28;
		details.alignment = TextAlignmentOptions.TopLeft;
		details.enableWordWrapping = true;
		var detailsRect = detailsGO.GetComponent<RectTransform>();
		detailsRect.anchorMin = Vector2.zero;
		detailsRect.anchorMax = Vector2.one;
		detailsRect.offsetMin = Vector2.zero;
		detailsRect.offsetMax = Vector2.zero;
		picker.detailsText = details;
		var rightRect = rightColumnGO.GetComponent<RectTransform>();
		rightRect.anchorMin = new Vector2(0.5f, 0.1f);
		rightRect.anchorMax = new Vector2(0.95f, 0.85f);
		rightRect.offsetMin = Vector2.zero;
		rightRect.offsetMax = Vector2.zero;
		picker.rightColumn = rightColumnGO.transform;

		// Build step 1
		picker.step = 1;
		picker.BuildStep1();

		return picker;
	}

	private void BuildStep1()
	{
		// Clear left column
		foreach (Transform child in leftColumn)
		{
			Destroy(child.gameObject);
		}

		// Add class buttons
		Button firstButton = null;
		foreach (var cls in classes)
		{
			var button = AddButton(cls.DisplayName, () => SelectPrimaryClass(cls), () => ShowDetails(cls));
			if (firstButton == null)
			{
				firstButton = button;
			}
		}

		// Add Back button
		AddButton("Back", () => Finish(cancelled), null);

		// Select first button and show details
		if (firstButton != null)
		{
			firstButton.Select();
			ShowDetails(classes[0]);
		}
	}

	private void BuildStep2()
	{
		// Clear left column
		foreach (Transform child in leftColumn)
		{
			Destroy(child.gameObject);
		}

		// Add "No secondary class" button
		var noSecondaryButton = AddButton("No secondary class",
			() => Finish(() => confirmed?.Invoke(primaryClass, null)),
			() => ShowNoSecondaryDetails());

		// Add class buttons (except primary)
		foreach (var cls in classes)
		{
			if (cls.Id != primaryClass.Id)
			{
				AddButton(cls.DisplayName,
					() => Finish(() => confirmed?.Invoke(primaryClass, cls)),
					() => ShowDetailsAsSecondary(cls));
			}
		}

		// Add Back button
		AddButton("Back", () => GoBackToStep1(), null);

		// Select first button (No secondary class) and show its details
		noSecondaryButton.Select();
		ShowNoSecondaryDetails();
	}

	private void SelectPrimaryClass(ClassDefinition cls)
	{
		primaryClass = cls;
		step = 2;
		BuildStep2();
		// Update title for step 2
		var titleGO = leftColumn.parent.Find("Title");
		if (titleGO != null)
		{
			var titleText = titleGO.GetComponent<TextMeshProUGUI>();
			if (titleText != null)
			{
				titleText.text = "Choose a secondary class (optional)";
			}
		}
	}

	private void GoBackToStep1()
	{
		step = 1;
		BuildStep1();
		// Update title for step 1
		var titleGO = leftColumn.parent.Find("Title");
		if (titleGO != null)
		{
			var titleText = titleGO.GetComponent<TextMeshProUGUI>();
			if (titleText != null)
			{
				titleText.text = "Choose your class";
			}
		}
	}

	private void ShowDetails(ClassDefinition cls)
	{
		if (cls == null || detailsText == null)
			return;

		var skillsList = (cls.Skills ?? new List<ClassSkillEntryData>())
			.Where(entry => entry != null && entry.Skill != null && entry.Tier == 1)
			.Select(entry => entry.Skill.SkillName)
			.ToList();

		var skillsText = skillsList.Count > 0 ? string.Join(", ", skillsList) : "none yet";
		var weaponsText = cls.AllowedWeapons != null && cls.AllowedWeapons.Count > 0
			? string.Join(", ", cls.AllowedWeapons)
			: "none";

		detailsText.text = $"{cls.DisplayName}\n{cls.Role}\nWeapons: {weaponsText}\nTier 1 skills: {skillsText}";
	}

	private void ShowDetailsAsSecondary(ClassDefinition cls)
	{
		if (cls == null || detailsText == null)
			return;

		var skillsList = (cls.Skills ?? new List<ClassSkillEntryData>())
			.Where(entry => entry != null && entry.Skill != null && entry.Tier == 1)
			.Select(entry => entry.Skill.SkillName)
			.ToList();

		var skillsText = skillsList.Count > 0 ? string.Join(", ", skillsList) : "none yet";
		var weaponsText = cls.AllowedWeapons != null && cls.AllowedWeapons.Count > 0
			? string.Join(", ", cls.AllowedWeapons)
			: "none";

		detailsText.text = $"{cls.DisplayName}\n{cls.Role}\nWeapons: {weaponsText}\nTier 1 skills: {skillsText}\nAs secondary: tier 1-2 skills only, rank 3 max.";
	}

	private void ShowNoSecondaryDetails()
	{
		if (detailsText == null)
			return;

		detailsText.text = "Single class: full access to all tiers and ranks.";
	}

	private Button AddButton(string label, Action onClick, Action onFocus)
	{
		var buttonGO = new GameObject(label, typeof(RectTransform));
		buttonGO.transform.SetParent(leftColumn, false);

		var image = buttonGO.AddComponent<Image>();
		image.color = new Color(0.2f, 0.2f, 0.25f, 1);

		var button = buttonGO.AddComponent<Button>();
		if (onClick != null)
		{
			button.onClick.AddListener(() => onClick?.Invoke());
		}

		var layoutElement = buttonGO.AddComponent<LayoutElement>();
		layoutElement.preferredHeight = 56;

		// Add FocusRelay
		if (onFocus != null)
		{
			var relay = buttonGO.AddComponent<FocusRelay>();
			relay.Focused += onFocus;
		}

		// Add text child
		var textGO = new GameObject("Text", typeof(RectTransform));
		textGO.transform.SetParent(buttonGO.transform, false);
		var textMesh = textGO.AddComponent<TextMeshProUGUI>();
		textMesh.text = label;
		textMesh.fontSize = 30;
		textMesh.alignment = TextAlignmentOptions.Center;
		var textRect = textGO.GetComponent<RectTransform>();
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = Vector2.zero;
		textRect.offsetMax = Vector2.zero;

		return button;
	}

	private void Finish(Action callback)
	{
		if (finished)
			return;

		finished = true;
		Destroy(gameObject);
		callback?.Invoke();
	}

	private class FocusRelay : MonoBehaviour, ISelectHandler, IPointerEnterHandler
	{
		public Action Focused;

		public void OnSelect(BaseEventData eventData)
		{
			Focused?.Invoke();
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			Focused?.Invoke();
		}
	}
}
