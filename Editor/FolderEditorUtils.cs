using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityHierarchyFolders.Runtime;

namespace UnityHierarchyFolders.Editor
{
	public static class FolderEditorUtils
	{
		private const string _actionName = "Create Hierarchy Folder";

		/// <summary>Add new folder "prefab".</summary>
		/// <param name="command">Menu command information.</param>
		[MenuItem("GameObject/" + _actionName, false, 0)]
		public static void AddFolderPrefab(MenuCommand command)
		{
			var obj = new GameObject { name = "Folder" };
			obj.AddComponent<Folder>();

			GameObjectUtility.SetParentAndAlign(obj, (GameObject) command.context);

			if (obj.transform.parent != null && obj.transform.parent.GetComponent<RectTransform>())
				obj.AddComponent<RectTransform>();

			Undo.RegisterCreatedObjectUndo(obj, _actionName);
		}

		[MenuItem("GameObject/Folder/Select Deep Children", false,0)]
		public static void SelectDeepChildrenNoFolders(MenuCommand command)
		{
			var go = (GameObject)command.context;
			if (go != null && go.TryGetComponent<Folder>(out var folder))
			{
				List<GameObject> deepChildrenGos = new List<GameObject>();
				foreach (var child in folder.DeepChildrenNoFolders)
					deepChildrenGos.Add(child.gameObject);
				
				Selection.objects = deepChildrenGos.ToArray();
			}
		}
	}

	public class FolderOnBuild : IProcessSceneWithReport
	{
		public int callbackOrder => 0;

		public void OnProcessScene(Scene scene, BuildReport report)
		{
			foreach (var folder in Object.FindObjectsOfType<Folder>())
			{
				folder.Flatten();
			}
		}
	}
}
