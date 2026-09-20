using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JuicyChickenGames.Menu
{
    public abstract class Dialog : MonoBehaviour
    {
        public ScrollRect scrollView;
        private Selectable savedSelectable;
        private Coroutine scrollAnimation;
        public Action CloseAction { get; internal set; }
        public DialogController Owner { get; internal set; }
        public void CloseDialog() => Owner?.Close(this);
        public virtual void PrepareTown(TownInteractionContext context) { }

        internal abstract void SetFirstSelect();
        internal void RestoreSelect()
        {
            if (savedSelectable != null && MenuUIInputModule.IsUsable(savedSelectable.gameObject))
                savedSelectable.Select();
            else SetFirstSelect();
            MenuUIInputModule.Active?.RestoreFocus();
        }

        internal void SaveSelection() => savedSelectable =
            EventSystem.current?.currentSelectedGameObject?.GetComponent<Selectable>();

        public void StopAutoScroll()
        {
            if (scrollAnimation != null) StopCoroutine(scrollAnimation);
            scrollAnimation = null;
        }

        public void ScrollToSelected(GameObject selectedItem)
        {
            StopAutoScroll();
            if (scrollView == null || scrollView.content == null || selectedItem == null) return;
            var viewport = scrollView.viewport != null ? scrollView.viewport : (RectTransform)scrollView.transform;
            var selected = selectedItem.GetComponent<RectTransform>();
            if (selected == null || !selected.IsChildOf(scrollView.content)) return;
            Canvas.ForceUpdateCanvases();
            var row = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selected);
            var content = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, scrollView.content);
            var view = viewport.rect;
            var target = scrollView.normalizedPosition;
            float dx = row.min.x < view.xMin ? view.xMin - row.min.x : row.max.x > view.xMax ? view.xMax - row.max.x : 0;
            float dy = row.max.y > view.yMax ? view.yMax - row.max.y : row.min.y < view.yMin ? view.yMin - row.min.y : 0;
            if (scrollView.horizontal && content.size.x > view.width)
                target.x = Mathf.Clamp01(target.x - dx / (content.size.x - view.width));
            if (scrollView.vertical && content.size.y > view.height)
                target.y = Mathf.Clamp01(target.y - dy / (content.size.y - view.height));
            if ((target - scrollView.normalizedPosition).sqrMagnitude < 0.000001f) return;
            scrollView.StopMovement();
            scrollAnimation = StartCoroutine(ScrollIntoView(target));
        }

        private IEnumerator ScrollIntoView(Vector2 target)
        {
            Vector2 start = scrollView.normalizedPosition;
            float elapsed = 0;
            const float duration = 0.08f;
            while (elapsed < duration)
            {
                var module = MenuUIInputModule.Active;
                if (module != null && (module.UI.Click.IsPressed() || module.UI.ScrollWheel.ReadValue<Vector2>() != Vector2.zero))
                    break;
                elapsed += Time.unscaledDeltaTime;
                scrollView.normalizedPosition = Vector2.Lerp(start, target, Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            scrollAnimation = null;
        }
    }
}
