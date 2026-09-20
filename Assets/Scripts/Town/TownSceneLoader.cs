using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TownSceneLoader
{
    public const string SceneName = "Town";
    public static TownConfiguration Default => Resources.Load<TownConfiguration>("Towns/DefaultTown");

    public static TownConfiguration ResolveSaved(TownConfiguration fallback = null)
    {
        var common = Common.Instance;
        string id = common.GameSaveData?.TownSaveData.ConfigurationId;
        if (common.CurrentTownConfiguration != null && common.CurrentTownConfiguration.Id == id)
            return common.CurrentTownConfiguration;
        return (!string.IsNullOrEmpty(id) ? Resources.Load<TownConfiguration>("Towns/" + id) : null) ?? fallback ?? Default;
    }

    // Call before loading the scene, including custom/additive test or editor loaders.
    public static void Configure(TownConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        configuration.Validate();
        Common.Instance.CurrentTownConfiguration = configuration;
        if (Common.Instance.GameSaveData != null)
            Common.Instance.GameSaveData.TownSaveData.ConfigurationId = configuration.Id;
    }

    public static void Load(TownConfiguration configuration, bool transition = true)
    {
        Configure(configuration);
        if (transition)
            Common.Instance.ScreenTransition.DoTransition(() => SceneManager.LoadScene(SceneName), autoOpen: false);
        else SceneManager.LoadScene(SceneName);
    }
}
