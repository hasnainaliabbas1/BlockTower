using UnityEngine;

public class TowerPhysics : MonoBehaviour
{
    [Header("Physics Properties")]
    [SerializeField] private float stabilizationForce = 10f;
    [SerializeField] private float maxStabilizationTorque = 5f;
    [SerializeField] private float frictionCoefficient = 0.8f;

    private Rigidbody rb;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float timeAlive = 0f;
    private bool isInitialized = false;
    private bool isStable = false;
    private float stabilityCheckDelay = 2.0f;

    public void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        lastPosition = transform.position;
        lastRotation = transform.rotation;
        timeAlive = 0f;
        isInitialized = true;

        // Configure physics material to control friction
        PhysicsMaterial material = new PhysicsMaterial("BlockMaterial"); // Updated line ?
        material.dynamicFriction = frictionCoefficient;
        material.staticFriction = frictionCoefficient * 1.2f;
        material.bounciness = 0.1f;
        material.frictionCombine = PhysicsMaterialCombine.Average;

        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.sharedMaterial = material; // Already fixed before ?
        }
    }


    void FixedUpdate()
    {
        if (!isInitialized) return;

        timeAlive += Time.fixedDeltaTime;

        if (timeAlive > stabilityCheckDelay && !isStable)
        {
            CheckStability();
        }

        ApplyStabilizationForces();
    }

    private void CheckStability()
    {
        float positionDelta = Vector3.Distance(transform.position, lastPosition);
        float rotationDelta = Quaternion.Angle(transform.rotation, lastRotation);

        if (positionDelta < 0.001f && rotationDelta < 0.1f)
        {
            isStable = true;
            rb.mass *= 1.2f;
        }

        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }

    private void ApplyStabilizationForces()
    {
        if (timeAlive < 1.5f)
        {
            Vector3 up = transform.up;
            float tiltAngle = Vector3.Angle(up, Vector3.up);

            if (tiltAngle > 5f)
            {
                Vector3 cross = Vector3.Cross(up, Vector3.up);
                float torqueStrength = Mathf.Min(maxStabilizationTorque, tiltAngle * 0.1f);
                rb.AddTorque(cross.normalized * torqueStrength, ForceMode.Force);
            }

            if (rb.linearVelocity.y < -0.1f)
            {
                rb.AddForce(Vector3.up * stabilizationForce * Mathf.Abs(rb.linearVelocity.y), ForceMode.Force);
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (timeAlive < 0.5f && collision.gameObject.CompareTag("Platform"))
        {
            Vector3 impulse = Vector3.zero;
            foreach (ContactPoint contact in collision.contacts)
            {
                impulse += contact.normal;
            }

            Vector3 randomForce = new Vector3(
                Random.Range(-0.1f, 0.1f),
                0,
                Random.Range(-0.1f, 0.1f)
            );

            rb.AddForce(randomForce, ForceMode.Impulse);
        }
    }
}
