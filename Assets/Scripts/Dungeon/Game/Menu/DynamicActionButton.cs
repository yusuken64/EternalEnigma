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
			
			ActionText.text = _data.ActionName;
			SkillIcon = SkillIconView.Bind(SkillIcon, ActionText, _data.Icon);
		}

		public void ActionButton_Clicked()
		{
			_data.ClickAction?.Invoke();
		}
	}
}
