using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class VolumeSlider : MonoBehaviour
{
    public TextMeshProUGUI VolumeValueText;
    public string VolumeParameterName;
    public Slider Slider;
    public AudioMixerGroup AudioMixerGroup;

    private void Start() => Refresh();
    private void OnEnable() => Refresh();
    public void Refresh()
    {
        if (Slider == null || AudioMixerGroup == null) return;
        Slider.minValue = 0;
        Slider.maxValue = 1;
        Slider.SetValueWithoutNotify(AudioPreferences.Read(AudioMixerGroup, VolumeParameterName));
        UpdateLabel(Slider.value);
    }
    private void UpdateLabel(float value)
    {
        if (VolumeValueText != null) VolumeValueText.text = Mathf.RoundToInt(value * 100) + "%";
    }
    public void OnSliderChanged(float value)
    {
        AudioPreferences.Set(AudioMixerGroup, VolumeParameterName, value);
        UpdateLabel(value);
    }
}
