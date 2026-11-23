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
    private Coroutine musicTransition, playlist;
    private readonly Dictionary<AudioClip, int> musicPositions = new();
    private const float MusicFadeSeconds = 0.4f;
    public event System.Action<AudioClip, AudioMixerGroup> SoundPlayed;
    public event System.Action<SoundCue, AudioClip, AudioMixerGroup> CuePlayed;
    private readonly Dictionary<SoundCue, float> lastCueTime = new();
    private readonly Dictionary<AudioClip, int> uiFrames = new();
    private int lastUIActionFrame = -1;

    protected override void Initialize()
    {
        TerminalAudioFilter.Ensure(MusicAudioSource);
        if (EffectAudioSources != null)
            foreach (var source in EffectAudioSources) TerminalAudioFilter.Ensure(source);
        foreach (var source in UIAudioSources) TerminalAudioFilter.Ensure(source);
    }

    public void PlayCue(SoundCue cue)
    {
        var clip = SoundEffects?.For(cue);
        if (clip == null) return;
        bool ui = global::SoundEffects.IsUI(cue);
        if ((cue == SoundCue.Navigate || cue == SoundCue.MenuOpen || cue == SoundCue.MenuClose) && lastUIActionFrame == Time.frameCount) return;
        float interval = cue switch
        {
            SoundCue.Navigate => .04f, SoundCue.Denied => .16f, SoundCue.Wait => .12f,
            SoundCue.LevelUp => .18f, SoundCue.Status => .06f, _ => 0
        };
        if (lastCueTime.TryGetValue(cue, out float last) && Time.unscaledTime - last < interval) return;
        var group = ui ? UIAudioMixerGroup : EffectAudioMixerGroup;
        bool played = ui ? PlayUI(clip, global::SoundEffects.Gain(cue), true) : PlayEffect(clip, global::SoundEffects.Gain(cue));
        if (!played) return;
        lastCueTime[cue] = Time.unscaledTime;
        if (ui && cue != SoundCue.Navigate) lastUIActionFrame = Time.frameCount;
        CuePlayed?.Invoke(cue, clip, group);
    }

    private void Start()
    {
        if (MusicAudioSource != null) MusicAudioSource.outputAudioMixerGroup = MusicAudioMixerGroup;
        foreach (var source in EffectAudioSources) if (source != null) source.outputAudioMixerGroup = EffectAudioMixerGroup;
        foreach (var source in UIAudioSources) if (source != null) source.outputAudioMixerGroup = UIAudioMixerGroup;
        AudioPreferences.Restore(MusicAudioMixerGroup, "MusicVolume");
        AudioPreferences.Restore(EffectAudioMixerGroup, "EffectVolume");
        AudioPreferences.Restore(UIAudioMixerGroup, "UIVolume");
        AudioPreferences.Restore(MusicAudioMixerGroup, "MasterVolume");
    }

    public void PlayUISound(AudioClip soundClip)
    {
        PlayUI(soundClip, .36f);
    }

    private bool PlayUI(AudioClip clip, float gain, bool deduplicate = false)
    {
        if (clip == null || UIAudioSources.Count == 0 || deduplicate && uiFrames.TryGetValue(clip, out int frame) && frame == Time.frameCount) return false;
        if (!PlayFromPool(UIAudioSources, ref uiClipIndex, clip, UIAudioMixerGroup, gain)) return false;
        uiFrames[clip] = Time.frameCount;
        SoundPlayed?.Invoke(clip, UIAudioMixerGroup);
        return true;
    }

	public SoundEffects SoundEffects;

    private string requestedTrack;

    /// <summary>
    /// Plays <paramref name="musicClip"/>, or, when a deferred build shipped the music separately,
    /// the named <paramref name="track"/> once it has downloaded. The current music keeps playing meanwhile.
    /// </summary>
    internal void PlayScore(AudioClip first, string firstTrack, AudioClip second, string secondTrack)
    {
        PlayMusic(first, firstTrack);
        if (MusicAudioSource == null || (first == null && string.IsNullOrEmpty(firstTrack)) ||
            (second == null && string.IsNullOrEmpty(secondTrack)) ||
            (first != null ? first == second : firstTrack == secondTrack)) return;
        playlist = StartCoroutine(AlternateTracks(first, firstTrack, second, secondTrack));
    }

    private IEnumerator AlternateTracks(AudioClip first, string firstTrack, AudioClip second, string secondTrack)
    {
        bool onFirst = true;
        int previousSample = -1;
        while (true)
        {
            yield return null;
            var source = MusicAudioSource;
            var expected = onFirst ? first : second;
            string track = onFirst ? firstTrack : secondTrack;
            if (!source.isPlaying || source.clip == null || (expected != null ? source.clip != expected : source.clip.name != track))
                continue;
            int sample = source.timeSamples;
            if (sample >= source.clip.samples - source.clip.frequency * MusicFadeSeconds || sample < previousSample)
            {
                onFirst = !onFirst;
                previousSample = -1;
                RequestTrack(onFirst ? first : second, onFirst ? firstTrack : secondTrack);
            }
            else previousSample = sample;
        }
    }

    internal void PlayMusic(AudioClip musicClip, string track)
    {
        if (playlist != null) { StopCoroutine(playlist); playlist = null; }
        RequestTrack(musicClip, track);
    }

    private void RequestTrack(AudioClip musicClip, string track)
    {
        if (musicClip != null || string.IsNullOrEmpty(track)) { requestedTrack = null; ChangeTo(musicClip); return; }
        if (requestedTrack == track) return;
        requestedTrack = track;
        GameContent.LoadTrack(track, clip =>
        {
            if (this != null && requestedTrack == track && clip != null) ChangeTo(clip);
        });
    }

	internal void PlayMusic(AudioClip musicClip) => PlayMusic(musicClip, null);

	private void ChangeTo(AudioClip musicClip)
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
        var outgoing = MusicAudioSource.clip;
        if (outgoing != null && MusicAudioSource.isPlaying)
        {
            int sample = MusicAudioSource.timeSamples;
            musicPositions[outgoing] = sample < outgoing.samples - outgoing.frequency ? sample : 0;
            yield return FadeMusic(0);
        }
        MusicAudioSource.Stop();
        MusicAudioSource.clip = next;
        if (next != null)
        {
            MusicAudioSource.loop = true;
            MusicAudioSource.volume = 0;
            if (musicPositions.TryGetValue(next, out int resume)) MusicAudioSource.timeSamples = resume;
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

    private static bool PlayFromPool(List<AudioSource> sources, ref int index, AudioClip clip, AudioMixerGroup group, float gain)
    {
        AudioSource fallback = null;
        for (int attempt = 0; attempt < sources.Count; attempt++)
        {
            var source = sources[index % sources.Count];
            index = (index + 1) % sources.Count;
            if (source == null) continue;
            fallback ??= source;
            if (source.isPlaying) continue;
            source.outputAudioMixerGroup = group;
            source.pitch = 1;
            source.PlayOneShot(clip, gain);
            return true;
        }
        // A busy pool should not cut short a prior cue.
        if (fallback == null) return false;
        fallback.outputAudioMixerGroup = group;
        fallback.pitch = 1;
        fallback.PlayOneShot(clip, gain);
        return true;
    }

	private int effectClipIndex;
	internal void PlaySoundEffect(AudioClip soundClip)
	{
        PlayEffect(soundClip, .6f);
	}

    private bool PlayEffect(AudioClip clip, float gain)
    {
        if (clip == null || EffectAudioSources.Count == 0 || !PlayFromPool(EffectAudioSources, ref effectClipIndex, clip, EffectAudioMixerGroup, gain)) return false;
        SoundPlayed?.Invoke(clip, EffectAudioMixerGroup);
        return true;
    }
}
