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
		[SerializeField] private bool _maintainChildrenWorldPositions = true;

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
					child.SetParent(transform.parent, _maintainChildrenWorldPositions);
					child.SetSiblingIndex(++index);
				}
			}

			if (Application.isPlaying)
				Destroy(gameObject);
			else
				DestroyImmediate(gameObject);
		}

#if UNITY_EDITOR
		[SerializeField] private bool _isLocked = false;
		[SerializeField] private int _colorIndex = 0;

		/// <summary>The set of Folder objects.</summary>
		public static Dictionary<int, int> folders = new Dictionary<int, int>();

		public bool IsLocked => _isLocked;
		public int ColorIndex => _colorIndex;

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
			HandleFolderLocking();
			HandleSubscribeToEditorUpdate();
		}

		private void Reset()
		{
			AddFolderData();
			EnsureExclusiveComponent();
			HandleFolderLocking();
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

		private void SubscribeToCallbacks()
		{
			Selection.selectionChanged += OnSelectionChanged;
			EditorApplication.hierarchyChanged += OnHierarchyChanged;
			SceneView.duringSceneGui += GuiEvents;
		}

		private void UnsubscribeToCallbacks()
		{
			Selection.selectionChanged -= OnSelectionChanged;
			EditorApplication.hierarchyChanged -= OnHierarchyChanged;
			SceneView.duringSceneGui -= GuiEvents;
			EditorApplication.update -= ResetTransform;
		}

		private void OnSelectionChanged()
		{
			if (Selection.activeGameObject == null || Selection.gameObjects == null || Selection.gameObjects.Length <= 0)
				return;

			HandleSubscribeToEditorUpdate();
			HandleHidingTools();
			HandleImSelected();
			HandleChildIsSelected();
			SetSelectedChildren();
		}

		private void HandleSubscribeToEditorUpdate()
		{
			if (transform.parent == null)
				return;

			foreach (var parent in _deepParents)
			{
				if (Selection.gameObjects.Contains(parent.gameObject) && _isLocked)
					EditorApplication.update += ResetTransform;
				else
					EditorApplication.update -= ResetTransform;
				return;
			}

			EditorApplication.update -= ResetTransform;
		}

		private static void HandleHidingTools()
		{
			foreach (var go in Selection.gameObjects)
			{
				if (go.TryGetComponent<Folder>(out var folder) && folder.IsLocked)
				{
					Tools.hidden = true;
					return;
				}
			}

			Tools.hidden = false;
		}

		private void HandleImSelected()
		{
			if (Selection.gameObjects.Contains(gameObject))
				_imSelected = true;
			else
				_imSelected = false;
		}
		
		private void HandleChildIsSelected()
		{
			foreach (var child in _deepChildren)
			{
				if (Selection.gameObjects.Contains(child.gameObject))
				{
					_aChildOfMineIsSelected = true;
					break;
				}

				_aChildOfMineIsSelected = false;
			}
		}

		private void SetSelectedChildren()
		{
			_selectedChildren.Clear();
			foreach (var child in _deepChildren)
			{
				if (Selection.gameObjects.Contains(child.gameObject))
					_selectedChildren.Add(child);
			}
		}

		private void GuiEvents(SceneView sceneView)
		{
			var guiEvent = Event.current;

			switch (guiEvent.type)
			{
				case EventType.MouseDown when Event.current.button == 0:
				{
					Mouse0Pressed();
					break;
				}
				case EventType.MouseUp when Event.current.button == 0:
				{
					Mouse0Released();
					break;
				}
			}
		}

		private void Mouse0Pressed() => TryRecordUndo();

		private void TryRecordUndo()
		{
			if (gameObject.GetComponent<RectTransform>())
				return;

			if (!(_imSelected || _aChildOfMineIsSelected))
				return;
			
			Undo.RegisterCompleteObjectUndo(transform, "Custom Undo");
			Undo.CollapseUndoOperations(Undo.GetCurrentGroup());

			foreach (Transform child in transform)
				Undo.RegisterCompleteObjectUndo(child, "Custom Undo");

			if (_aChildOfMineIsSelected)
			{
				foreach (var child in _selectedChildren)
					Undo.RegisterCompleteObjectUndo(child, "Custom Undo");
			}

			Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
		}

		private void Mouse0Released() => UpdatePositionBasedOnChildrenBoundsCenter();

		private void AddFolderData() => folders[gameObject.GetInstanceID()] = _colorIndex;

		private void RemoveFolderData() => folders.Remove(gameObject.GetInstanceID());

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

		private void HandleFolderLocking()
		{
			if (_isLocked)
			{
				ResetTransformKeepChildrenInPlace();
				transform.hideFlags = HideFlags.HideInInspector;
				Tools.hidden = true;
			}
			else
			{
				SetDeepChildren();
				CenterAtChildrenBoundingBox();
				transform.hideFlags = HideFlags.None;
				Tools.hidden = false;
			}
		}
		
		private void ResetTransformKeepChildrenInPlace()
		{
			TryRecordUndo();
			List<Vector3> previousChildrenPositions = new List<Vector3>();
			List<Quaternion> previousChildrenRotations = new List<Quaternion>();
			List<Vector3> previousChildrenScales = new List<Vector3>();

			SetChildren();
			
			foreach (var child in _children)
			{
				previousChildrenPositions.Add(child.position);
				previousChildrenRotations.Add(child.rotation);
				previousChildrenScales.Add(child.lossyScale);
			}

			ResetTransform();

			for (int i = 0; i < _children.Count; i++)
			{
				_children[i].position = previousChildrenPositions[i];
				_children[i].rotation = previousChildrenRotations[i];
				_children[i].localScale = previousChildrenScales[i];
			}
		}

		private void UpdatePositionBasedOnChildrenBoundsCenter()
		{
			if (_deepChildren == null || _deepChildren.Count <= 0)
				return;

			if (Selection.activeGameObject == null)
				return;

			if (!_deepChildren.Contains(Selection.activeGameObject.transform))
				return;

			CenterAtChildrenBoundingBox();
		}

		private void OnHierarchyChanged()
		{
			SetChildren();
			SetDeepChildren();
			SetDeepParents();
			CenterAtChildrenBoundingBox();
		}

		private void SetChildren()
		{
			_children.Clear();
			foreach (Transform child in transform)
				_children.Add(child);
		}

		private void SetDeepParents()
		{
			_deepParents.Clear();
			_deepParents = RecursiveParentSearch(transform);
		}

		private static List<Transform> RecursiveParentSearch(Transform child)
		{
			if (child.parent == null)
				return new List<Transform>();

			var parents = new List<Transform>();

			parents.Add(child.transform.parent);

			if (child.transform.parent.parent != null)
			{
				foreach (var grandParent in RecursiveParentSearch(child.transform.parent))
					parents.Add(grandParent);
			}

			return parents;
		}
		
		private void SetDeepChildren()
		{
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
		
		private void CenterAtChildrenBoundingBox()
		{
			if (_isLocked)
				return;

			if (gameObject.GetComponent<RectTransform>())
				return;

			if (transform.childCount <= 0)
				return;

			var childrenTransformCenter = GetChildrenTransformCenter(_deepChildren);
			var childrenBoundsCenter = GetChildrenBoundsCenter(childrenTransformCenter);
			SetPositionAndCorrectChildren(childrenBoundsCenter);
		}

		private Vector3 GetChildrenTransformCenter(List<Transform> transforms)
		{
			var transformsCenter = Vector3.zero;

			foreach (var child in _deepChildren)
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
			var previousPosition = transform.position;
			transform.position = position;
			var positionChange = transform.position - previousPosition;

			foreach (Transform child in transform)
				child.transform.position -= positionChange;
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

		private bool _imSelected = false;
		private bool _aChildOfMineIsSelected = false;
		private List<Transform> _selectedChildren = new List<Transform>();
		private List<Transform> _children = new List<Transform>();
		private List<Transform> _deepChildren = new List<Transform>();
		private List<Transform> _deepParents = new List<Transform>();
#endif
	}
}
