#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
#endif
using UnityEngine;

namespace UnityHierarchyFolders.Runtime
{
#if UNITY_EDITOR
	/// <summary>
	/// <para>Extension to Components to check if there are no dependencies to itself.</para>
	/// <para>
	///     taken from:
	///     <see cref="!:https://gamedev.stackexchange.com/a/140799">
	///         StackOverflow: Check if a game object's component can be destroyed
	///     </see>
	/// </para>
	/// </summary>
	internal static class CanDestroyExtension
	{
		private static bool Requires(Type obj, Type req) => Attribute.IsDefined(obj, typeof(RequireComponent)) &&
			Attribute.GetCustomAttributes(obj, typeof(RequireComponent)).OfType<RequireComponent>()
			// RequireComponent has up to 3 required types per requireComponent, because of course.
			.SelectMany(rc => new Type[] { rc.m_Type0, rc.m_Type1, rc.m_Type2 }).Any(t => t != null && t.IsAssignableFrom(req));

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
		[SerializeField] private bool _maintainChildrenWorldPositions = true;

#if UNITY_EDITOR
		[SerializeField] private int _colorIndex = 0;

		/// <summary>The set of Folder objects.</summary>
		public static Dictionary<int, int> Folders = new Dictionary<int, int>();

		public int ColorIndex => _colorIndex;

		private void Start()
		{
			AddFolderData();
			ResetTransform();
			SetAllChildrenAndCenter();
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
		/// Gets the icon index associated with the specified object.
		/// </summary>
		/// <param name="obj">Test object.</param>
		/// <param name="index">The icon index.</param>
		/// <returns>True if the specified object is a folder with a registered icon index.</returns>
		public static bool TryGetIconIndex(UnityEngine.Object obj, out int index)
		{
			index = -1;
			return obj && Folders.TryGetValue(obj.GetInstanceID(), out index);
		}

		public static bool IsFolder(UnityEngine.Object obj) => Folders.ContainsKey(obj.GetInstanceID());

		private void SubscribeToCallbacks()
		{
			Selection.selectionChanged += HandleSelection;
			EditorApplication.hierarchyChanged += SetAllChildrenAndCenter;
			SceneView.duringSceneGui += GUIEvents;
		}

		private void UnsubscribeToCallbacks()
		{
			Selection.selectionChanged -= HandleSelection;
			EditorApplication.hierarchyChanged -= SetAllChildrenAndCenter;
			SceneView.duringSceneGui -= GUIEvents;
		}

		private void HandleSelection()
		{
			if (Selection.gameObjects.Contains(gameObject))
				_imSelected = true;
			else
				_imSelected = false;

			foreach (var child in _allChildren)
			{
				if (Selection.gameObjects.Contains(child.gameObject))
				{
					_aChildOfMineIsSelected = true;
					break;
				}
				else
					_aChildOfMineIsSelected = false;
			}

			_selectedChildren.Clear();
			foreach (var child in _allChildren)
			{
				if (Selection.gameObjects.Contains(child.gameObject))
					_selectedChildren.Add(child);
			}
		}

		private void GUIEvents(SceneView sceneView)
		{
			Event GUIEvent = Event.current;

			if (GUIEvent.type == EventType.MouseDown && Event.current.button == 0)
			{
				if (!gameObject.GetComponent<RectTransform>())
					if (_imSelected || _aChildOfMineIsSelected)
						RecordUndo();
			}

			if (GUIEvent.type == EventType.MouseUp && Event.current.button == 0)
			{
				Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
				UpdatePositionBasedOnChildrenBoundsCenter();
			}
		}

		private void RecordUndo()
		{
			Undo.RegisterCompleteObjectUndo(transform, "Custom Undo");

			if (!_aChildOfMineIsSelected)
				return;

			foreach (var child in _selectedChildren)
				Undo.RegisterCompleteObjectUndo(child, "Custom Undo");

			foreach (Transform child in transform)
				Undo.RegisterCompleteObjectUndo(child, "Custom Undo");
		}

		private void AddFolderData() => Folders[this.gameObject.GetInstanceID()] = this._colorIndex;

		private void RemoveFolderData() => Folders.Remove(this.gameObject.GetInstanceID());

		private void ResetTransform()
		{
			if (transform.childCount > 0)
				return;

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

		private void UpdatePositionBasedOnChildrenBoundsCenter()
		{
			if (_allChildren == null || _allChildren.Count <= 0)
				return;

			if (Selection.activeGameObject == null)
				return;

			if (!_allChildren.Contains(Selection.activeGameObject.transform))
				return;

			/* if (!(Tools.current == Tool.Move || Tools.current == Tool.Scale))
				return; */

			CenterAtChildrenBoundingBox();
		}

		private void SetAllChildrenAndCenter()
		{
			SetAllChildren();
			CenterAtChildrenBoundingBox();
		}

		private void SetAllChildren()
		{
			_allChildren.Clear();
			_allChildren = RecursiveChildrenSearch(transform);
		}

		private List<Transform> RecursiveChildrenSearch(Transform parent)
		{
			if (parent.childCount <= 0)
				return new List<Transform>();

			List<Transform> children = new List<Transform>();

			foreach (Transform child in parent)
			{
				children.Add(child);

				if (child.childCount > 0)
				{
					foreach (var grandChild in RecursiveChildrenSearch(child))
						children.Add(grandChild);
				}
			}

			return children;
		}

		private void CenterAtChildrenBoundingBox()
		{
			if (gameObject.GetComponent<RectTransform>())
				return;

			if (transform.childCount <= 0)
				return;

			var childrenTransformCenter = GetChildrenTransformCenter(_allChildren);
			var childrenBoundscenter = GetChildrenBoundsCenter(childrenTransformCenter);
			SetPositionAndCorrectChildren(childrenBoundscenter);
		}

		private Vector3 GetChildrenTransformCenter(List<Transform> transforms)
		{
			var transformsCenter = Vector3.zero;

			foreach (var child in _allChildren)
				transformsCenter += child.position;

			transformsCenter /= _allChildren.Count;

			return transformsCenter;
		}

		private Vector3 GetChildrenBoundsCenter(Vector3 center)
		{
			var boundsCenter = new Bounds(center, Vector3.one);

			foreach (var child in _allChildren)
			{
				if (!child.GetComponent<Renderer>())
					continue;

				boundsCenter.Encapsulate(child.GetComponent<Renderer>().bounds);
			}

			return boundsCenter.center;
		}

		private void SetPositionAndCorrectChildren(Vector3 position)
		{

			var previousPosition = transform.position;
			transform.position = position;
			var positionChange = transform.position - previousPosition;

			foreach (Transform child in transform)
				child.transform.position -= positionChange;
		}

		private bool AskDelete() => EditorUtility.DisplayDialog(
			title: "Can't add script",
			message: "Folders shouldn't be used with other components. Which component should be kept?",
			ok: "Folder",
			cancel: "Component"
		);

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
			var existingComponents = this.GetComponents<Component>().Where(c => c != this && !typeof(Transform).IsAssignableFrom(c.GetType()));

			// no items means no actions anyways
			if (!existingComponents.Any())
				return;

			if (this.AskDelete())
				this.DeleteComponents(existingComponents);
			else
				DestroyImmediate(this);
		}

		private bool _imSelected = false;
		private bool _aChildOfMineIsSelected = false;
		private List<Transform> _selectedChildren = new List<Transform>();
		private List<Transform> _allChildren = new List<Transform>();
#endif
		/// <summary>Takes direct children and links them to the parent transform or global.</summary>
		public void Flatten()
		{
			// gather first-level children
			int index = this.transform.GetSiblingIndex(); // keep components in logical order
			foreach (var child in this.transform.GetComponentsInChildren<Transform>(includeInactive: true))
			{
				if (child.parent == this.transform)
				{
					child.name = $"{this.name}/{child.name}";
					child.SetParent(this.transform.parent, _maintainChildrenWorldPositions);
					child.SetSiblingIndex(++index);
				}
			}

			if (Application.isPlaying)
				Destroy(this.gameObject);
			else
				DestroyImmediate(this.gameObject);
		}
	}
}
