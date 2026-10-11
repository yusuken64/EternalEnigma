using TWC;
using UnityEngine;

/// <summary>A runtime reference to the shared friendly-town template; never mutated during generation.</summary>
[CreateAssetMenu(menuName = "Game/Town Dungeon Assets")]
public sealed class TownDungeonAssets : ScriptableObject
{
    public TileWorldCreatorAsset Template;
    public static TileWorldCreatorAsset SharedTemplate => Resources.Load<TownDungeonAssets>("TownDungeonAssets")?.Template;
}
