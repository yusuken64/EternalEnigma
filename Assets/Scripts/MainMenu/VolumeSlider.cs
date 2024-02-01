using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class VolumeSlider : MonoBehaviour
{
	public TextMeshProUGUI VolumeValueText;
	public string VolumeParameterName;
	public Slider Slider;
	public UnityEngine.Audio.AudioMixerGroup AudioMixerGroup;

	private void Start()
	{
		SetSliderValue(AudioMixerGroup.audioMixer, VolumeParameterName, Slider);
	}

	private void SetSliderValue(AudioMixer mixer, string mixerValueName, Slider slider)
	{
		mixer.GetFloat(mixerValueName, out float value);
		float logValue = value / 20;
		var sliderValue = Mathf.Pow(10, logValue);
		slider.SetValueWithoutNotify(sliderValue);
	}

	public void OnSliderChanged(float value)
	{
		float volumeValue = Mathf.Log10(value) * 20;
		UnityEngine.Audio.AudioMixerGroup effectAudioMixerGroup = Common.Instance.AudioManager.EffectAudioMixerGroup;
		effectAudioMixerGroup.audioMixer.SetFloat(VolumeParameterName, volumeValue);
	}
}
