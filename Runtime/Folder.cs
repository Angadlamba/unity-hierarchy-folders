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
			AddFolderData();
			SubscribeToCallbacks();
		}

		private void OnDestroy()
		{
			RemoveFolderData();
			UnsubscribeToCallbacks();
		}

		private void OnValidate()
		{
			AddFolderData();
			EnsureExclusiveComponent();
		}

		private void Reset()
		{
			AddFolderData();
			EnsureExclusiveComponent();
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
			SetChildren();
			SetDeepChildren();
			SetDeepChildrenNoFolders();
		}

		private void RemoveFolderData()
		{
			folders.Remove(gameObject.GetInstanceID());
			_children.Clear();
			_deepChildren.Clear();
			DeepChildrenNoFolders.Clear();
		}

		private void SubscribeToCallbacks()
		{
			Selection.selectionChanged += OnSelectionChanged;
			EditorApplication.hierarchyChanged += OnHierarchyChanged;
		}

		private void UnsubscribeToCallbacks()
		{
			Selection.selectionChanged -= OnSelectionChanged;
			EditorApplication.hierarchyChanged -= OnHierarchyChanged;
		}

		private void OnSelectionChanged()
		{
			if (CheckAFolderIsSelected())
				CenterAtChildrenBoundingBox();
			else
				ResetTransformKeepChildrenInPlace();
		}

		private bool CheckAFolderIsSelected()
		{
			foreach (var go in Selection.gameObjects)
			{
				if (go.GetComponent<Folder>())
					return true;
			}

			return false;
		}

		private bool CheckImSelected()
		{
			if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
				return false;

			return Selection.gameObjects.Contains(gameObject);
		}

		private void CenterAtChildrenBoundingBox()
		{
			if (gameObject.GetComponent<RectTransform>() || transform.childCount == 0)
				return;

			var childrenTransformCenter = GetChildrenTransformCenter(DeepChildrenNoFolders);
			var childrenBoundsCenter = GetChildrenBoundsCenter(childrenTransformCenter);
			SetPositionAndCorrectChildren(childrenBoundsCenter);
		}

		private Vector3 GetChildrenTransformCenter(List<Transform> transforms)
		{
			var transformsCenter = Vector3.zero;

			foreach (var child in transforms)
				transformsCenter += child.position;

			transformsCenter /= _deepChildren.Count;

			return transformsCenter;
		}

		private Vector3 GetChildrenBoundsCenter(Vector3 center)
		{
			var boundsCenter = new Bounds(center, Vector3.one);

			foreach (var child in _deepChildren)
			{
				if (!child.GetComponent<Renderer>())
					continue;

				boundsCenter.Encapsulate(child.GetComponent<Renderer>().bounds);
			}

			return boundsCenter.center;
		}

		private void SetPositionAndCorrectChildren(Vector3 position)
		{
			var previousChildrenPositions = new List<Vector3>();
			var previousChildrenRotations = new List<Quaternion>();
			var previousChildrenScales = new List<Vector3>();

			foreach (var child in _children)
			{
				previousChildrenPositions.Add(child.position);
				previousChildrenRotations.Add(child.rotation);
				previousChildrenScales.Add(child.lossyScale);
			}

			TryRecordUndo();
			transform.position = position;

			for (var i = 0; i < _children.Count; i++)
			{
				_children[i].position = previousChildrenPositions[i];
				_children[i].rotation = previousChildrenRotations[i];
				_children[i].localScale = previousChildrenScales[i];
			}
		}

		//TODO: Does not work well with scaling nested folders
		private void ResetTransformKeepChildrenInPlace()
		{
			if (gameObject.GetComponent<RectTransform>())
				return;

			var previousChildrenPositions = new List<Vector3>();
			var previousChildrenRotations = new List<Quaternion>();
			var previousChildrenScales = new List<Vector3>();

			foreach (var child in _children)
			{
				previousChildrenPositions.Add(child.position);
				previousChildrenRotations.Add(child.rotation);
				previousChildrenScales.Add(child.lossyScale);
			}

			TryRecordUndo();
			ResetTransform();

			for (var i = 0; i < _children.Count; i++)
			{
				_children[i].position = previousChildrenPositions[i];
				_children[i].rotation = previousChildrenRotations[i];
				_children[i].localScale = previousChildrenScales[i];
			}
		}

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
			}

			return children;
		}

		private void TryRecordUndo()
		{
			if (gameObject.GetComponent<RectTransform>())
				return;

			var currentGroup = Undo.GetCurrentGroup();
			Undo.RegisterCompleteObjectUndo(transform, "Custom Undo");

			foreach (Transform child in _children)
				Undo.RegisterCompleteObjectUndo(child, "Custom Undo");

			Undo.CollapseUndoOperations(currentGroup);
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

		private void OnHierarchyChanged()
		{
			SetChildren();
			SetDeepChildren();
			SetDeepChildrenNoFolders();
		}

		private void SetChildren()
		{
			if (this == null)
				return;

			_children.Clear();
			foreach (Transform child in transform)
				_children.Add(child);
		}

		private void SetDeepChildren()
		{
			if (this == null)
				return;

			_deepChildren.Clear();
			_deepChildren = RecursiveChildrenSearch(transform);
		}

		private static List<Transform> RecursiveChildrenSearch(Transform parent)
		{
			if (parent.childCount <= 0)
				return new List<Transform>();

			var children = new List<Transform>();

			foreach (Transform child in parent)
			{
				children.Add(child);

				if (child.childCount <= 0)
					continue;

				foreach (var grandChild in RecursiveChildrenSearch(child))
					children.Add(grandChild);
			}

			return children;
		}

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

		private List<Transform> _children = new List<Transform>();
		private List<Transform> _deepChildren = new List<Transform>();
#endif
	}
}
