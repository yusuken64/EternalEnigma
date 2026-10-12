using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

// Opt-in measurements for the manual finale harness. Inactive in ordinary play.
internal static class FinalePerformance
{
    [Serializable] internal sealed class Sample
    {
        public string Name;
        public int Calls;
        public double Milliseconds, MaximumMilliseconds;
    }
    static readonly Dictionary<string, Sample> samples = new();
    internal static bool Enabled;
    internal static void Begin() { samples.Clear(); Enabled = true; }
    internal static Sample[] Finish() { Enabled = false; return samples.Values.OrderByDescending(s => s.Milliseconds).ToArray(); }
    internal readonly struct Scope : IDisposable
    {
        readonly string name;
        readonly long start;
        internal Scope(string name) { this.name = name; start = Enabled ? Stopwatch.GetTimestamp() : 0; }
        public void Dispose()
        {
            if (start == 0) return;
            double elapsed = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
            if (!samples.TryGetValue(name, out var sample)) samples.Add(name, sample = new Sample { Name = name });
            sample.Calls++; sample.Milliseconds += elapsed; sample.MaximumMilliseconds = Math.Max(sample.MaximumMilliseconds, elapsed);
        }
    }
}
