using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;

public sealed class CampaignSlotView : MonoBehaviour, ISelectHandler
{
    public Button Button;
    public TMP_Text Label;
    public RawImage Artwork;
    public Image[] Portraits;
    public Action Selected;
    public void OnSelect(BaseEventData eventData) => Selected?.Invoke();
}
