using JuicyChickenGames.Menu;
using UnityEngine;
using UnityEngine.UI;
public sealed class HomeBedDialog : Dialog
{
    [SerializeField] private Button sleep;
    [SerializeField] private Button back;
    #if UNITY_EDITOR
    public static HomeBedDialog AuthorLayout(Transform parent)
    {
        var panel = GameUISkin.Panel(parent,new Vector2(.3f,.3f),new Vector2(.7f,.7f));
        var dialog = panel.gameObject.AddComponent<HomeBedDialog>();
        GameUISkin.Label(panel.transform,"Home",new Vector2(.1f,.7f),new Vector2(.9f,.94f),34);
        dialog.sleep = GameUISkin.Button(panel.transform,"Sleep and Save",new Vector2(.1f,.4f),new Vector2(.9f,.6f), () => {
            dialog.CloseDialog(); Object.FindFirstObjectByType<HomeBed>().SleepAndSave(); });
        dialog.back=GameUISkin.Button(panel.transform,"Back",new Vector2(.1f,.1f),new Vector2(.9f,.3f),dialog.CloseDialog);
        panel.gameObject.SetActive(false); return dialog;
    }
#endif
    public static HomeBedDialog Create(Transform parent)=>AuthoredUI.Require<HomeBedDialog>(parent);
    private void Awake()
    {
        sleep.onClick.AddListener(()=> {CloseDialog(); Object.FindFirstObjectByType<HomeBed>().SleepAndSave();});
        back.onClick.AddListener(CloseDialog);
    }
    internal override void SetFirstSelect() => sleep.Select();
}
