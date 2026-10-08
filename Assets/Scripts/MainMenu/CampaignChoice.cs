using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CampaignChoice : MonoBehaviour
{
    public TMP_Text Prompt;
    public Button Confirm, Back;
    private bool open;
    public static void Show(string prompt,string confirm,Action action,Action cancel=null)
    {
        var choice=AuthoredUI.Require<CampaignChoice>();
        if(choice.open)return;
        choice.open=true;choice.gameObject.SetActive(true);choice.Prompt.text=prompt;
        choice.Confirm.GetComponentInChildren<TMP_Text>().text=confirm;
        void Close(Action callback)
        {
            if(!choice.open)return;choice.open=false;
            MenuUIInputModule.Active?.PopDialog(choice);choice.gameObject.SetActive(false);callback?.Invoke();
        }
        choice.Confirm.onClick.RemoveAllListeners();choice.Back.onClick.RemoveAllListeners();
        choice.Confirm.onClick.AddListener(()=>Close(action));choice.Back.onClick.AddListener(()=>Close(cancel));
        // The authored buttons can have no automatic navigation path when this
        // confirmation is opened over the slot picker.
        choice.Confirm.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnLeft = choice.Back, selectOnRight = choice.Back,
            selectOnUp = choice.Back, selectOnDown = choice.Back };
        choice.Back.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnLeft = choice.Confirm, selectOnRight = choice.Confirm,
            selectOnUp = choice.Confirm, selectOnDown = choice.Confirm };
        MenuUIInputModule.Active?.PushDialog(choice,choice.transform,choice.Back.gameObject,()=>Close(cancel));choice.Back.Select();
    }
}
