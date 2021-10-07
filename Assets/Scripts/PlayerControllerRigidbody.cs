using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerControllerRigidbody : MonoBehaviour
{
    [SerializeField]
    Transform playerInputSpace = default;

    [SerializeField, Range(0, 50)]
    float speed = 6.7f;

    [SerializeField, Range(0, 50)]
    float acceleration;

    [SerializeField, Range(0, 1)]
    float maxAirAcceleration = 1f;

    [SerializeField, Range(0, 10)]
    float jumpHeight = 2f;

    [SerializeField, Range(0, 1)]
    float turnSmoothTime = 0.2f;

    [SerializeField, Range(0, 5)]
    int maxAirJumps = 0;

    [SerializeField, Range(0f, 90f)]
    float maxGroundAngle = 25f;

    [SerializeField, Range(0f, 100f)]
    float maxSnapSpeed = 100f;

    [SerializeField, Min(0f)]
    float probeDistance = 1f;

    [SerializeField]
    LayerMask probeMask = -1;

    private Rigidbody rigidBody;
    private Animator animator;
    private Vector3 velocity;
    private Vector3 direction;
    private Vector3 desiredVelocity;
    private Vector3 cameraOffset;
    private float turnSmoothVelocity;
    private bool desiredJump;
    private int jumpPhase;
    private float minGroundDotProduct;
    private Vector3 contactNormal, steepNormal;
    private int stepsSinceLastGrounded, stepsSinceLastJump;

    private int groundContactCount, steepContactCount;
    bool OnGround => groundContactCount > 0;
    bool OnSteep => steepContactCount > 0;

    void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        OnValidate();
    }

    void OnValidate()
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
    }

    void Update()
    {
        direction = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
        direction.Normalize();

        if (playerInputSpace)
        {
            Vector3 forward = playerInputSpace.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = playerInputSpace.right;
            right.y = 0f;
            right.Normalize();

            direction = forward * direction.z + right * direction.x;
            desiredVelocity = direction * speed;
        }
        else
            desiredVelocity = direction * speed;

        if (direction.magnitude != 0)
        {
            float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            transform.eulerAngles = Vector3.up * Mathf.SmoothDampAngle(transform.eulerAngles.y, angle, ref turnSmoothVelocity, turnSmoothTime);
        }
        

        desiredJump |= Input.GetButtonDown("Jump");
    }

    void FixedUpdate()
    {
        UpdateState();
        AdjustVelocity();

        if (desiredJump)
        {
            desiredJump = false;
            Jump();
        }

        rigidBody.velocity = velocity;
        animator.SetFloat("Blend", new Vector3(velocity.x, 0, velocity.z).magnitude / speed);
        ClearState();
    }

    Vector3 ProjectOnContactPlane(Vector3 vector)
    {
        return vector - contactNormal * Vector3.Dot(vector, contactNormal);
    }

    void AdjustVelocity()
    {
        Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

        Vector3 adjustment = new Vector3();
        adjustment.x = desiredVelocity.x - Vector3.Dot(velocity, xAxis);
        adjustment.z = desiredVelocity.z - Vector3.Dot(velocity, zAxis);

        float _acceleration = OnGround ? acceleration : acceleration * maxAirAcceleration;
        float maxSpeedChange = _acceleration * Time.deltaTime;
        
        adjustment = Vector3.ClampMagnitude(adjustment, acceleration * Time.deltaTime);

        velocity += xAxis * adjustment.x + zAxis * adjustment.z;
    }

    void Jump()
    {
        Vector3 jumpDirection;
        if (OnGround) jumpDirection = contactNormal;
        else if (OnSteep)
        {
            jumpPhase = 0;
            jumpDirection = steepNormal;
        }
        else if (maxAirJumps > 0 && jumpPhase <= maxAirJumps)
        {
            if (jumpPhase == 0) jumpPhase = 1;
            jumpDirection = contactNormal;
        }
        else return;

        stepsSinceLastJump = 0;
        jumpPhase += 1;
        float jumpSpeed = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);

        jumpDirection = (jumpDirection + Vector3.up).normalized;
        float alignedSpeed = Vector3.Dot(velocity, jumpDirection);
        if (alignedSpeed > 0f)
        {
            jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
        }

        velocity += jumpDirection * jumpSpeed;

    }

    void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        EvaluateCollision(collision);
    }

    void EvaluateCollision(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;

            if (normal.y >= minGroundDotProduct)
            {
                groundContactCount += 1;
                contactNormal += normal;
            }
            else if (normal.y > -0.01f)
            {
                steepContactCount += 1;
                steepNormal += normal;
            }
        }
    }

    void ClearState()
    {
        groundContactCount = steepContactCount = 0;
        contactNormal = steepNormal = Vector3.zero;
    }

    void UpdateState()
    {
        stepsSinceLastGrounded += 1;
        stepsSinceLastJump += 1;
        velocity = rigidBody.velocity;
        if (OnGround || SnapToGround() || CheckSteepContacts())
        {
            stepsSinceLastGrounded = 0;
            if (stepsSinceLastJump > 1) jumpPhase = 0;
            if (groundContactCount > 1)
                contactNormal.Normalize();
        }
        else
            contactNormal = Vector3.up;

    }

    bool SnapToGround()
    {
        if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2)
            return false;

        float speed = velocity.magnitude;
        if (speed > maxSnapSpeed)
            return false;

        if (!Physics.Raycast(rigidBody.position, Vector3.down, out RaycastHit hit, probeDistance, probeMask))
        {
            if (hit.normal.y < minGroundDotProduct) return false;

            return false;
        }

        groundContactCount = 1;
        contactNormal = hit.normal;

        float dot = Vector3.Dot(velocity, hit.normal);
        if (dot > 0f) velocity = (velocity - hit.normal * dot).normalized * speed;
        return true;
    }

    bool CheckSteepContacts()
    {
        if (steepContactCount > 1)
        {
            steepNormal.Normalize();
            if (steepNormal.y >= minGroundDotProduct)
            {
                groundContactCount = 1;
                contactNormal = steepNormal;
                return true;
            }
        }
        return false;
    }
}
