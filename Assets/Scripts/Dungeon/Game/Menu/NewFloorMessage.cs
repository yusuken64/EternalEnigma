using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NewFloorMessage : MonoBehaviour
{
	public Image BackgroundColor;
	public TextMeshProUGUI FloorMessage;

    private void OnEnable()
    {
        // A separate canvas keeps the fade above HUD canvases created at runtime.
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = ScreenTransition.FloorOverlayOrder;
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
    }

	public void ShowNewFloor(int floor)
	{
		if (DungeonPreferences.AnimationMode == DungeonAnimationMode.None)
		{
			gameObject.SetActive(false);
			return;
		}
		this.gameObject.SetActive(true);
		BackgroundColor.sprite = null;
		BackgroundColor.color = Color.black;
        FloorMessage.color = GameUITheme.LightInk;
		FloorMessage.text = $"Floor {floor}";

		BackgroundColor.CrossFadeAlpha(0, 3f, true);
		FloorMessage.transform.DOBlendablePunchRotation(Vector3.one * 3, 3f)
			.OnComplete(() => { this.gameObject.SetActive(false); });
	}

	internal void HideScreen(int destinationFloor)
	{
		// Show the destination while its layout is being generated.
		FloorMessage.text = $"Floor {destinationFloor}";
		BackgroundColor.CrossFadeAlpha(1, 1.0f, true);
		this.gameObject.SetActive(true);
		BackgroundColor.sprite = null;
		BackgroundColor.color = Color.black;
        FloorMessage.color = GameUITheme.LightInk;
	}

	[ContextMenu("Do Floor Message")]
	public void DoFloorMeesage()
	{
		ShowNewFloor(Game.Instance != null ? Game.Instance.PlayerController.Floor : 1);
	}
}
