using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Focusable details control: up/down scroll the text, left returns to the skill.
public sealed class TrainerPreviewScroll : SelectToActivateButton
{
    public ScrollRect Scroll;
    public override void OnMove(AxisEventData eventData)
    {
        if (eventData.moveDir == MoveDirection.Up || eventData.moveDir == MoveDirection.Down)
        {
            Canvas.ForceUpdateCanvases();
            float overflow = Scroll.content.rect.height - Scroll.viewport.rect.height;
            if (overflow > 0)
                Scroll.verticalNormalizedPosition = Mathf.Clamp01(Scroll.verticalNormalizedPosition +
                    (eventData.moveDir == MoveDirection.Up ? 1 : -1) * 100 / overflow);
            eventData.Use();
        }
        else base.OnMove(eventData);
    }
}
