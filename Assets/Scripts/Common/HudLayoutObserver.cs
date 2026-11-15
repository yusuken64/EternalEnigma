using System;
using UnityEngine;

/// <summary>Bridges Unity's layout notification to views that need to reflow their text.</summary>
internal sealed class HudLayoutObserver : MonoBehaviour
{
    internal Action Changed;
    private void OnRectTransformDimensionsChange() => Changed?.Invoke();
}
