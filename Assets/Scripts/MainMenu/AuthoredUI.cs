using System;
using System.Linq;
using UnityEngine;

/// <summary>Resolves authored views, including closed dialogs. Never creates missing UI.</summary>
public static class AuthoredUI
{
    public static T Require<T>(Transform owner = null) where T : Component
    {
        var views = owner != null
            ? owner.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray()
            : UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (views.Length != 1)
            throw new InvalidOperationException($"Expected one authored {typeof(T).Name} in {(owner != null ? owner.gameObject.scene.name : "loaded scenes")}; found {views.Length}.");
        return views[0];
    }
}
