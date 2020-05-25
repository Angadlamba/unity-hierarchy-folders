using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
#endif

namespace UnityHierarchyFolders.Runtime
{
#if UNITY_EDITOR
	/// <summary>
	///     <para>Extension to Components to check if there are no dependencies to itself.</para>
	///     <para>
	///         taken from:
	///         <see cref="!:https://gamedev.stackexchange.com/a/140799">
	///             StackOverflow: Check if a game object's component can be destroyed
	///         </see>
	///     </para>
	/// </summary>
	internal static class CanDestroyExtension
	{
		private static bool Requires(Type obj, Type req) => Attribute.IsDefined(obj, typeof(RequireComponent)) &&
		                                                    Attribute.GetCustomAttributes(obj, typeof(RequireComponent)).OfType<RequireComponent>()
		                                                              // RequireComponent has up to 3 required types per requireComponent, because of course.
		                                                             .SelectMany(rc => new[] { rc.m_Type0, rc.m_Type1, rc.m_Type2 })
		                                                             .Any(t => t != null && t.IsAssignableFrom(req));

		/// <summary>Checks whether the stated component can be destroyed without violating dependencies.</summary>
		/// <returns>Is component destroyable?</returns>
		/// <param name="t">Component candidate for destruction.</param>
		internal static bool CanDestroy(this Component t) => !t.gameObject.GetComponents<Component>().Any(c => Requires(c.GetType(), t.GetType()));
	}
#endif

	[DisallowMultipleComponent]
	[ExecuteAlways]
	public class Folder : MonoBehaviour
	{
		[SerializeField] private bool maintainChildrenWorldPositions = true;

		/// <summary>Takes direct children and links them to the parent transform or global.</summary>
		public void Flatten()
		{
			// gather first-level children
			var index = transform.GetSiblingIndex(); // keep components in logical order
			foreach (var child in transform.GetComponentsInChildren<Transform>(true))
			{
				if (child.parent == transform)
				{
					child.name = $"{name}/{child.name}";
					child.SetParent(transform.parent, maintainChildrenWorldPositions);
					child.SetSiblingIndex(++index);
				}
			}

			if (Application.isPlaying)
				Destroy(gameObject);
			else
				DestroyImmediate(gameObject);
		}

#if UNITY_EDITOR
		[SerializeField] private int colorIndex = 0;

		public List<Transform> DeepChildrenNoFolders { get; private set; } = new List<Transform>();

		/// <summary>The set of Folder objects.</summary>
		public static Dictionary<int, int> folders = new Dictionary<int, int>();

		public int ColorIndex => colorIndex;

		private void Start()
		{
			gameObject.transform.hideFlags = HideFlags.HideInInspector;
			AddFolderData();
			SubscribeToCallbacks();
			ResetTransform();
		}

		private void OnDestroy()
		{
			RemoveFolderData();
			UnsubscribeFromCallbacks();
		}

		private void OnValidate()
		{
			gameObject.transform.hideFlags = HideFlags.HideInInspector;
			AddFolderData();
			EnsureExclusiveComponent();
			ResetTransform();
		}

		private void Reset()
		{
			gameObject.transform.hideFlags = HideFlags.HideInInspector;
			AddFolderData();
			EnsureExclusiveComponent();
			ResetTransform();
		}

		/// <summary>
		///     Gets the icon index associated with the specified object.
		/// </summary>
		/// <param name="obj">Test object.</param>
		/// <param name="index">The icon index.</param>
		/// <returns>True if the specified object is a folder with a registered icon index.</returns>
		public static bool TryGetIconIndex(Object obj, out int index)
		{
			index = -1;
			return obj && folders.TryGetValue(obj.GetInstanceID(), out index);
		}

		public static bool IsFolder(Object obj) => folders.ContainsKey(obj.GetInstanceID());

		private void AddFolderData()
		{
			folders[gameObject.GetInstanceID()] = colorIndex;
			SetDeepChildrenNoFolders();
		}

		private void RemoveFolderData()
		{
			folders.Remove(gameObject.GetInstanceID());
			DeepChildrenNoFolders.Clear();
		}

		private void SubscribeToCallbacks()
		{
			Selection.selectionChanged += OnSelectionChanged;
			EditorApplication.hierarchyChanged += OnHierarchyChanged;
		}

		private void OnSelectionChanged()
		{
			HandleHidingTools();
		}

		private static void HandleHidingTools()
		{
			if (Selection.gameObjects.Any(go => go != null && go.GetComponent<Folder>()))
			{
				Tools.hidden = true;
				return;
			}

			Tools.hidden = false;
		}

		private void UnsubscribeFromCallbacks() => EditorApplication.hierarchyChanged -= OnHierarchyChanged;
		
		private static List<Transform> RecursiveNonFolderChildrenSearch(Transform parent)
		{
			if (parent.childCount == 0)
				return new List<Transform>();

			var children = new List<Transform>();

			foreach (Transform child in parent)
			{
				if (child.gameObject.GetComponent<Folder>())
				{
					foreach (var grandChild in RecursiveNonFolderChildrenSearch(child))
					{
						children.Add(grandChild);
					}
					continue;
				}
				
				children.Add(child);
				
				foreach (var grandChild in RecursiveNonFolderChildrenSearch(child))
				{
					children.Add(grandChild);
				}
			}
			
			return children;
		}
		
		private void ResetTransform()
		{
			if (gameObject.TryGetComponent<RectTransform>(out var rectTransform))
			{
				rectTransform.sizeDelta = Vector2.zero;
				rectTransform.anchorMin = Vector2.zero;
				rectTransform.anchorMax = Vector2.one;
				rectTransform.pivot = new Vector2(0.5f, 0.5f);
				rectTransform.rotation = Quaternion.identity;
				rectTransform.localScale = Vector3.one;
			}
			else
			{
				transform.position = Vector3.zero;
				transform.rotation = Quaternion.identity;
				transform.localScale = new Vector3(1, 1, 1);
			}
		}

		private void OnHierarchyChanged() => SetDeepChildrenNoFolders();
		private void SetDeepChildrenNoFolders()
		{
			if (this == null)
				return;

			DeepChildrenNoFolders.Clear();
			DeepChildrenNoFolders = RecursiveNonFolderChildrenSearch(transform);
		}
		
		private bool AskDelete() => EditorUtility.DisplayDialog("Can't add script",
		                                                        "Folders shouldn't be used with other components. Which component should be kept?",
		                                                        "Folder",
		                                                        "Component");

		/// <summary>Delete all components regardless of dependency hierarchy.</summary>
		/// <param name="comps">Which components to delete.</param>
		private void DeleteComponents(IEnumerable<Component> comps)
		{
			var destroyable = comps.Where(c => c != null && c.CanDestroy());

			// keep cycling through the list of components until all components are gone.
			while (destroyable.Any())
			{
				foreach (var c in destroyable)
					DestroyImmediate(c);
			}
		}

		/// <summary>Ensure that the Folder is the only component.</summary>
		private void EnsureExclusiveComponent()
		{
			var existingComponents = GetComponents<Component>().Where(c => c != this && !typeof(Transform).IsAssignableFrom(c.GetType()));

			// no items means no actions anyways
			if (!existingComponents.Any())
				return;

			if (AskDelete())
				DeleteComponents(existingComponents);
			else
				DestroyImmediate(this);
		}
#endif
	}
}
