using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FaceCamDisplay : MonoBehaviour
{
    public LayerMask LayerMask;
    private GameObject currentFollowedObject;
    private int originalLayer;

    internal void SetFollow(GameObject followObject)
    {
        if (currentFollowedObject != null && currentFollowedObject != followObject)
        {
            Unfollow(currentFollowedObject);
        }

        var faceCam = FindFirstObjectByType<FaceCam>();
        if (faceCam != null)
        {
            faceCam.SetFollow(followObject);
        }

        originalLayer = followObject.layer;
        currentFollowedObject = followObject;
        int layer = LayerMaskToLayer(LayerMask);
        Debug.Log($"setting to layer follow {layer}", followObject);
        SetLayerRecursively(followObject, layer);
    }

    internal void Unfollow(GameObject followObject)
    {
        if (currentFollowedObject == followObject)
        {
            Debug.Log($"setting to layer unfollow {originalLayer}", followObject);
            SetLayerRecursively(followObject, originalLayer);
            currentFollowedObject = null;
            
            var faceCam = FindFirstObjectByType<FaceCam>();
            if (faceCam != null)
            {
                faceCam.Unfollow();
            }
        }
        else
        {
            Debug.LogWarning("Trying to unfollow an object that is not currently followed.");
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private int LayerMaskToLayer(LayerMask layerMask)
    {
        int layer = 0;
        int mask = layerMask.value;
        while (mask > 0)
        {
            mask = mask >> 1;
            layer++;
        }
        return layer - 1;
    }
}
