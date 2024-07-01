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

	public AudioSource MusicAudioSource;
	public List<AudioSource> EffectAudioSources;

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
		effectClipIndex++;
		effectClipIndex %= EffectAudioSources.Count();

		var audioSource = EffectAudioSources[effectClipIndex];
		audioSource.clip = soundClip;
		audioSource.Play();
	}
}
