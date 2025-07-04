using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Fart : MonoBehaviour
{
	public float CountDownTime;
	public Button OKButton;
	public TextMeshProUGUI ButtonText;

	public AudioClip Short;
	public AudioClip Short2;
	public AudioClip Long;

	public int countdownStep = 4;

	private void Start()
	{
		OKButton.interactable = false;
	}

	void Update()
	{
		CountDownTime -= Time.deltaTime;

		// Play countdown beeps based on time remaining
		if (CountDownTime <= countdownStep && countdownStep > 0)
		{
			if (countdownStep == 1)
				Common.Instance.AudioManager.PlaySoundEffect(Short2);
			else
				Common.Instance.AudioManager.PlaySoundEffect(Short);

			countdownStep--;
		}

		if (CountDownTime <= 0)
		{
			CountDownTime = 0f;
			if (countdownStep == 0)
			{
				Common.Instance.AudioManager.PlaySoundEffect(Long);
				countdownStep = -1; // prevent re-trigger
			}

			ButtonText.text = "Start";
			OKButton.interactable = true;
		}
		else
		{
			ButtonText.text = $"Wait ({CountDownTime.ToString("F2")})";
		}
	}

	public void Button_Clicked()
	{
		Common.Instance.AudioManager.PlaySoundEffect(Long);
		SceneManager.LoadScene("MainMenu");
	}
}
