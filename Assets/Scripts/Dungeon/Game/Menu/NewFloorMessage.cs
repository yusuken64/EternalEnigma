using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NewFloorMessage : MonoBehaviour
{
	public Image BackgroundColor;
	public TextMeshProUGUI FloorMessage;
    [SerializeField] private Image decorativeFrame;

    #if UNITY_EDITOR
    public void AuthorLayout()
    {
        // A separate canvas keeps the fade above HUD canvases created at runtime.
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = ScreenTransition.FloorOverlayOrder;
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
    }
#endif

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
        if (decorativeFrame != null) decorativeFrame.CrossFadeAlpha(0, 3f, true);
		FloorMessage.transform.DOKill();
		FloorMessage.transform.DOBlendablePunchRotation(Vector3.one * 3, 3f)
			.SetUpdate(true)
			.OnComplete(() => { this.gameObject.SetActive(false); });
	}

	internal void HideScreen(int destinationFloor)
	{
		// Show the destination while its layout is being generated.
		FloorMessage.text = $"Floor {destinationFloor}";
		FloorMessage.transform.DOKill();
		FloorMessage.transform.localRotation = Quaternion.identity;
		this.gameObject.SetActive(true);
		BackgroundColor.CrossFadeAlpha(1, 1.0f, true);
		BackgroundColor.sprite = null;
		BackgroundColor.color = Color.black;
        FloorMessage.color = GameUITheme.LightInk;
        if (decorativeFrame != null) decorativeFrame.CrossFadeAlpha(1, 0, true);
	}

	[ContextMenu("Do Floor Message")]
	public void DoFloorMeesage()
	{
		ShowNewFloor(Game.Instance != null ? Game.Instance.PlayerController.Floor : 1);
	}
}
