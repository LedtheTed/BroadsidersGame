using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GroupMoveController : MonoBehaviour
{

    // *********************************************
    // HANDLES MOVE ORDERS WHEN RIGHT CLICKING
    // GRABS UNIT FROM SELECTED LIST AND SEND TO
    // CLICK POINT
    // *********************************************

    [Header("References")]
    [SerializeField] private SelectionManager selection;        // reference to list
    [SerializeField] private Camera cam;                        // reference to main camera

    [Header("Raycast")]
    [SerializeField] private LayerMask waterMask;               // layer we want to click on (our water/ocean)
    [SerializeField] private float maxRayDistance = 5000f;      // arbitrary check distance

    [Header("Formation")]
    [SerializeField] private float spacing = 4f;     // distance between ships in formation
    [SerializeField] private float slotArriveRadius = 0.5f;     // in position distance
    [SerializeField] private float holdSpeed = 0.8f;            // speed cap while waiting
    [SerializeField] private bool enableHoldUntilAllArrive = true;
    

private Dictionary<SelectableUnit, Vector3> currentSlots = new();
private bool formationActive = false;

    // initial set up
    private void Awake(){
        if(selection == null) selection = FindFirstObjectByType<SelectionManager>();
        if(cam == null) cam = Camera.main;
    }

    // update function which happens every frame
    private void Update(){
        if(cam == null || selection == null) return;
        if(Mouse.current == null) return;

        // if we right click move group
        if(Mouse.current.rightButton.wasPressedThisFrame){
            if (selection.Selected.Count == 0) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, waterMask)){
                IssueGroupMove(hit.point);
            }
        }
        if (formationActive){
            UpdateFormationHold();
        }
    }

    // move all selected units to point
    private void IssueGroupMove(Vector3 anchor){

        var units = selection.Selected;             // get all units currently selected

        // compute facing: from group center to anchor (so formation faces the move direction)
        Vector3 center = ComputeCenter(units);
        Vector3 forward = (anchor - center);
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = cam.transform.forward; // fallback
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        // generate N formation slots (grid)
        List<Vector3> slots = GenerateDiscSlots(anchor, units.Count);

        // assign units to closest slots (greedy)
        AssignGreedy(units, slots);
    }

    // compute center of all selected units
    private Vector3 ComputeCenter(IReadOnlyList<SelectableUnit> units){
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < units.Count; i++)
            sum += units[i].transform.position;
        return sum / Mathf.Max(1, units.Count);
    }

    // creat spots around anchor for ships to fill
    private List<Vector3> GenerateDiscSlots(Vector3 anchor, int n){
        
        List<Vector3> slots = new List<Vector3>(n);
        if (n <= 0) return slots;

        int placed = 0;
        int ring = 1;

        while (placed < n){
            float radius = ring * spacing;
            // choose how many points on this ring (roughly circumference / spacing)
            int ringCount = Mathf.Max(6, Mathf.RoundToInt((2f * Mathf.PI * radius) / spacing));

            for(int i = 0; i < ringCount && placed < n; i++){
                float angle = (i / (float)ringCount) * Mathf.PI * 2f;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                slots.Add(anchor + offset);
                placed++;
            }
            ring++;
        }

        return slots;
    }

    // assign ships to calulated spots
    private void AssignGreedy(IReadOnlyList<SelectableUnit> units, List<Vector3> slots){
        
        currentSlots.Clear();
        List<Vector3> freeSlots = new List<Vector3>(slots);

        for(int i = 0; i < units.Count; i++){
            SelectableUnit unit = units[i];

            int bestIndex = 0;
            float bestDist = float.MaxValue;

            for(int s = 0; s < freeSlots.Count; s++){
                float d = (unit.transform.position - freeSlots[s]).sqrMagnitude;
                if (d < bestDist){
                    bestDist = d;
                    bestIndex = s;
                }
            }

            Vector3 chosen = freeSlots[bestIndex];
            freeSlots.RemoveAt(bestIndex);

            currentSlots[unit] = chosen;

            if (unit.Motor != null){
                unit.Motor.ExitHold();     // clears any previous hold state
                unit.Motor.SetDestination(chosen);
            }
        }

        formationActive = enableHoldUntilAllArrive;
    }

    private void UpdateFormationHold(){
        if (currentSlots.Count == 0){
            formationActive = false;
            return;
        }

        bool allArrived = true;

        foreach(var kvp in currentSlots){
            SelectableUnit unit = kvp.Key;
            Vector3 slot = kvp.Value;

            if(unit == null || unit.Motor == null) continue;

            Vector3 pos = unit.transform.position;
            pos.y = 0f;
            Vector3 target = slot;
            target.y = 0f;

            float dist = Vector3.Distance(pos, target);

            bool arrived = dist <= slotArriveRadius;

            if (!arrived){
                allArrived = false;
                unit.Motor.ExitHold();
            }else{
                // ff not everyone has arrived yet, cap speed so it holds nicely
                unit.Motor.EnterHold(holdSpeed);
            }
        }

        if (allArrived){
            // everyone is in formation: release speed limits
            foreach (var kvp in currentSlots){
                if (kvp.Key != null && kvp.Key.Motor != null)
                    kvp.Key.Motor.ExitHold();
            }

            formationActive = false;
        }
    }


}
