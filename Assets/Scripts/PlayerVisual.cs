using UnityEngine;

public class PlayerVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    private PlayerMovement playerMovement;
    private PlayerCombat playerCombat;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (animator == null)
            animator = GetComponent<Animator>();

        FindReferences();
    }

    private void Update()
    {
        if (playerMovement == null || playerCombat == null)
            FindReferences();

        UpdateFacing();
        UpdateAnimation();
    }

    private void FindReferences()
    {
        Transform root = transform.parent;
        if (root == null)
            return;

        playerMovement = root.GetComponent<PlayerMovement>();
        playerCombat = root.GetComponent<PlayerCombat>();

        if (playerMovement == null)
        {
            Transform core = root.Find("Core");
            if (core != null)
                playerMovement = core.GetComponent<PlayerMovement>();
        }

        if (playerCombat == null)
        {
            Transform core = root.Find("Core");
            if (core != null)
                playerCombat = core.GetComponent<PlayerCombat>();
        }
    }

    private void UpdateFacing()
    {
        if (spriteRenderer == null)
            return;

        Vector2 faceDirection = Vector2.zero;

        // 이동 중엔 이동 방향 우선
        if (playerMovement != null && playerMovement.MoveInput != Vector2.zero)
            faceDirection = playerMovement.MoveInput;
        else if (playerCombat != null && playerCombat.AimDirection != Vector2.zero)
            faceDirection = playerCombat.AimDirection;

        if (faceDirection.x < 0f)
            spriteRenderer.flipX = false;
        else if (faceDirection.x > 0f)
            spriteRenderer.flipX = true;
    }

    private void UpdateAnimation()
    {
        if (animator == null || playerMovement == null)
            return;

        float speed = playerMovement.MoveInput.sqrMagnitude > 0.001f ? 1f : 0f;
        animator.SetFloat("Speed", speed);
    }
}