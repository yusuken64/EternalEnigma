using System.Collections.Generic;

/// <summary>Cosmetic variation must never advance the turn system's Unity random stream.</summary>
internal static class VisualRandom
{
    private static readonly System.Random random = new();
    internal static T Pick<T>(IReadOnlyList<T> choices) => choices==null || choices.Count==0 ? default : choices[random.Next(choices.Count)];
}
