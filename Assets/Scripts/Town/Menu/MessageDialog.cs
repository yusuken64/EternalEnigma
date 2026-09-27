using JuicyChickenGames.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MessageDialog : Dialog
{
	public TextMeshProUGUI PromptText;
	public Button OkButton;

	private void Awake()
	{
		this.gameObject.SetActive(false);
	}

    private void OnEnable()
    {
        var panel = (RectTransform)PromptText.transform.parent;
        Fit(panel, new Vector2(.12f, .18f), new Vector2(.88f, .82f));
        Fit(PromptText.rectTransform, new Vector2(.06f, .28f), new Vector2(.94f, .92f));
        PromptText.alignment = TextAlignmentOptions.Center;
        PromptText.enableAutoSizing = true;
        PromptText.fontSizeMin = 16;
        PromptText.fontSizeMax = 28;
        PromptText.textWrappingMode = TextWrappingModes.Normal;
        Fit((RectTransform)OkButton.transform, new Vector2(.3f, .06f), new Vector2(.7f, .20f));
        var caption = OkButton.GetComponentInChildren<TMP_Text>();
        caption.enableAutoSizing = true;
        caption.fontSizeMin = 16;
        caption.fontSizeMax = 24;
        foreach (Transform child in transform)
        {
            if (child == panel) continue;
            var backdrop = child.GetComponent<Image>();
            if (backdrop == null) continue;
            backdrop.sprite = null;
            backdrop.color = new Color(0, 0, 0, .65f);
            backdrop.raycastTarget = true;
        }
    }

    private static void Fit(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.localScale = Vector3.one;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

	public void Ok_Clicked()
	{
		CloseDialog();
	}

	internal override void SetFirstSelect()
	{
		var eventSystem = EventSystem.current;
		if (eventSystem != null)
        {
            eventSystem.SetSelectedGameObject(null);
            eventSystem.SetSelectedGameObject(OkButton.gameObject);
        }

		var nav = new Navigation
		{
			mode = Navigation.Mode.None
		};
		OkButton.navigation = nav;
	}
}
