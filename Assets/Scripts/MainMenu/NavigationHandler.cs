using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class NavigationHandler : MonoBehaviour
{
    public GameObject defaultSelectable;

    private DungeonControls controls;
    private EventSystem currentEventSystem;
    private GameObject lastSelected;
    private bool navigatePressedThisFrame;

    private void Awake()
    {
        currentEventSystem = EventSystem.current;
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
    }

    private void Update()
    {
        if (currentEventSystem == null) return;

        // Restore focus if navigation is used and nothing is selected
        if (currentEventSystem.currentSelectedGameObject == null && navigatePressedThisFrame)
        {
            ReestablishUIFocus();
        }

        GameObject current = currentEventSystem.currentSelectedGameObject;
        if (lastSelected != current)
        {
            lastSelected = current;
            if (lastSelected != null)
            {
                Common.Instance.AudioManager.PlaySoundEffect(Common.Instance.AudioManager.SoundEffects.Hover);
            }
        }
    }

    private void LateUpdate()
    {
        navigatePressedThisFrame = false; // Reset each frame
    }

    private void OnNavigatePerformed(InputAction.CallbackContext context)
    {
        navigatePressedThisFrame = true;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            ReestablishUIFocus();
        }
    }

    private void ReestablishUIFocus()
    {
        if (currentEventSystem.currentSelectedGameObject == null)
        {
            if (lastSelected != null && lastSelected.activeInHierarchy)
            {
                currentEventSystem.SetSelectedGameObject(lastSelected);
            }
            else if (defaultSelectable != null)
            {
                currentEventSystem.SetSelectedGameObject(defaultSelectable);
                lastSelected = defaultSelectable;
            }
            else if (currentEventSystem.firstSelectedGameObject != null)
            {
                currentEventSystem.SetSelectedGameObject(currentEventSystem.firstSelectedGameObject);
                lastSelected = currentEventSystem.firstSelectedGameObject;
            }
        }
    }

    private Stack<GameObject> selectionStack = new Stack<GameObject>();
    private Stack<GameObject> dialogFirstSelectables = new Stack<GameObject>();
    private HashSet<MonoBehaviour> activeDialogs = new HashSet<MonoBehaviour>();

    public void PushDialog(MonoBehaviour dialogOwner, GameObject firstSelectable)
    {
        // Prevent duplicates
        if (activeDialogs.Contains(dialogOwner))
            return;

        activeDialogs.Add(dialogOwner);

        // Save current selection
        var current = EventSystem.current.currentSelectedGameObject;
        if (current != null)
            selectionStack.Push(current);

        dialogFirstSelectables.Push(firstSelectable);
        SetSelectable(firstSelectable);
    }

    public void PopDialog(MonoBehaviour dialogOwner)
    {
        if (!activeDialogs.Contains(dialogOwner))
            return;

        activeDialogs.Remove(dialogOwner);

        // Remove current dialog's firstSelectable
        if (dialogFirstSelectables.Count > 0)
            dialogFirstSelectables.Pop();

        if (selectionStack.Count > 0)
        {
            GameObject toSelect = selectionStack.Pop();
            SetSelectable(toSelect);
        }
        else
        {
            // Fallback to firstSelectable from NavigationSystem
            SetSelectable(EventSystem.current.firstSelectedGameObject);
        }
    }

    private void SetSelectable(GameObject obj)
    {
        if (obj != null && obj.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(obj);
            lastSelected = obj;
        }
    }
}
