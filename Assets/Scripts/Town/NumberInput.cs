using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Digits receive navigation/submit through the shared EventSystem. Cancel always means Back.
public class NumberInput : MonoBehaviour
{
    public Transform Container;
    public SelectableDigit SelectableDigitPrefab;
    internal List<SelectableDigit> SelectableDigits;
    public Button OKButton;

    public void Setup(int places)
    {
        var values = Enumerable.Range(0, Mathf.Clamp(places, 1, 8)).Select(i => (int)Mathf.Pow(10, i)).ToList();
        SelectableDigits = Container.RePopulateObjects(SelectableDigitPrefab, values, (view, value) => {
            view.DisplayedDigit = 0;
            view.Setup(value);
        });
        for (int i = 0; i < SelectableDigits.Count; i++)
            SelectableDigits[i].navigation = new Navigation {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = i + 1 < SelectableDigits.Count ? SelectableDigits[i + 1] : OKButton,
                selectOnRight = i > 0 ? SelectableDigits[i - 1] : OKButton
            };
        OKButton.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnLeft = SelectableDigits[0], selectOnRight = SelectableDigits[^1] };
    }
    internal int GetNumber() => SelectableDigits.Sum(d => d.GetNumber());
}
