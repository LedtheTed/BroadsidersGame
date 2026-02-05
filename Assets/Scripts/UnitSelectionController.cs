using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;


public class UnitSelectionController : MonoBehaviour
{

    // *********************************************
    // HANDLES SELECTING UNITS IN GAME WITH RAYCAST
    // SINGLE CLICK AND DRAG SELECT
    // DOES NOT MOVE ANYTHING
    // *********************************************
    
    [Header("References")]
    [SerializeField] private SelectionManager selection;            // access to selected list
    [SerializeField] private Camera cam;                            // access to camer

    [Header("Raycast")]
    [SerializeField] private LayerMask unitMask;                    // access to unit layer
    [SerializeField] private float maxRayDistance = 5000f;          // arbitrary check distance

    [Header("Box Select")]
    [SerializeField] private float dragThresholdPx = 8f;

    private Vector2 dragStart;
    private bool isDragging;

    // setup on start
    private void Awake(){
        if(selection == null) selection = FindFirstObjectByType<SelectionManager>();
        if(cam == null) cam = Camera.main;
    }

    // update function which happens every frame
    private void Update(){
        if (cam == null || selection == null) return;
        if (Mouse.current == null || Keyboard.current == null) return;

        bool shift = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;

        // if started to left click, set starting point of box
        if(Mouse.current.leftButton.wasPressedThisFrame){
            dragStart = Mouse.current.position.ReadValue();
            isDragging = false;
        }

        // if still clicking we are dragging
        if(Mouse.current.leftButton.isPressed){
            Vector2 now = Mouse.current.position.ReadValue();
            if(!isDragging && Vector2.Distance(now, dragStart) > dragThresholdPx){
                isDragging = true;
            }
        }

        // if mouse released, call box select
        if(Mouse.current.leftButton.wasReleasedThisFrame){
            Vector2 dragEnd = Mouse.current.position.ReadValue();

            if(!shift){
                // FUTURE IDEA: if holding shift add to current list
            }

            if(isDragging){
                if(!shift) selection.ClearSelection();
                BoxSelect(dragStart, dragEnd, additive: shift);
            } else {
                ClickSelect(shift);
            }

            isDragging = false;
        }
    }

    // single Click logic / select
    private void ClickSelect(bool shift){
        Vector2 mousePos = Mouse.current.position.ReadValue();

        Ray ray = cam.ScreenPointToRay(mousePos);

        if(Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, unitMask)){
            SelectableUnit unit = hit.collider.GetComponentInParent<SelectableUnit>();
            if(unit == null) return;

            if(!shift){
                selection.SetSelectionSingle(unit);
            } else {
                if(selection.Selected.Contains(unit)){
                    selection.RemoveFromSelection(unit);
                } else {
                    selection.AddToSelection(unit);
                }
            }
        } else { // clicked empty space
            if(!shift) selection.ClearSelection();
        }
    }

    // builds the rectangle we are selcting with
    private Rect GetScreenRectGUI(Vector2 aScreen, Vector2 bScreen){
        // convert screen space (bottom-left origin) to GUI space (top-left origin)
        Vector2 a = new Vector2(aScreen.x, Screen.height - aScreen.y);
        Vector2 b = new Vector2(bScreen.x, Screen.height - bScreen.y);

        float xMin = Mathf.Min(a.x, b.x);
        float xMax = Mathf.Max(a.x, b.x);
        float yMin = Mathf.Min(a.y, b.y);
        float yMax = Mathf.Max(a.y, b.y);

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    // logic to select groupd of troops in box
    private void BoxSelect(Vector2 startScreen, Vector2 endScreen, bool additive){
        if (!additive) selection.ClearSelection();

        Rect rectGUI = GetScreenRectGUI(startScreen, endScreen);

        SelectableUnit[] all = FindObjectsByType<SelectableUnit>(FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            SelectableUnit unit = all[i];

            Vector3 sp = cam.WorldToScreenPoint(unit.transform.position);
            if (sp.z < 0f) continue;

            // convert unit position to GUI space too
            Vector2 unitGUI = new Vector2(sp.x, Screen.height - sp.y);

            if (rectGUI.Contains(unitGUI))
                selection.AddToSelection(unit);
        }
    }

    // creates the visual rectangle (looks cool)
    private void OnGUI(){
        if (!isDragging || Mouse.current == null) return;

        Vector2 currentScreen = Mouse.current.position.ReadValue();
        Rect r = GetScreenRectGUI(dragStart, currentScreen);

        GUI.color = new Color(0f, 1f, 0f, 0.15f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);

        GUI.color = new Color(0f, 1f, 0f, 0.9f);
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMin, r.yMax - 2, r.width, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, 2, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - 2, r.yMin, 2, r.height), Texture2D.whiteTexture);

        GUI.color = Color.white;
    }


}
