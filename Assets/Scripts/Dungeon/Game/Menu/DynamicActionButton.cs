using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JuicyChickenGames.Menu
{
	public class DynamicActionButton : MonoBehaviour
	{
		public TextMeshProUGUI ActionText;
		public Button Button;
		public Image SkillIcon;
		internal DynamicActionInfo _data;

		internal void Setup(DynamicActionInfo data)
		{
			this._data = data;
			
			ActionText.text = _data.ActionName + (string.IsNullOrEmpty(data.Description) ? "" : "\n<size=75%>" + data.Description + "</size>");
			SkillIcon = SkillIconView.Bind(SkillIcon, ActionText, _data.Icon);
		}

		internal void SizeDescription()
		{
			if (string.IsNullOrEmpty(_data?.Description)) return;
			var rect = (RectTransform)transform;
			float width = Mathf.Max(160, ActionText.rectTransform.rect.width);
			float height = Mathf.Max(64, ActionText.GetPreferredValues(ActionText.text, width, 0).y + 24);
			var layout = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
			layout.preferredHeight = height;
			rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
		}

		public void ActionButton_Clicked()
		{
			_data.ClickAction?.Invoke();
		}
	}
}
