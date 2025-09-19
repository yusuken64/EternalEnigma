using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ActionStep), true)]
public class ActionStepDrawer : PropertyDrawer
{
	static Dictionary<string, Type> typeMap;

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		if (typeMap == null)
		{
			typeMap = DrawerUtility.BuildTypeMap<ActionStep>();
		}

		var typeRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
		var contentRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, position.height - EditorGUIUtility.singleLineHeight);

		EditorGUI.BeginProperty(position, label, property);
		var typeName = property.managedReferenceFullTypename;
		var displayName = DrawerUtility.GetShortTypeName(typeName);

		if (EditorGUI.DropdownButton(typeRect, new GUIContent(displayName ?? "Select Effect Type"), FocusType.Keyboard))
		{
			var menu = new GenericMenu();
			if (typeMap == null || typeMap.Count == 0)
			{
				menu.AddDisabledItem(new GUIContent("No Ability Effects available"));
				menu.ShowAsContext();
				return;
			}

			foreach (var kvp in typeMap)
			{
				var name = kvp.Key;
				var type = kvp.Value;
				menu.AddItem(new GUIContent(name), type.FullName == typeName, () =>
				{
					property.managedReferenceValue = Activator.CreateInstance(type);
					property.serializedObject.ApplyModifiedProperties();
				});
			}

			menu.ShowAsContext();
		}

		if (property.managedReferenceValue != null)
		{
			EditorGUI.indentLevel++;
			EditorGUI.PropertyField(contentRect, property, GUIContent.none, true);
			EditorGUI.indentLevel--;
		}

		EditorGUI.EndProperty();
	}

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		return EditorGUI.GetPropertyHeight(property, label, true) + EditorGUIUtility.singleLineHeight;
	}
}
