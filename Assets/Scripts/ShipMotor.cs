using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipMotor : MonoBehaviour
{
    [Header("Arrival")]
    [SerializeField] private float slowdownRadius = 12f;                // distance where ship starts slowing down on arrival
    [SerializeField] private float stoppingDistance = 0.6f;             // distance from click where it is considered arrived

    [Header("Anchor")]
    [SerializeField] private bool anchorOnArrival = true;               // allows for "anchoring" - prevents units from pushing this unit
    [SerializeField] private float anchorSnapSpeed = 25f;               // how fast we stop after anchor (higher = faster slow)
    [SerializeField] private LayerMask unitMask;                        // layer for detecting other ships (for anchor spacing)
    [SerializeField] private float anchorInfluenceRadius = 14f;         // how far to look for anchored ships
    [SerializeField] private float extraStopPerAnchored = 1.0f;         // each anchored ship increases stopping distance of others
    [SerializeField] private float maxExtraStop = 6.0f;                 // cap of max distance allowed to stop at
    [SerializeField] private bool relaxOnlyNearDestination = true;      // helps prevent early stopping
    [SerializeField] private float relaxNearDistance = 20f;             // distance for relaxation to apply

    [Header("Path Following")]
    [SerializeField] private float waypointArriveDist = 5.0f;     // close enough = advance
    [SerializeField] private float dirSmoothing = 10f;            // higher = snappier, lower = smoother

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;                    // draws visuals in scene view
    
    // cached refs
    private Rigidbody rb;
    private ShipState shipState;

    // destination
    private bool hasMoveTarget;
    private Vector3 moveTarget;        // current steering target (waypoint or final)

    private bool hasFinalDestination;
    private Vector3 finalDestination;  // where we slow/arrive/anchor (always the clicked destination / last waypoint)

    public Vector3 FinalDestination => finalDestination;
    public bool HasFinalDestination => hasFinalDestination;

    public bool HasDestination => hasFinalDestination;
    public Vector3 Destination => finalDestination;

    // movement stats (from ShipDefinition)
    private float maxSpeed;
    private float acceleration;
    private float turnRateDegPerSec;

    // runtime multipliers
    private float speedMult = 1f;
    private float accelMult = 1f;
    private float turnMult = 1f;

    // anchor state
    private bool anchored = false;
    private Vector3 anchoredPosition;
    public bool IsAnchored => anchored;

    // anchored-near-destination relaxation
    private readonly Collider[] nearby = new Collider[64];

    // per-ship pathing state (each ship follows its own copy)
    private List<Vector3> path = null;
    private int pathIndex = 0;
    private Vector3 smoothedDesiredDir = Vector3.forward;


    private void Awake(){
        rb = GetComponent<Rigidbody>();
        shipState = GetComponent<ShipState>();
        RefreshStatsFromDefinition();
    }

    public void RefreshStatsFromDefinition(){
        var def = shipState != null ? shipState.Definition : null;
        if (def == null){
            Debug.LogError($"{name}: ShipMotor could not find ShipDefinition on ShipState.");
            enabled = false;
            return;
        }

        maxSpeed = def.maxSpeed * def.maxSpeedMult * speedMult;
        acceleration = def.acceleration * def.accelMult * accelMult;
        turnRateDegPerSec = def.turnRateDegPerSec * def.turnRateMult * turnMult;
    }

    public void SetDestination(Vector3 worldPoint){
        ClearPathInternal();

        moveTarget = worldPoint;
        finalDestination = worldPoint;
        hasMoveTarget = true;
        hasFinalDestination = true;

        Unanchor();
    }

    private void SetMoveTargetOnly(Vector3 worldPoint){
        moveTarget = worldPoint;
        hasMoveTarget = true;
    }

    public void ClearDestination(){
        hasMoveTarget = false;
        hasFinalDestination = false;

        moveTarget = rb.position;          // prevent steering toward old target
        finalDestination = rb.position;    // prevents arrival logic using stale final
        path = null;
        pathIndex = 0;

        // Full state reset for clean transitions
        smoothedDesiredDir = transform.forward;

        if (!rb.isKinematic){
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Unanchor();
    }


    private void FixedUpdate(){
        // early checks
        if(anchored){
            rb.MovePosition(anchoredPosition);
            return;
        }
        if(rb.isKinematic) return;

        // clamp planar velocity
        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;

        // if we have a path, update moveTarget from current waypoint (advance + lookahead)
        UpdateMoveTargetFromPath();

        // if no destination, bleed off speed and stop
        if (!(hasFinalDestination && hasMoveTarget)){
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, 0.08f);
            return;
        }

        // compute planar vectors/distances
        ComputePlanarVectors(out Vector3 planarPos, out Vector3 toTarget, out Vector3 toFinal, out float distToFinal, out float dynamicStop);

        // arrival braking / anchoring
        if (HandleArrival(distToFinal, dynamicStop)) return;

        // desired heading (pure path following)
        Vector3 desiredDir = ComputeDesiredDirection(toTarget, distToFinal);

        // smooth desired heading to reduce flip-flop
        SmoothDesiredDirection(desiredDir);

        // rotate toward smoothed heading
        RotateTowardSmoothedHeading();

        // compute target speed (slowdown near final + slow when turning)
        float targetSpeed = ComputeTargetSpeed(distToFinal);

        // accelerate toward forward velocity
        ApplyForwardVelocity(targetSpeed);
    }

    private void UpdateMoveTargetFromPath(){
        if (path == null || path.Count == 0) return;

        Vector3 pos = rb.position;
        pos.y = 0f;
        
        // Clamp pathIndex safely
        if (pathIndex < 0) pathIndex = 0;
        if (pathIndex >= path.Count) pathIndex = path.Count - 1;

        Vector3 currentWp = path[pathIndex];
        currentWp.y = 0f;

        // On first waypoint, use closest point on line between wp0 and wp1
        if (pathIndex == 0 && path.Count > 1){
            Vector3 wp1 = path[1];
            wp1.y = 0f;
            currentWp = GetClosestPointOnLineSegment(pos, path[0], wp1);
        }

        // Check if we've arrived at current waypoint
        float distToCurrentWp = (currentWp - pos).magnitude;
        
        // Advance to next waypoint if we've reached this one
        if (distToCurrentWp <= waypointArriveDist && pathIndex < path.Count - 1){
            pathIndex++;
            currentWp = path[pathIndex];
            currentWp.y = 0f;
        }

        // simply target the current waypoint
        SetMoveTargetOnly(currentWp);
    }

    private Vector3 GetClosestPointOnLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd){
        Vector3 lineDir = lineEnd - lineStart;
        float lineLengthSq = lineDir.sqrMagnitude;
        if (lineLengthSq < 0.0001f) return lineStart; // degenerate line
        
        float t = Vector3.Dot(point - lineStart, lineDir) / lineLengthSq;
        t = Mathf.Clamp01(t); // clamp to segment bounds
        
        return lineStart + lineDir * t;
    }

    private void ComputePlanarVectors(out Vector3 planarPos, out Vector3 toTarget, out Vector3 toFinal, out float distToFinal, out float dynamicStop){
        planarPos = rb.position; 
        planarPos.y = 0f;

        toTarget = moveTarget - planarPos;
        toTarget.y = 0f;

        toFinal = finalDestination - planarPos;
        toFinal.y = 0f;

        distToFinal = toFinal.magnitude;
        dynamicStop = ComputeDynamicStoppingDistance(planarPos);
    }

    private bool HandleArrival(float distToFinal, float dynamicStop){
        if (distToFinal > dynamicStop) return false;

        rb.linearVelocity = Vector3.MoveTowards(
            rb.linearVelocity,
            Vector3.zero,
            anchorSnapSpeed * Time.fixedDeltaTime
        );

        if (anchorOnArrival && rb.linearVelocity.sqrMagnitude < 0.0025f){
            AnchorHere();
        } else if (!anchorOnArrival){
            rb.linearVelocity = Vector3.zero;
            hasMoveTarget = false;
            hasFinalDestination = false;
        }

        return true;
    }

    private Vector3 ComputeDesiredDirection(Vector3 toTarget, float distToFinal){
        // Pure path following without flocking - just head toward the target
        return toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : transform.forward;
    }

    private void SmoothDesiredDirection(Vector3 desiredDir){
        if (desiredDir.sqrMagnitude <= 0.001f){
            smoothedDesiredDir = transform.forward;
            return;
        }

        float t = 1f - Mathf.Exp(-dirSmoothing * Time.fixedDeltaTime);
        smoothedDesiredDir = Vector3.Slerp(smoothedDesiredDir, desiredDir, t);
        smoothedDesiredDir.y = 0f;

        if (smoothedDesiredDir.sqrMagnitude > 0.0001f){
            smoothedDesiredDir.Normalize();
        } else {
            smoothedDesiredDir = transform.forward;
        }
    }

    private void RotateTowardSmoothedHeading(){
        if (smoothedDesiredDir.sqrMagnitude <= 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(smoothedDesiredDir, Vector3.up);
        Quaternion newRot = Quaternion.RotateTowards(
            rb.rotation,
            targetRot,
            turnRateDegPerSec * Time.fixedDeltaTime
        );
        rb.MoveRotation(newRot);
    }

    private float ComputeTargetSpeed(float distToFinal){
        // slowdown near final destination
        float speed = maxSpeed;
        if (distToFinal < slowdownRadius){
            float t = Mathf.Clamp01(distToFinal / slowdownRadius);
            speed = Mathf.Lerp(0f, maxSpeed, t);
        }

        // slow down when turning sharply
        float angle = Vector3.Angle(transform.forward, smoothedDesiredDir);
        float turnSlowFactor = Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(angle / 90f));
        return speed * turnSlowFactor;
    }

    private void ApplyForwardVelocity(float targetSpeed){
        Vector3 desiredVelocity = transform.forward * targetSpeed;

        Vector3 newVelocity = Vector3.MoveTowards(
            rb.linearVelocity,
            desiredVelocity,
            acceleration * Time.fixedDeltaTime
        );
        newVelocity.y = 0f;
        rb.linearVelocity = newVelocity;
    }

    private void AnchorHere(){
        anchored = true;
        anchoredPosition = rb.position;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.isKinematic = true;
    }

    private void Unanchor(){
        if (!anchored) return;

        anchored = false;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void OnDrawGizmos(){
        if (!drawGizmos) return;
        if (hasFinalDestination){
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(finalDestination, 0.5f);
            Gizmos.DrawLine(transform.position, finalDestination);
        }
        if (path != null && path.Count > 0){
            Gizmos.color = Color.cyan;
            for (int i = 0; i < path.Count - 1; i++)
                Gizmos.DrawLine(path[i], path[i + 1]);
        }
    }

    public void ApplyStatMultipliers(float newSpeedMult, float newAccelMult, float newTurnMult){
        speedMult = Mathf.Max(0f, newSpeedMult);
        accelMult = Mathf.Max(0f, newAccelMult);
        turnMult = Mathf.Max(0f, newTurnMult);
        RefreshStatsFromDefinition();
    }

    private float ComputeDynamicStoppingDistance(Vector3 planarPos){
        float baseStop = stoppingDistance;

        if (!hasFinalDestination) return baseStop;

        // relax when you're actually near the destination
        if (relaxOnlyNearDestination){
            Vector3 toGoal = finalDestination - planarPos;
            toGoal.y = 0f;
            if (toGoal.magnitude > relaxNearDistance){
                return baseStop;
            }
        }

        // count anchored ships near the destination
        int count = Physics.OverlapSphereNonAlloc(finalDestination, anchorInfluenceRadius, nearby, unitMask);
        if (count <= 0) return baseStop;

        int anchoredCount = 0;
        for(int i = 0; i < count; i++){
            var c = nearby[i];
            if (c == null) continue;

            ShipMotor other = c.GetComponentInParent<ShipMotor>();
            if (other == null) continue;
            if (other == this) continue;

            if (other.IsAnchored){
                anchoredCount++;
            }
        }

        float extra = Mathf.Min(maxExtraStop, anchoredCount * extraStopPerAnchored);
        return baseStop + extra;
    }

    // assigns an explicit waypoint path to this ship only
    // the last waypoint is treated as the final destination (arrival/anchoring logic uses it).

    public void SetPath(System.Collections.Generic.List<Vector3> newPath){
        // Clear all old path state
        ClearPathInternal();
        
        // Unanchor before starting new path
        Unanchor();

        if (newPath == null || newPath.Count == 0) return;

        path = newPath;
        pathIndex = 0;

        // Set final destination from last waypoint
        finalDestination = path[path.Count - 1];
        hasFinalDestination = true;

        // Set initial move target to first waypoint
        moveTarget = path[0];
        hasMoveTarget = true;

        // Initialize smoothed direction toward first waypoint
        Vector3 initialDir = (moveTarget - rb.position);
        initialDir.y = 0f;
        if (initialDir.sqrMagnitude < 0.001f){
            smoothedDesiredDir = transform.forward;
        } else {
            smoothedDesiredDir = initialDir.normalized;
        }
    }

    public void ClearPathInternal(){
        path = null;
        pathIndex = 0;
    }

}