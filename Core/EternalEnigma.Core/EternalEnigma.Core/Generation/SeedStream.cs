namespace EternalEnigma.Core.Generation;

// SplitMix64 with explicit overflow and rejection sampling. Stable across .NET/Unity runtimes.
internal sealed class SeedStream
{
    private ulong state;
    public SeedStream(int seed, uint stream) { state = unchecked((ulong)(uint)seed | ((ulong)stream << 32)); }
    private ulong Next()
    {
        unchecked
        {
            ulong z = (state += 0x9e3779b97f4a7c15UL);
            z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL;
            z = (z ^ (z >> 27)) * 0x94d049bb133111ebUL;
            return z ^ (z >> 31);
        }
    }
    public int Range(int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
        ulong bound = (ulong)exclusiveMaximum;
        ulong threshold = unchecked(0UL - bound) % bound;
        ulong value;
        do { value = Next(); } while (value < threshold);
        return (int)(value % bound);
    }
    public List<T> Shuffle<T>(IEnumerable<T> values)
    {
        var result = values.ToList();
        for (int i = result.Count - 1; i > 0; --i)
        { int j = Range(i + 1); (result[i], result[j]) = (result[j], result[i]); }
        return result;
    }
}
