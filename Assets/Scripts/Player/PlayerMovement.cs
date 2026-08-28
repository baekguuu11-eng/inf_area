using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 42f;
    [SerializeField] private float deceleration = 55f;

    private Rigidbody2D rb;
    private PlayerStats stats;
    private PlayerDashController dash;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LastMoveDirection { get; private set; } = Vector2.down;
    public Vector2 CurrentVelocity { get { return rb != null ? rb.linearVelocity : Vector2.zero; } }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<PlayerStats>();
        dash = GetComponent<PlayerDashController>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Update()
    {
        if (GameInputState.IsLocked)
        {
            MoveInput = Vector2.zero;
            return;
        }

        float x = 0f;
        float y = 0f;
        if (Input.GetKey(KeyCode.A)) x -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;
        if (Input.GetKey(KeyCode.W)) y += 1f;
        if (Input.GetKey(KeyCode.S)) y -= 1f;

        MoveInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        if (MoveInput.sqrMagnitude > 0.0001f)
            LastMoveDirection = MoveInput.normalized;
    }

    private void FixedUpdate()
    {
        if (dash == null)
            dash = GetComponent<PlayerDashController>();
        if (dash != null && dash.IsDashing)
            return;

        float finalSpeed = stats != null ? stats.MoveSpeed : Mathf.Max(0.1f, moveSpeed);
        Vector2 targetVelocity = GameInputState.IsLocked ? Vector2.zero : MoveInput * finalSpeed;
        float rate = targetVelocity.sqrMagnitude > rb.linearVelocity.sqrMagnitude ? acceleration : deceleration;

        rb.linearVelocity = Vector2.MoveTowards(
            rb.linearVelocity,
            targetVelocity,
            Mathf.Max(0f, rate) * Time.fixedDeltaTime);

        if (GameInputState.IsLocked && rb.linearVelocity.sqrMagnitude < 0.0004f)
            rb.linearVelocity = Vector2.zero;
    }
}
