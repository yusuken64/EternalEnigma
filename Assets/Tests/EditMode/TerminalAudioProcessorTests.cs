using System;
using System.Linq;
using NUnit.Framework;

namespace EternalEnigma.Tests
{
    public sealed class TerminalAudioProcessorTests
    {
        [Test]
        public void BypassAndSilenceAreUnchanged()
        {
            var processor = new TerminalAudioProcessor();
            var dry = new[] { -1f, -.1031f, 0, .0001f, .72345f, 1 };
            var samples = (float[])dry.Clone();
            processor.Process(samples, 2, 48000, false);
            Assert.That(samples, Is.EqualTo(dry));
            samples = new float[4096];
            processor.Process(samples, 2, 48000, true);
            Assert.That(samples, Is.All.Zero);
        }

        [Test]
        public void FullEffectUses256SignedLevelsAndClampsOverload()
        {
            var processor = Warm(11025);
            var samples = Enumerable.Range(0, 4097).Select(i => i / 1024f - 2).ToArray();
            processor.Process(samples, 1, 11025, true);
            Assert.That(samples.Distinct().Count(), Is.EqualTo(256));
            Assert.That(samples.Min(), Is.EqualTo(-1));
            Assert.That(samples.Max(), Is.EqualTo(127f / 128));
            Assert.That(samples.All(s => s * 128 == Math.Round(s * 128)), Is.True);
            Assert.That(samples[2048], Is.Zero);
        }

        [Test]
        public void StereoSampleHoldPreservesChannelsAndDuration()
        {
            var processor = Warm(44100, 2);
            var samples = new[] { .25f, -.5f, .5f, -.25f, .75f, .125f, -.875f, .375f,
                -.25f, .5f, -.5f, .25f, -.75f, -.125f, .875f, -.375f };
            processor.Process(samples, 2, 44100, true);
            Assert.That(samples, Is.EqualTo(new[] { .25f, -.5f, .25f, -.5f, .25f, -.5f, .25f, -.5f,
                -.25f, .5f, -.25f, .5f, -.25f, .5f, -.25f, .5f }));
        }

        [Test]
        public void FractionalSampleHoldIsContinuousAcrossAudioBuffers()
        {
            var input = Enumerable.Range(0, 8000).Select(i => (float)Math.Sin(i * .137) * .7f).ToArray();
            var whole = (float[])input.Clone();
            new TerminalAudioProcessor().Process(whole, 2, 48000, true);
            var processor = new TerminalAudioProcessor();
            var first = input.Take(274).ToArray();
            var second = input.Skip(274).Take(1666).ToArray();
            var third = input.Skip(1940).ToArray();
            foreach (var block in new[] { first, second, third }) processor.Process(block, 2, 48000, true);
            Assert.That(first.Concat(second).Concat(third), Is.EqualTo(whole));
        }

        [Test]
        public void ToggleRampsToDryAndReentryDoesNotReplayHeldSamples()
        {
            var processor = Warm(44100);
            var signal = Enumerable.Repeat(.303f, 2048).ToArray();
            processor.Process(signal, 1, 44100, true);
            var restoring = Enumerable.Repeat(.303f, 2048).ToArray();
            processor.Process(restoring, 1, 44100, false);
            Assert.That(restoring[0], Is.Not.EqualTo(.303f), "Disabling should ramp, not abruptly switch.");
            Assert.That(restoring[2047], Is.EqualTo(.303f));
            var silence = new float[2048];
            processor.Process(silence, 1, 44100, false);
            processor.Process(silence, 1, 44100, true);
            Assert.That(silence, Is.All.Zero);
        }

        [Test]
        public void OutputConfigurationChangeDropsStaleChannelSamples()
        {
            var processor = Warm(48000, 2);
            processor.Process(new[] { .5f, -.5f }, 2, 48000, true);
            var samples = new float[128];
            processor.Process(samples, 1, 44100, true);
            Assert.That(samples, Is.All.Zero);
        }

        private static TerminalAudioProcessor Warm(int rate, int channels = 1)
        {
            var processor = new TerminalAudioProcessor();
            processor.Process(new float[4096 * channels], channels, rate, true);
            return processor;
        }
    }
}
