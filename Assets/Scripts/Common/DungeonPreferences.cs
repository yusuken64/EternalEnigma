using UnityEngine;

public enum DungeonAnimationMode { Normal, AnimateAlliedActions, AnimateControlledHeroActions, NoAnimations }

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
        get => AnimationOverride ?? SavedAnimationMode;
        set { PlayerPrefs.SetInt("Dungeon.AnimationModeV2", (int)value); PlayerPrefs.Save(); }
    }
    private static DungeonAnimationMode SavedAnimationMode
    {
        get
        {
            if (PlayerPrefs.HasKey("Dungeon.AnimationModeV2"))
                return (DungeonAnimationMode)Mathf.Clamp(PlayerPrefs.GetInt("Dungeon.AnimationModeV2"), 0, 3);
            int old = Mathf.Clamp(PlayerPrefs.GetInt("Dungeon.AnimationMode", 0), 0, 3);
            var migrated = old == 0 ? DungeonAnimationMode.Normal :
                old == 3 ? DungeonAnimationMode.NoAnimations : DungeonAnimationMode.AnimateControlledHeroActions;
            PlayerPrefs.SetInt("Dungeon.AnimationModeV2", (int)migrated);
            PlayerPrefs.Save();
            return migrated;
        }
    }
    public static string SpeedLabel => AnimationMode switch
    {
        DungeonAnimationMode.AnimateAlliedActions => "Animate allied actions",
        DungeonAnimationMode.AnimateControlledHeroActions => "Animate only controlled hero actions",
        DungeonAnimationMode.NoAnimations => "No animations",
        _ => "Normal"
    };
}
