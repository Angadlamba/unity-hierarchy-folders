﻿#if UNITY_2019_1_OR_NEWER
using UnityEditor;
using UnityEngine;
using UnityHierarchyFolders.Runtime;

namespace UnityHierarchyFolders.Editor
{
	[CustomEditor(typeof(Folder))]
	public class FolderEditor : UnityEditor.Editor
	{
		private SerializedProperty _worldPositionStays;
		private SerializedProperty _isLocked;

		public override bool RequiresConstantRepaint() => true;

		public override void OnInspectorGUI()
		{
			RenderColorPicker();
			SetMaintainChildrenWorldPositions();
		}

		private void SetMaintainChildrenWorldPositions()
		{
			_worldPositionStays = serializedObject.FindProperty("maintainChildrenWorldPositions");
			EditorGUILayout.PropertyField(_worldPositionStays);
			serializedObject.ApplyModifiedProperties();
		}
		
		private void RenderColorPicker()
		{
			var colorIndexProperty = this.serializedObject.FindProperty("colorIndex");

			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();

			float buttonSize = 25f;

			var gridRect = EditorGUILayout.GetControlRect(false, buttonSize * HierarchyFolderIcon.IconRowCount,
				GUILayout.Width(buttonSize * HierarchyFolderIcon.IconColumnCount));

			int currentIndex = colorIndexProperty.intValue;
			for (int row = 0; row < HierarchyFolderIcon.IconRowCount; row++)
			{
				for (int column = 0; column < HierarchyFolderIcon.IconColumnCount; column++)
				{
					int index = 1 + column + row * HierarchyFolderIcon.IconColumnCount;
					float width = gridRect.width / HierarchyFolderIcon.IconColumnCount;
					float height = gridRect.height / HierarchyFolderIcon.IconRowCount;
					var rect = new Rect(gridRect.x + width * column, gridRect.y + height * row, width, height);
					(var openIcon, var closeIcon) = HierarchyFolderIcon.ColoredFolderIcons(index);

					if (Event.current.type == EventType.Repaint)
					{
						if (index == currentIndex)
						{
							GUIStyle hover = "TV Selection";
							hover.Draw(rect, false, false, false, false);
						}
						else if (rect.Contains(Event.current.mousePosition))
						{
							GUI.backgroundColor = new Color(.7f, .7f, .7f, 1f);
							GUIStyle white = "WhiteBackground";
							white.Draw(rect, false, false, true, false);
							GUI.backgroundColor = Color.white;
						}
					}

					if (GUI.Button(rect, currentIndex == index ? openIcon : closeIcon, EditorStyles.label))
					{
						Undo.RecordObject(this.target, "Set Folder Color");
						colorIndexProperty.intValue = currentIndex == index ? 0 : index;
						this.serializedObject.ApplyModifiedProperties();
						EditorApplication.RepaintHierarchyWindow();
						GUIUtility.ExitGUI();
					}
				}
			}

			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();

			GUILayout.Space(10f);
		}
	}
}
#endif
