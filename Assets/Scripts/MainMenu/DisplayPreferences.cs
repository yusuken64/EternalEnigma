using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IDisplayBackend
{
    Vector2Int Size { get; }
    Vector2Int Desktop { get; }
    FullScreenMode Mode { get; }
    IEnumerable<Vector2Int> Resolutions { get; }
    void Apply(Vector2Int size, FullScreenMode mode);
}

public sealed class UnityDisplayBackend : IDisplayBackend
{
    public Vector2Int Size => new(Screen.width, Screen.height);
    public Vector2Int Desktop => new(Display.main.systemWidth, Display.main.systemHeight);
    public FullScreenMode Mode => Screen.fullScreenMode;
    public IEnumerable<Vector2Int> Resolutions => Screen.resolutions.Select(r => new Vector2Int(r.width, r.height));
    public void Apply(Vector2Int size, FullScreenMode mode) => Screen.SetResolution(size.x, size.y, mode);
}

/// <summary>Global preferences, independent of campaign saves. Screen changes settle at end of frame.</summary>
public sealed class DisplayPreferences
{
    public const string ModeKey = "Settings.Display.Mode", WidthKey = "Settings.Display.Width", HeightKey = "Settings.Display.Height";
    public static readonly FullScreenMode[] Modes = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
    public static DisplayPreferences Current { get; } = new(new UnityDisplayBackend());
    public IDisplayBackend Backend { get; }
    public DisplayPreferences(IDisplayBackend backend) => Backend = backend;
    public List<Vector2Int> Choices(FullScreenMode mode)
    {
        var sizes = Backend.Resolutions.Where(s => s.x > 0 && s.y > 0).Distinct().ToList();
        if (sizes.Count == 0 || mode == FullScreenMode.Windowed) sizes.Add(Backend.Size);
        return sizes.Distinct().OrderBy(s => s.x).ThenBy(s => s.y).ToList();
    }
    public void Restore()
    {
        if (!PlayerPrefs.HasKey(ModeKey) || !PlayerPrefs.HasKey(WidthKey) || !PlayerPrefs.HasKey(HeightKey)) return;
        var mode = (FullScreenMode)PlayerPrefs.GetInt(ModeKey);
        if (!Modes.Contains(mode)) return;
        var size = new Vector2Int(PlayerPrefs.GetInt(WidthKey), PlayerPrefs.GetInt(HeightKey));
        if (size.x <= 0 || size.y <= 0) size = Backend.Size;
        if (mode != FullScreenMode.Windowed && !Backend.Resolutions.Contains(size)) size = Backend.Desktop;
        Backend.Apply(size, mode);
    }
    public void Commit(Vector2Int size, FullScreenMode mode)
    {
        if (!Modes.Contains(mode) || size.x <= 0 || size.y <= 0) return;
        Backend.Apply(size, mode);
        PlayerPrefs.SetInt(ModeKey, (int)mode);
        PlayerPrefs.SetInt(WidthKey, size.x);
        PlayerPrefs.SetInt(HeightKey, size.y);
        PlayerPrefs.Save();
    }
}
