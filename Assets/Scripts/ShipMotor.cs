using UnityEngine;

public class ShipMotor : MonoBehaviour
{

    [Header("Movement")]
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private float acceleration = 6f;
    [SerializeField] private float turnRateDegPerSec = 90f;

    [Header("Arrival")]
    [SerializeField] private float slowdownRadius = 12f;
    [SerializeField] private float stoppingDistance = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private Rigidbody rb;

    private bool hasDestination = false;
    private Vector3 destination;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void SetDestination(Vector3 worldPoint)
    {
        destination = worldPoint;
        hasDestination = true;
    }

    public void ClearDestination()
    {
        hasDestination = false;
    }

    private void FixedUpdate()
    {
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
            targetSpeed = Mathf.Lerp(0.5f, maxSpeed, t);
        }

        // slow down while turning sharply
        float angle = Vector3.Angle(transform.forward, desiredDir);
        float turnSlowFactor = Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(angle / 90f));
        targetSpeed *= turnSlowFactor;

        // accelerate toward target speed
        Vector3 desiredVelocity = transform.forward * targetSpeed;
        Vector3 newVelocity = Vector3.MoveTowards(rb.linearVelocity, desiredVelocity, acceleration * Time.fixedDeltaTime);

        newVelocity.y = 0f;
        rb.linearVelocity = newVelocity;

    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        if (hasDestination)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(destination, 0.5f);
            Gizmos.DrawLine(transform.position, destination);
        }
    }



    
}
