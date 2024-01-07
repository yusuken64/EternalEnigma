using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JuicyChickenGames.Menu
{
	public abstract class Dialog : MonoBehaviour
	{
		public ScrollRect scrollView;
		private Selectable savedSelectable;

		internal void Submit()
		{
			EventSystem.current.currentSelectedGameObject.GetComponent<Button>()?.onClick.Invoke();
		}

		internal abstract void SetFirstSelect();
		internal void RestoreSelect()
		{
			savedSelectable.Select();
		}

		internal void SaveSelection()
		{
			savedSelectable = EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>();
		}

		////TODO move logic to somewhere else
		//private void ScrollToSelected()
		//{
		//	if (selectables.Count == 0 || scrollView == null)
		//		return;

		//	// Get the selected item's RectTransform
		//	RectTransform selectedRectTransform = selectables[selectedIndex].GetComponent<RectTransform>();

		//	// Calculate the position of the selected item within the ScrollView's content
		//	Vector2 selectedAnchoredPosition = selectedRectTransform.anchoredPosition;
		//	Vector2 contentSize = scrollView.content.sizeDelta;

		//	// Calculate normalized position within ScrollView content
		//	float normalizedX = selectedAnchoredPosition.x / contentSize.x;
		//	float normalizedY = selectedAnchoredPosition.y / contentSize.y;

		//	// Set the ScrollView's normalized position based on the selected item's position
		//	scrollView.normalizedPosition = new Vector2(normalizedX, normalizedY);
		//}
	}
}