using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored GameUISkin controls for the isolated watch-demo session.</summary>
public sealed class AutoplayPanel : MonoBehaviour
{
    public RectTransform Expanded,Collapsed,Prompt;
    public TMP_Text Title,Status,Counts,Playback,Footer,PauseLabel,AnimationLabel;
    public Button Hide,Show,Pause,Animations,Stop,Return,TakeControl,Menu,KeepWatching;
    public Button[] Speeds;
    public static readonly float[] Rates={.5f,1,2,4,8,16,32};
    AutoplayRunner runner;

    public void Bind(AutoplayRunner value)
    {
        runner=value;
        Hide.onClick.AddListener(()=>runner.SetPanelVisible(false));
        Show.onClick.AddListener(()=>runner.SetPanelVisible(true));
        Pause.onClick.AddListener(()=>runner.SetPaused(!runner.Paused));
        Animations.onClick.AddListener(()=>DungeonPreferences.AnimationMode=(DungeonAnimationMode)(((int)DungeonPreferences.AnimationMode+1)%4));
        Stop.onClick.AddListener(runner.Stop);
        Return.onClick.AddListener(runner.RequestReturn);
        TakeControl.onClick.AddListener(runner.TakeControl);
        Menu.onClick.AddListener(()=>runner.ConfirmReturn(true));
        KeepWatching.onClick.AddListener(()=>runner.ConfirmReturn(false));
        for(int i=0;i<Speeds.Length;i++){float rate=Rates[i];Speeds[i].onClick.AddListener(()=>runner.SetSpeed(rate));}
        Refresh();
    }

    public bool Contains(Vector2 point)
    {
        var rect=Prompt.gameObject.activeInHierarchy?Prompt:runner.PanelVisible?Expanded:Collapsed;
        return rect.gameObject.activeInHierarchy&&RectTransformUtility.RectangleContainsScreenPoint(rect,point);
    }

    void Update()=>Refresh();
    public void Refresh()
    {
        bool visible=runner!=null&&runner.ShowPlaybackUI;
        Expanded.gameObject.SetActive(visible&&!runner.ReturnPromptOpen&&(runner.PanelVisible||runner.TakingControl));
        Collapsed.gameObject.SetActive(visible&&!runner.ReturnPromptOpen&&!runner.PanelVisible&&!runner.TakingControl);
        Prompt.gameObject.SetActive(visible&&runner.ReturnPromptOpen);
        if(!visible)return;
        Title.text=runner.Options.DebugPlaythrough?"Autoplay · Debug":"Autoplay";
        Status.text=runner.TakingControl?"Finishing current action…":runner.Status;
        Counts.text=$"Actions: {runner.Report?.Actions??0}   Turns: {runner.Report?.DungeonTurns??0}";
        Playback.text=$"Playback: {runner.Options.Speed:0.#}×"+(runner.Paused?" (paused)":"");
        PauseLabel.text=runner.Paused?"Resume":"Pause";
        AnimationLabel.text="Animations: "+DungeonPreferences.SpeedLabel;
        Footer.text=runner.Running?"Input outside the controls opens stop options.":"Playthrough report saved.";
        Pause.interactable=Animations.interactable=runner.Running&&!runner.TakingControl;
        Stop.gameObject.SetActive(!runner.AppSession&&runner.Running);
        Return.interactable=!runner.TakingControl;TakeControl.interactable=runner.CanTakeControl;
        for(int i=0;i<Speeds.Length;i++)Speeds[i].interactable=runner.Running&&!runner.TakingControl&&!Mathf.Approximately(runner.Options.Speed,Rates[i]);
    }
}
