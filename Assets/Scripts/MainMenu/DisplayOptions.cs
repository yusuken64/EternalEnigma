using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public sealed class DisplayOptions : MonoBehaviour
{
    public TMP_Dropdown WindowMode;
    public TMP_Dropdown Resolution;
    private List<Vector2Int> sizes;
    private Vector2Int lastSize;
    private FullScreenMode lastMode;
    private Coroutine pending;

    private void Awake()
    {
        TerminalModeButton.Ensure(transform, Resolution);
        WindowMode.onValueChanged.AddListener(ChangeMode);
        Resolution.onValueChanged.AddListener(ChangeResolution);
    }
    private void OnEnable() => Refresh();
    private void OnDisable() { if (pending != null) StopCoroutine(pending); pending = null; }
    public void Refresh()
    {
        var preferences = DisplayPreferences.Current;
        lastMode = preferences.Backend.Mode;
        lastSize = preferences.Backend.Size;
        WindowMode.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(DisplayPreferences.Modes, lastMode)));
        sizes = preferences.Choices(lastMode);
        // Reflect the actual render size even if the platform chose a fallback.
        if (!sizes.Contains(lastSize)) sizes.Add(lastSize);
        sizes = sizes.OrderBy(s => s.x).ThenBy(s => s.y).ToList();
        Resolution.ClearOptions();
        Resolution.AddOptions(sizes.Select(s => $"{s.x} x {s.y}").ToList());
        Resolution.SetValueWithoutNotify(sizes.IndexOf(lastSize));
        Resolution.RefreshShownValue();
        WindowMode.RefreshShownValue();
    }
    private void Update()
    {
        var backend = DisplayPreferences.Current.Backend;
        if (pending == null && !WindowMode.IsExpanded && !Resolution.IsExpanded && (backend.Size != lastSize || backend.Mode != lastMode)) Refresh();
    }
    private void ChangeMode(int value)
    {
        var preferences = DisplayPreferences.Current;
        var mode = DisplayPreferences.Modes[value];
        var size = preferences.Backend.Size;
        if (mode != FullScreenMode.Windowed && !preferences.Choices(mode).Contains(size)) size = preferences.Backend.Desktop;
        Apply(size, mode);
    }
    private void ChangeResolution(int value) => Apply(sizes[value], DisplayPreferences.Current.Backend.Mode);
    private void Apply(Vector2Int size, FullScreenMode mode)
    {
        DisplayPreferences.Current.Commit(size, mode);
        if (pending != null) StopCoroutine(pending);
        pending = StartCoroutine(RefreshAfterApply());
    }
    private IEnumerator RefreshAfterApply()
    {
        yield return null;
        yield return null;
        pending = null;
        Refresh();
    }
}
