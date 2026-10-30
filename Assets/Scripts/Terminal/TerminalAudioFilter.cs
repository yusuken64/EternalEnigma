using System;
using UnityEngine;

// Process each source before the mixer, so volume controls retain their full range.
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class TerminalAudioFilter : MonoBehaviour
{
    private readonly TerminalAudioProcessor processor = new();
    private volatile int outputSampleRate = 48000;

    internal static void Ensure(AudioSource source)
    {
        if (source != null && source.GetComponent<TerminalAudioFilter>() == null)
            source.gameObject.AddComponent<TerminalAudioFilter>();
    }

    private void OnEnable()
    {
        UpdateSampleRate(false);
        AudioSettings.OnAudioConfigurationChanged += UpdateSampleRate;
    }

    private void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= UpdateSampleRate;
    private void UpdateSampleRate(bool deviceChanged) => outputSampleRate = AudioSettings.outputSampleRate;

    // Keep this component enabled: bypass in DSP avoids rebuilding Unity's audio graph
    // and lets already-playing music and one-shots transition without restarting.
    private void OnAudioFilterRead(float[] data, int channels) =>
        processor.Process(data, channels, outputSampleRate, TerminalMode.Effective);
}

// State belongs exclusively to the audio thread. No allocations, locks or Unity API calls.
internal sealed class TerminalAudioProcessor
{
    internal const int SampleRate = 11025;
    internal const float TransitionSeconds = .015f;
    private readonly float[] held = new float[8]; // Unity supports up to 7.1 output.
    private double remainingFrames;
    private float mix;
    private int previousChannels;
    private int previousSampleRate;

    internal void Process(float[] data, int channels, int outputSampleRate, bool active)
    {
        if (channels < 1 || channels > held.Length || outputSampleRate < 1) return;
        if (!active && mix == 0)
        {
            remainingFrames = 0;
            return; // Exact dry signal, including silence.
        }
        if (channels != previousChannels || outputSampleRate != previousSampleRate)
        {
            remainingFrames = 0;
            previousChannels = channels;
            previousSampleRate = outputSampleRate;
        }

        double holdFrames = Math.Max(1, (double)outputSampleRate / SampleRate);
        float mixStep = 1 / (TransitionSeconds * outputSampleRate);
        for (int frame = 0; frame + channels <= data.Length; frame += channels)
        {
            mix = active ? Math.Min(1, mix + mixStep) : Math.Max(0, mix - mixStep);
            if (remainingFrames <= 0)
            {
                for (int channel = 0; channel < channels; channel++)
                {
                    // Signed 8-bit PCM: 256 levels, with an exact zero for silence.
                    held[channel] = (float)Math.Max(-128, Math.Min(127,
                        Math.Round(data[frame + channel] * 128))) / 128;
                }
                remainingFrames += holdFrames;
            }
            remainingFrames--;
            for (int channel = 0; channel < channels; channel++)
            {
                int index = frame + channel;
                if (mix == 1) data[index] = held[channel];
                else if (mix > 0) data[index] += (held[channel] - data[index]) * mix;
            }
        }
    }
}
