using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Up/down scroll the text; explicit vertical links leave it at either end.
public sealed class TrainerPreviewScroll : SelectToActivateButton
{
    public ScrollRect Scroll;
    public override void OnMove(AxisEventData eventData)
    {
        if (eventData.moveDir == MoveDirection.Up || eventData.moveDir == MoveDirection.Down)
        {
            Canvas.ForceUpdateCanvases();
            float overflow = Scroll.content.rect.height - Scroll.viewport.rect.height;
            bool up=eventData.moveDir==MoveDirection.Up;
            var destination=up?navigation.selectOnUp:navigation.selectOnDown;
            bool atEdge=overflow<=0 || (up?Scroll.verticalNormalizedPosition>=.9999f:Scroll.verticalNormalizedPosition<=.0001f);
            if(atEdge && destination!=null)
            {
                base.OnMove(eventData);
                return;
            }
            if (overflow > 0)
                Scroll.verticalNormalizedPosition = Mathf.Clamp01(Scroll.verticalNormalizedPosition +
                    (eventData.moveDir == MoveDirection.Up ? 1 : -1) * 100 / overflow);
            eventData.Use();
        }
        else base.OnMove(eventData);
    }
}
