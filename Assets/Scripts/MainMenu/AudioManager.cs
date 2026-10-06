using System.Collections;
using System.Collections.Generic;
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
    private Coroutine musicTransition;
    private const float MusicFadeSeconds = 0.4f;

    private void Start()
    {
        if (MusicAudioSource != null) MusicAudioSource.outputAudioMixerGroup = MusicAudioMixerGroup;
        foreach (var source in EffectAudioSources) if (source != null) source.outputAudioMixerGroup = EffectAudioMixerGroup;
        foreach (var source in UIAudioSources) if (source != null) source.outputAudioMixerGroup = UIAudioMixerGroup;
        AudioPreferences.Restore(MusicAudioMixerGroup, "MusicVolume");
        AudioPreferences.Restore(EffectAudioMixerGroup, "EffectVolume");
        AudioPreferences.Restore(UIAudioMixerGroup, "UIVolume", AudioPreferences.Read(EffectAudioMixerGroup, "EffectVolume"));
        AudioPreferences.Restore(MusicAudioMixerGroup, "MasterVolume");
    }

    public void PlayUISound(AudioClip soundClip)
    {
        if (soundClip == null || UIAudioSources.Count == 0) return;
        PlayFromPool(UIAudioSources, ref uiClipIndex, soundClip, UIAudioMixerGroup);
    }

	public SoundEffects SoundEffects;

	internal void PlayMusic(AudioClip musicClip)
	{
		if (MusicAudioSource == null) return;
        if (musicTransition != null) StopCoroutine(musicTransition);
        if (musicClip == MusicAudioSource.clip && MusicAudioSource.isPlaying)
        {
            musicTransition = StartCoroutine(FadeMusic(1));
            return;
        }
        musicTransition = StartCoroutine(ChangeMusic(musicClip));
	}

	internal void StopMusic()
	{
		PlayMusic(null);
	}

    private IEnumerator ChangeMusic(AudioClip next)
    {
        if (MusicAudioSource.isPlaying) yield return FadeMusic(0);
        MusicAudioSource.Stop();
        MusicAudioSource.clip = next;
        if (next != null)
        {
            MusicAudioSource.loop = true;
            MusicAudioSource.volume = 0;
            MusicAudioSource.Play();
            yield return FadeMusic(1);
        }
        musicTransition = null;
    }

    private IEnumerator FadeMusic(float target)
    {
        float initial = MusicAudioSource.volume;
        for (float elapsed = 0; elapsed < MusicFadeSeconds; elapsed += Time.unscaledDeltaTime)
        {
            MusicAudioSource.volume = Mathf.Lerp(initial, target, elapsed / MusicFadeSeconds);
            yield return null;
        }
        MusicAudioSource.volume = target;
    }

    private static void PlayFromPool(List<AudioSource> sources, ref int index, AudioClip clip, AudioMixerGroup group)
    {
        for (int attempt = 0; attempt < sources.Count; attempt++)
        {
            var source = sources[index++ % sources.Count];
            if (source == null || source.isPlaying) continue;
            source.outputAudioMixerGroup = group;
            source.PlayOneShot(clip);
            return;
        }
        // A busy pool should not cut short a prior cue.
        var fallback = sources[index++ % sources.Count];
        if (fallback != null) { fallback.outputAudioMixerGroup = group; fallback.PlayOneShot(clip); }
    }

	private int effectClipIndex;
	private float nextFootstep;
	internal void PlayFootstep(AudioClip clip, int seed, int x, int y)
	{
		if (DungeonPreferences.AnimationMode == DungeonAnimationMode.NoAnimations || clip == null ||
			Time.unscaledTime < nextFootstep || EffectAudioSources.Count == 0) return;
		nextFootstep = Time.unscaledTime + .075f;
		var source = EffectAudioSources[effectClipIndex++ % EffectAudioSources.Count];
		if (source == null || source.isPlaying) return;
		uint hash = DungeonPresentation.Hash(seed, x, y);
		source.outputAudioMixerGroup = EffectAudioMixerGroup;
		source.pitch = .94f + (hash % 13) * .01f;
		source.PlayOneShot(clip, .45f);
	}
	internal void PlayDashSounds()
	{
		if (DungeonPreferences.AnimationMode == DungeonAnimationMode.NoAnimations) return;
		PlaySoundEffect(SoundEffects?.Jump);
		StartCoroutine(CompleteDashSound());
	}
	private IEnumerator CompleteDashSound()
	{
		yield return new WaitForSecondsRealtime(.15f);
		if (DungeonPreferences.AnimationMode != DungeonAnimationMode.NoAnimations) PlaySoundEffect(SoundEffects?.Landing);
	}

	internal void PlaySoundEffect(AudioClip soundClip)
	{
        if (soundClip == null || EffectAudioSources.Count == 0) return;
        foreach (var source in EffectAudioSources) if (source != null && !source.isPlaying) source.pitch = 1;
        PlayFromPool(EffectAudioSources, ref effectClipIndex, soundClip, EffectAudioMixerGroup);
	}
}
