using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GroupMoveController : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private SelectionManager selection;
    [SerializeField] private Camera cam;

    [Header("Raycast")]
    [SerializeField] private LayerMask waterMask;
    [SerializeField] private float maxRayDistance = 5000f;

    [Header("Formation")]
    [SerializeField] private float spacing = 4f;     // distance between ships in formation
    [SerializeField] private int columns = 5;        // grid formation width

    private void Awake(){
        if (selection == null) selection = FindFirstObjectByType<SelectionManager>();
        if (cam == null) cam = Camera.main;
    }

    private void Update(){
        if (cam == null || selection == null) return;
        if (Mouse.current == null) return;

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (selection.Selected.Count == 0) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, waterMask))
            {
                IssueGroupMove(hit.point);
            }
        }
    }

    private void IssueGroupMove(Vector3 anchor)
    {
        var units = selection.Selected;

        // Compute facing: from group center -> anchor (so formation faces the move direction)
        Vector3 center = ComputeCenter(units);
        Vector3 forward = (anchor - center);
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = cam.transform.forward; // fallback
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        // Generate N formation slots (grid)
        List<Vector3> slots = GenerateGridSlots(anchor, forward, right, units.Count);

        // Assign units to closest slots (greedy)
        AssignGreedy(units, slots);
    }

    private Vector3 ComputeCenter(IReadOnlyList<SelectableUnit> units)
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < units.Count; i++)
            sum += units[i].transform.position;
        return sum / Mathf.Max(1, units.Count);
    }

    private List<Vector3> GenerateGridSlots(Vector3 anchor, Vector3 forward, Vector3 right, int n)
    {
        // Put the front row near the anchor, subsequent rows behind it

        int cols = Mathf.Max(1, columns);
        float half = (cols - 1) * 0.5f;

        List<Vector3> slots = new List<Vector3>(n);

        for (int i = 0; i < n; i++)
        {
            int row = i / cols;
            int col = i % cols;

            float x = (col - half) * spacing;       // left/right
            float z = -row * spacing;               // behind anchor

            Vector3 offset = right * x + forward * z;
            Vector3 slot = anchor + offset;
            slot.y = anchor.y;

            slots.Add(slot);
        }

        return slots;
    }

    private void AssignGreedy(IReadOnlyList<SelectableUnit> units, List<Vector3> slots)
    {
        // copy to mutable list
        List<Vector3> freeSlots = new List<Vector3>(slots);

        for (int i = 0; i < units.Count; i++)
        {
            SelectableUnit unit = units[i];
            int bestIndex = 0;
            float bestDist = float.MaxValue;

            for (int s = 0; s < freeSlots.Count; s++)
            {
                float d = (unit.transform.position - freeSlots[s]).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIndex = s;
                }
            }

            Vector3 chosen = freeSlots[bestIndex];
            freeSlots.RemoveAt(bestIndex);

            if (unit.Motor != null)
                unit.Motor.SetDestination(chosen);
        }
    }

}
