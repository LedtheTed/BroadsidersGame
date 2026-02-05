using UnityEngine;

public class ShipMotor : MonoBehaviour
{

    [Header("Arrival")]
    [SerializeField] private float slowdownRadius = 12f;
    [SerializeField] private float stoppingDistance = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    [Header("Speed Override (formation hold)")]
    [SerializeField] private bool useSpeedLimit = false;
    [SerializeField] private float speedLimit = 0.1f; // used when override active

    [Header("Formation Hold")]
    [SerializeField] private bool holdMode = false;
    [SerializeField] private float holdBrake = 20f;

    private Rigidbody rb;
    private ShipState shipState;
    
    private bool hasDestination = false;
    private Vector3 destination;

    // movement stats (from ShipDefinition, then modified by multipliers/effects)
    private float maxSpeed;
    private float acceleration;
    private float turnRateDegPerSec;

    // runtime multipliers (for upgrades/status effects)
    private float speedMult = 1f;
    private float accelMult = 1f;
    private float turnMult = 1f;

    private void Awake(){
        rb = GetComponent<Rigidbody>();
        shipState = GetComponent<ShipState>();
        RefreshStatsFromDefinition();
    }

    // pulls base movement stats from ShipDefinition and re-applies current multipliers.
    // call this if we swap ShipDefinition at runtime (upgrades/transformations).
    public void RefreshStatsFromDefinition(){
        var def = shipState.Definition;
        if (def == null){
            Debug.LogError($"{name}: ShipMotor could not find ShipDefinition on ShipState.");
            enabled = false;
            return;
        }

        // base values come from definition
        float baseMaxSpeed = def.maxSpeed;
        float baseAcceleration = def.acceleration;
        float baseTurnRate = def.turnRateDegPerSec;

        // apply definition multipliers AND runtime multipliers
        maxSpeed = baseMaxSpeed * def.maxSpeedMult * speedMult;
        acceleration = baseAcceleration * def.accelMult * accelMult;
        turnRateDegPerSec = baseTurnRate * def.turnRateMult * turnMult;
    }

    public void SetDestination(Vector3 worldPoint){
        destination = worldPoint;
        hasDestination = true;
    }

    public void ClearDestination(){
        hasDestination = false;
    }

    private void FixedUpdate(){
        if (!hasDestination){
            // slowly slow velocity when idle
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, 0.05f);
            return;
        }

        Vector3 pos = rb.position;
        Vector3 toTarget = destination - pos;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;

        if (distance < stoppingDistance){
            if(holdMode){
                // in hold mode, keep destination active so the ship continues correcting
                // but brake hard to prevent drifting/pushing.
                rb.linearVelocity = Vector3.MoveTowards(rb.linearVelocity, Vector3.zero, holdBrake * Time.fixedDeltaTime);
                return;
            }
            rb.linearVelocity = Vector3.zero;
            hasDestination = false;
            return;
        }

        Vector3 desiredDir = toTarget.normalized;

        // turn toward direction
        if(desiredDir.sqrMagnitude > 0.001f){
            Quaternion current = rb.rotation;
            Quaternion targetRot = Quaternion.LookRotation(desiredDir, Vector3.up);
            Quaternion newRot = Quaternion.RotateTowards(current, targetRot, turnRateDegPerSec * Time.fixedDeltaTime);
            rb.MoveRotation(newRot);
        }

        // speed based on distance
        float targetSpeed = maxSpeed;
        if (distance < slowdownRadius){
            float t = Mathf.Clamp01(distance / slowdownRadius);
            targetSpeed = Mathf.Lerp(0f, maxSpeed, t);
        }

        // slow down while turning sharply
        float angle = Vector3.Angle(transform.forward, desiredDir);
        float turnSlowFactor = Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(angle / 90f));
        targetSpeed *= turnSlowFactor;

        // slow if getting in formation
        if (useSpeedLimit){
            targetSpeed = Mathf.Min(targetSpeed, speedLimit);
        }

        // accelerate toward target speed
        Vector3 desiredVelocity = transform.forward * targetSpeed;
        Vector3 newVelocity = Vector3.MoveTowards(rb.linearVelocity, desiredVelocity, acceleration * Time.fixedDeltaTime);

        newVelocity.y = 0f;
        rb.linearVelocity = newVelocity;

    }

    private void OnDrawGizmos(){
        if (!drawGizmos) return;

        if(hasDestination){
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(destination, 0.5f);
            Gizmos.DrawLine(transform.position, destination);
        }
    }


    public void SetSpeedLimit(float limit){
        useSpeedLimit = true;
        speedLimit = Mathf.Max(0f, limit);
    }

    public void ClearSpeedLimit(){
        useSpeedLimit = false;
    }
    
    public void EnterHold(float limitSpeed){
        holdMode = true;
        SetSpeedLimit(limitSpeed); // reuse your existing speed limit
    }

    public void ExitHold(){
        holdMode = false;
        ClearSpeedLimit();
    }

    // changes speed based on some effect
    public void ApplyStatMultipliers(float newSpeedMult, float newAccelMult, float newTurnMult){
        speedMult = Mathf.Max(0f, newSpeedMult);
        accelMult = Mathf.Max(0f, newAccelMult);
        turnMult = Mathf.Max(0f, newTurnMult);

        RefreshStatsFromDefinition();
    }

}
