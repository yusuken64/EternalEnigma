using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class NavigationHandler : MonoBehaviour
{
    [Tooltip("Default UI element to select when none is active.")]
    public GameObject defaultSelectable;

    private DungeonControls controls;
    private EventSystem es;
    private GameObject lastSelected;
    private bool navigatePressedThisFrame;
    private float lastHoverSoundTime;
    private const float hoverSoundCooldown = 0.1f;

    private Stack<GameObject> selectionStack = new Stack<GameObject>();
    private Stack<GameObject> dialogFirstSelectables = new Stack<GameObject>();
    private HashSet<MonoBehaviour> activeDialogs = new HashSet<MonoBehaviour>();
    
    public RectTransform selectionArrow;
    public Vector3 arrowOffset = new Vector3(0, 40, 0); // offset above button
    public float arrowFollowSpeed = 10f;

    private void Awake()
    {
        es = EventSystem.current;
        controls = new DungeonControls();
    }

    private void OnEnable()
    {
        controls.UI.Enable();
        controls.UI.Navigate.performed += OnNavigatePerformed;
    }

    private void OnDisable()
    {
        controls.UI.Navigate.performed -= OnNavigatePerformed;
        controls.UI.Disable();
        ClearAllDialogs();
    }

    private void Update()
    {
        if (es == null) return;

        // Reestablish UI focus if navigation is used and nothing is selected
        if (es.currentSelectedGameObject == null && navigatePressedThisFrame)
            ReestablishUIFocus();

        // Hover sound playback when changing selection
        GameObject current = es.currentSelectedGameObject;

        UpdateSelectionArrow(current);

        if (lastSelected != current)
        {
            lastSelected = current;
            if (lastSelected != null && Time.unscaledTime - lastHoverSoundTime > hoverSoundCooldown)
            {
                Common.Instance.AudioManager.PlaySoundEffect(Common.Instance.AudioManager.SoundEffects.Hover);
                lastHoverSoundTime = Time.unscaledTime;
                
                // Trigger "Selected" animation on Buttons or any Selectable
                var selectable = lastSelected.GetComponent<Selectable>();
                if (selectable != null && selectable.enabled && selectable.interactable)
                {
                    // Force it into its 'Selected' visual state
                    selectable.OnSelect(null);

                    // If it’s a Button, also notify the Animator
                    var animator = selectable.animator;
                    if (animator != null && animator.isActiveAndEnabled)
                    {
                        // Unity’s default button animator uses the "Highlighted" trigger when selected
                        animator.SetTrigger("Highlighted");
                    }
                }
            }
        }
    }

    private void UpdateSelectionArrow(GameObject current)
    {
        if (selectionArrow == null)
            return;

        if (current == null || !current.activeInHierarchy)
        {
            selectionArrow.gameObject.SetActive(false);
            return;
        }

        // Make arrow visible
        if (!selectionArrow.gameObject.activeSelf)
            selectionArrow.gameObject.SetActive(true);

        // Smoothly move arrow toward selected UI element
        RectTransform target = current.GetComponent<RectTransform>();
        if (target != null)
        {
            Vector3 targetPos = target.position + arrowOffset;
            selectionArrow.position = Vector3.Lerp(selectionArrow.position, targetPos, Time.unscaledDeltaTime * arrowFollowSpeed);
        }
    }

    private void LateUpdate()
    {
        navigatePressedThisFrame = false; // Reset flag each frame
    }

    private void OnNavigatePerformed(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();
        if (input.sqrMagnitude > 0.1f)
            navigatePressedThisFrame = true;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            ReestablishUIFocus();
    }

    private void ReestablishUIFocus()
    {
        if (es == null || es.currentSelectedGameObject != null)
            return;

        GameObject toSelect = null;

        if (lastSelected != null && lastSelected.activeInHierarchy)
            toSelect = lastSelected;
        else if (defaultSelectable != null)
            toSelect = defaultSelectable;
        else if (es.firstSelectedGameObject != null)
            toSelect = es.firstSelectedGameObject;

        if (toSelect != null)
            SetSelectable(toSelect);
    }

    public void PushDialog(MonoBehaviour dialogOwner, GameObject firstSelectable)
    {
        // Prevent duplicates
        if (activeDialogs.Contains(dialogOwner))
            return;

        activeDialogs.Add(dialogOwner);

        // Save current selection
        var current = es.currentSelectedGameObject;
        if (current != null)
            selectionStack.Push(current);

        dialogFirstSelectables.Push(firstSelectable);
        SetSelectable(firstSelectable);
    }

    public void PopDialog(MonoBehaviour dialogOwner)
    {
        // Cleanup nulls before processing
        activeDialogs.RemoveWhere(d => d == null);

        if (!activeDialogs.Contains(dialogOwner))
            return;

        activeDialogs.Remove(dialogOwner);

        if (dialogFirstSelectables.Count > 0)
            dialogFirstSelectables.Pop();

        if (selectionStack.Count > 0)
        {
            SetSelectable(selectionStack.Pop());
        }
        else
        {
            SetSelectable(es.firstSelectedGameObject ?? defaultSelectable);
        }
    }

    private void SetSelectable(GameObject obj)
    {
        if (obj != null && obj.activeInHierarchy)
        {
            es.SetSelectedGameObject(obj);
            lastSelected = obj;
        }
    }

    public void ClearAllDialogs()
    {
        activeDialogs.Clear();
        selectionStack.Clear();
        dialogFirstSelectables.Clear();
    }
}
