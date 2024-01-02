using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NewFloorMessage : MonoBehaviour
{
	public Image BackgroundColor;
	public TextMeshProUGUI FloorMessage;

	public void ShowNewFloor(int floor)
	{
		this.gameObject.SetActive(true);
		BackgroundColor.color = Color.black;
		FloorMessage.text = $"Floor {floor}";

		BackgroundColor.CrossFadeAlpha(0, 3f, true);
		FloorMessage.transform.DOBlendablePunchRotation(Vector3.one * 3, 3f)
			.OnComplete(() => { this.gameObject.SetActive(false); });
	}

	[ContextMenu("Do Floor Message")]
	public void DoFloorMeesage()
	{
		ShowNewFloor(1);
	}
}
