using DG.Tweening;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NewFloorMessage : MonoBehaviour
{
    private static string Label(int floor) => Game.Instance?.Finale != null ? Game.Instance.Finale.Title : Common.Instance?.GameSaveData?.DungeonSaveData?.Variant == EternalEnigma.Core.World.DungeonVariant.Town
        ? $"Town district · Floor {floor}" : $"Floor {floor}";
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
		FloorMessage.transform.DOKill();
		if (DungeonPreferences.AnimationMode == DungeonAnimationMode.NoAnimations ||
            Common.Instance?.ScreenTransition?.HasTravelPresentation == true)
		{
			gameObject.SetActive(false);
			return;
		}
		this.gameObject.SetActive(true);
		BackgroundColor.sprite = null;
		BackgroundColor.color = Color.black;
        FloorMessage.color = GameUITheme.LightInk;
		FloorMessage.text = GameplayText.Format(Label(floor));

		BackgroundColor.CrossFadeAlpha(0, 3f, true);
        if (decorativeFrame != null) decorativeFrame.CrossFadeAlpha(0, 3f, true);
        FloorMessage.CrossFadeAlpha(0, 3f, true);
		FloorMessage.transform.DOBlendablePunchRotation(Vector3.one * 3, 3f)
			.SetUpdate(true)
			.OnComplete(() => { this.gameObject.SetActive(false); });
	}

	internal void HideScreen(int destinationFloor)
	{
		// Show the destination while its layout is being generated.
		FloorMessage.text = GameplayText.Format(Label(destinationFloor));
		FloorMessage.transform.DOKill();
		FloorMessage.transform.localRotation = Quaternion.identity;
		bool wasVisible = gameObject.activeSelf;
		gameObject.SetActive(true);
		BackgroundColor.sprite = null;
		BackgroundColor.color = Color.black;
        if (!wasVisible) BackgroundColor.CrossFadeAlpha(0, 0, true);
        BackgroundColor.CrossFadeAlpha(1,
            DungeonPreferences.AnimationMode == DungeonAnimationMode.NoAnimations ? 0 : 1f, true);
        FloorMessage.color = GameUITheme.LightInk;
        FloorMessage.CrossFadeAlpha(1, 0, true);
        if (decorativeFrame != null) decorativeFrame.CrossFadeAlpha(1, 0, true);
	}

    internal IEnumerator CoverFloor(int destinationFloor)
    {
        HideScreen(destinationFloor);
        if (DungeonPreferences.AnimationMode != DungeonAnimationMode.NoAnimations)
            yield return new WaitForSecondsRealtime(1f);
        // Pin the exact endpoint before rendering; Graphic's fade coroutine and
        // this coroutine need not resume in the same order on the final frame.
        BackgroundColor.CrossFadeAlpha(1, 0, true);
        yield return ScreenTransition.PresentLoadingScreen();
    }

	[ContextMenu("Do Floor Message")]
	public void DoFloorMeesage()
	{
		ShowNewFloor(Game.Instance != null ? Game.Instance.PlayerController.Floor : 1);
	}
}
