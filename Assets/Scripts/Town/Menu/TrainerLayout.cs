using TMPro;
using UnityEngine;
using UnityEngine.UI;

// References are authored in the prefab; backgrounds remain nested shared prefabs.
public sealed class TrainerLayout : MonoBehaviour
{
    public TextMeshProUGUI Balance, Preview;
    public Button PrimaryTab, SecondaryTab, Close;
    public TrainerPreviewScroll PreviewControl;
    public ScrollRect ListScroll, PreviewScroll;
    public SkillGridItem SkillTemplate;
    public TextMeshProUGUI HeadingTemplate;
}
