using UnityEngine;

public enum DungeonAnimationMode { Current, ControllingHero, YourActionOnly, None }

public static class DungeonPreferences
{
    public static bool? FullControlOverride;
    public static DungeonAnimationMode? AnimationOverride;
    public static bool FullControl
    {
        get => !AutoplayRunner.BlocksPlayerInput &&
            (FullControlOverride ?? PlayerPrefs.GetInt("Dungeon.FullControl", 0) != 0);
        set { PlayerPrefs.SetInt("Dungeon.FullControl", value ? 1 : 0); PlayerPrefs.Save(); }
    }
    public static DungeonAnimationMode AnimationMode
    {
        get => AnimationOverride ?? (DungeonAnimationMode)Mathf.Clamp(PlayerPrefs.GetInt("Dungeon.AnimationMode", 0), 0, 3);
        set { PlayerPrefs.SetInt("Dungeon.AnimationMode", (int)value); PlayerPrefs.Save(); }
    }
    public static string SpeedLabel => AnimationMode switch
    {
        DungeonAnimationMode.ControllingHero => "Controlling hero",
        DungeonAnimationMode.YourActionOnly => "Your action only",
        DungeonAnimationMode.None => "No animation",
        _ => "Current"
    };
}
