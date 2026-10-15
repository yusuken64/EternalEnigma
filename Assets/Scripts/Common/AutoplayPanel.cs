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
    Image inputBlocker;

    public void Bind(AutoplayRunner value)
    {
        runner=value;
        // A paused transition cannot uncover the resume prompt, so playback UI must stay above it.
        GetComponent<Canvas>().sortingOrder=ScreenTransition.SceneOverlayOrder+1;
        inputBlocker=gameObject.AddComponent<Image>();
        inputBlocker.color=Color.clear;
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
        if(inputBlocker!=null)inputBlocker.enabled=visible;
        Expanded.gameObject.SetActive(visible&&!runner.ReturnPromptOpen&&(runner.PanelVisible||runner.TakingControl));
        Collapsed.gameObject.SetActive(visible&&!runner.ReturnPromptOpen&&!runner.PanelVisible&&!runner.TakingControl);
        Prompt.gameObject.SetActive(visible&&runner.ReturnPromptOpen);
        if(!visible)return;
        Title.text="Autoplay";
        Status.text=runner.TakingControl?"Finishing the current action…":runner.Finalizing?"Saving the run report…":
            runner.SessionRunning?(runner.Paused?"Paused":"Exploring the world…"):"Autoplay has ended.";
        Counts.text=$"Run {System.Math.Min(runner.RunNumber,runner.Options.RunCount)} of {runner.Options.RunCount}\n"+
            (runner.IsLootSweep?$"Found {runner.Report?.CollectedLoot??0} of {runner.Report?.ExpectedLoot??0} items. ":$"{runner.Report?.DungeonTurns??0} turns. ")+$"{runner.Report?.Actions??0} actions.";
        Playback.text=$"Playback speed {runner.Options.Speed:0.#}×"+(runner.Paused?" paused":"");
        PauseLabel.text=runner.Paused?"Resume":"Pause";
        AnimationLabel.text="Animations "+DungeonPreferences.SpeedLabel;
        Footer.text=runner.Finalizing?"Saving the run report…":runner.SessionRunning?"Click outside these controls to stop or take control.":
            runner.Report?.ArtifactsComplete==true?"The run report has been saved.":"The run report could not be completed.";
        Pause.interactable=runner.SessionRunning&&!runner.TakingControl;
        Animations.interactable=Pause.interactable&&!runner.IsLootSweep;
        Stop.gameObject.SetActive(runner.SessionRunning);
        Return.interactable=!runner.TakingControl;TakeControl.interactable=runner.CanTakeControl;
        for(int i=0;i<Speeds.Length;i++)Speeds[i].interactable=runner.SessionRunning&&!runner.TakingControl&&!Mathf.Approximately(runner.Options.Speed,Rates[i]);
    }
}
