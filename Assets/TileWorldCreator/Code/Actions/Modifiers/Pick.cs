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
			_r.PickCount = PickCount;

			return _r;
		}

		public bool[,] Execute(bool[,] map, TileWorldCreator _twc)
		{
			UnityEngine.Random.InitState(_twc.twcAsset.randomSeed);

			// Step 1: Collect all true positions from _fromMap
			List<Vector2Int> truePositions = new List<Vector2Int>();
			int width = map.GetLength(0);
			int height = map.GetLength(1);

			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < height; y++)
				{
					if (map[x, y])
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
	}
}