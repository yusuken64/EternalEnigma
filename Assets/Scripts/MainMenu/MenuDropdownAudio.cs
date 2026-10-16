using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Native dropdown opening uses the same UI cue as button activation.</summary>
[DisallowMultipleComponent]
public sealed class MenuDropdownAudio : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    private TMP_Dropdown dropdown;

    public static void Ensure(TMP_Dropdown control)
    {
        if (control != null && control.GetComponent<MenuDropdownAudio>() == null)
            control.gameObject.AddComponent<MenuDropdownAudio>();
    }

    private void Awake() => dropdown = GetComponent<TMP_Dropdown>();
    private void Play()
    {
        if (dropdown != null && dropdown.IsActive() && dropdown.IsInteractable() &&
            (MenuUIInputModule.Active == null || MenuUIInputModule.Active.Allows(gameObject)))
            AudioManager.Instance?.PlayCue(SoundCue.Confirm);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) Play();
    }
    public void OnSubmit(BaseEventData eventData) => Play();
}
