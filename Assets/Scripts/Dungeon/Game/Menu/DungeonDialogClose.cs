using JuicyChickenGames.Menu;
using UnityEngine;
using UnityEngine.UI;

public sealed class DungeonDialogClose : MonoBehaviour
{
    private void Awake() => GetComponent<Button>().onClick.AddListener(() => GetComponentInParent<Dialog>().CloseDialog());
}
