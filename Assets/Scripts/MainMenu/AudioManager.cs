using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : SingletonMonoBehaviour<AudioManager>
{
	public AudioMixerGroup MusicAudioMixerGroup;
	public AudioMixerGroup EffectAudioMixerGroup;
    public AudioMixerGroup UIAudioMixerGroup;

	public AudioSource MusicAudioSource;
	public List<AudioSource> EffectAudioSources;
    public List<AudioSource> UIAudioSources = new();
    private int uiClipIndex;

    private void Start()
    {
        AudioPreferences.Restore(MusicAudioMixerGroup, "MusicVolume");
        AudioPreferences.Restore(EffectAudioMixerGroup, "EffectVolume");
        AudioPreferences.Restore(UIAudioMixerGroup, "UIVolume", AudioPreferences.Read(EffectAudioMixerGroup, "EffectVolume"));
    }

    public void PlayUISound(AudioClip soundClip)
    {
        if (soundClip == null || UIAudioSources.Count == 0) return;
        var source = UIAudioSources[uiClipIndex++ % UIAudioSources.Count];
        source.clip = soundClip;
        source.Play();
    }

	public SoundEffects SoundEffects;

	internal void PlayMusic(AudioClip musicClip)
	{
		MusicAudioSource.clip = musicClip;
		MusicAudioSource.loop = true;
		MusicAudioSource.Play();
	}

	internal void StopMusic()
	{
		MusicAudioSource.Stop();
	}

	private int effectClipIndex;

	internal void PlaySoundEffect(AudioClip soundClip)
	{
        if (soundClip == null || EffectAudioSources.Count == 0) return;
		effectClipIndex++;
		effectClipIndex %= EffectAudioSources.Count();

		var audioSource = EffectAudioSources[effectClipIndex];
		audioSource.clip = soundClip;
		audioSource.Play();
	}
}
