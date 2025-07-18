using System;

using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

using TWC.editor;

namespace TWC.Actions
{
	[ActionCategoryAttribute(Category = ActionCategoryAttribute.CategoryTypes.Modifiers)]
	[ActionNameAttribute(Name = "Pick")]
	public class Pick : TWCBlueprintAction, ITWCAction
	{
		public int selectedLayerIndex;
		public Guid guidCopyLayer;
		public int PickCount;

		private TWCGUILayout guiLayout;

		public class GenericMenuData
		{
			public int selectedIndex;
			public TileWorldCreator twc;
		}

		public ITWCAction Clone()
		{
			var _r = new Pick();

			_r.selectedLayerIndex = this.selectedLayerIndex;
			_r.guidCopyLayer = this.guidCopyLayer;

			return _r;
		}

		public bool[,] Execute(bool[,] map, TileWorldCreator _twc)
		{
			UnityEngine.Random.InitState(_twc.twcAsset.randomSeed);
			var _fromMap = _twc.GetMapOutputFromBlueprintLayer(guidCopyLayer);

			if (_fromMap == null)
			{
				Debug.LogWarning("TileWorldCreator: Add modifier - Layer not assigned");
				return map;
			}

			// Step 1: Collect all true positions from _fromMap
			List<Vector2Int> truePositions = new List<Vector2Int>();
			int width = _fromMap.GetLength(0);
			int height = _fromMap.GetLength(1);

			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < height; y++)
				{
					if (_fromMap[x, y])
					{
						truePositions.Add(new Vector2Int(x, y));
					}

					// Step 2: Clear the output map entirely
					map[x, y] = false;
				}
			}

			// Step 3: Shuffle the list of true positions
			int count = Mathf.Min(PickCount, truePositions.Count);
			for (int i = 0; i < truePositions.Count; i++)
			{
				int swapIndex = UnityEngine.Random.Range(i, truePositions.Count);
				(truePositions[i], truePositions[swapIndex]) = (truePositions[swapIndex], truePositions[i]);
			}

			// Step 4: Set the first N positions to true
			for (int i = 0; i < count; i++)
			{
				var pos = truePositions[i];
				map[pos.x, pos.y] = true;
			}

			return map;
		}

#if UNITY_EDITOR
		public override void DrawGUI(Rect _rect, int _layerIndex, TileWorldCreatorAsset _asset, TileWorldCreator _twc)
		{
			using (guiLayout = new TWCGUILayout(_rect))
			{
				var _names = EditorUtilities.GetAllGenerationLayerNames(_asset);
				var _layerName = "";
				var _layerData = _asset.GetBlueprintLayerData(guidCopyLayer);
				if (_layerData != null)
				{
					_layerName = _layerData.layerName;
				}

				guiLayout.Add();
				if (EditorGUI.DropdownButton(guiLayout.rect, new GUIContent(_layerName), FocusType.Keyboard))
				{
					GenericMenu menu = new GenericMenu();

					for (int n = 0; n < _names.Length; n++)
					{
						var _data = new GenericMenuData();
						_data.selectedIndex = n;

						if (_twc != null)
						{
							_data.twc = _twc;
						}

						menu.AddItem(new GUIContent(_names[n]), false, AssignLayer, _data);
					}

					menu.ShowAsContext();
				}

				// --- New: Integer field for PickCount ---
				guiLayout.Add();
				PickCount = EditorGUI.IntField(guiLayout.rect, new GUIContent("Pick Count"), PickCount);
				// Clamp if necessary
				PickCount = Mathf.Max(0, PickCount); // optional: avoid negatives
			}
		}
#endif

		public float GetGUIHeight()
		{
			if (guiLayout != null)
			{
				return guiLayout.height;
			}
			else
			{
				return 18;
			}
		}


		void AssignLayer(object _data)
		{
			var _d = _data as GenericMenuData;
			guidCopyLayer = _d.twc.twcAsset.mapBlueprintLayers[_d.selectedIndex].guid;
		}
	}
}