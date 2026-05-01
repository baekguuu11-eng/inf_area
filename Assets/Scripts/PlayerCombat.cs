using System.Collections;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private Transform weaponPivot;
    [SerializeField] private GameObject weaponVisual;

    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackAngle = 120f;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackCooldown = 0.25f;
    [SerializeField] private float swingDuration = 0.12f;
    [SerializeField] private float swingArc = 100f;
    [SerializeField] private float attackOriginDistance = 0.6f;

    private bool isAttacking = false;
    private Vector2 lastAttackDirection = Vector2.down;

    public bool IsAttacking => isAttacking;
    public Vector2 AimDirection => lastAttackDirection;

    private void Awake()
    {
        Transform core = transform.Find("Core");

        if (core != null)
        {
            if (attackOrigin == null)
            {
                Transform found = core.Find("AttackOrigin");
                if (found != null)
                    attackOrigin = found;
            }

            if (weaponPivot == null)
            {
                Transform found = core.Find("WeaponPivot");
                if (found != null)
                    weaponPivot = found;
            }
        }

        if (weaponVisual == null && weaponPivot != null)
        {
            Transform found = weaponPivot.Find("WeaponVisual");
            if (found != null)
                weaponVisual = found.gameObject;
        }
    }

    private void Start()
    {
        if (weaponVisual != null)
            weaponVisual.SetActive(false);

        UpdateAttackOriginPosition();
        UpdateWeaponBaseRotation();
    }

    private void Update()
    {
        if (!isAttacking)
            UpdateAttackDirection();

        if (Input.GetKeyDown(KeyCode.X) && !isAttacking)
            StartCoroutine(DoAttack());
    }

    private void UpdateAttackDirection()
    {
        if (Input.GetKey(KeyCode.UpArrow))
            lastAttackDirection = Vector2.up;
        else if (Input.GetKey(KeyCode.DownArrow))
            lastAttackDirection = Vector2.down;
        else if (Input.GetKey(KeyCode.LeftArrow))
            lastAttackDirection = Vector2.left;
        else if (Input.GetKey(KeyCode.RightArrow))
            lastAttackDirection = Vector2.right;

        UpdateAttackOriginPosition();
        UpdateWeaponBaseRotation();
    }

    private void UpdateAttackOriginPosition()
    {
        if (attackOrigin != null)
            attackOrigin.localPosition = (Vector3)(lastAttackDirection * attackOriginDistance);
    }

    private void UpdateWeaponBaseRotation()
    {
        if (weaponPivot == null)
            return;

        float baseAngle = Mathf.Atan2(lastAttackDirection.y, lastAttackDirection.x) * Mathf.Rad2Deg;
        weaponPivot.localRotation = Quaternion.Euler(0f, 0f, baseAngle);
    }

    private IEnumerator DoAttack()
    {
        isAttacking = true;

        Vector2 lockedAttackDirection = lastAttackDirection.normalized;
        if (lockedAttackDirection == Vector2.zero)
            lockedAttackDirection = Vector2.down;

        float baseAngle = Mathf.Atan2(lockedAttackDirection.y, lockedAttackDirection.x) * Mathf.Rad2Deg;
        float startAngle = baseAngle - swingArc * 0.5f;
        float endAngle = baseAngle + swingArc * 0.5f;

        if (weaponVisual != null)
            weaponVisual.SetActive(true);

        DealDamage(lockedAttackDirection);

        float elapsed = 0f;

        while (elapsed < swingDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / swingDuration);
            float currentAngle = Mathf.Lerp(startAngle, endAngle, t);

            if (weaponPivot != null)
                weaponPivot.localRotation = Quaternion.Euler(0f, 0f, currentAngle);

            yield return null;
        }

        UpdateWeaponBaseRotation();

        if (weaponVisual != null)
            weaponVisual.SetActive(false);

        yield return new WaitForSeconds(attackCooldown);
        isAttacking = false;
    }

    private void DealDamage(Vector2 attackDirection)
    {
        if (attackOrigin == null)
        {
            Debug.LogWarning("AttackOrigin is missing.");
            return;
        }

        Collider2D[] targets = Physics2D.OverlapCircleAll(attackOrigin.position, attackRange);
     

        foreach (Collider2D target in targets)
        {
            Vector2 toTarget = ((Vector2)target.bounds.center - (Vector2)attackOrigin.position).normalized;
            float angle = Vector2.Angle(attackDirection, toTarget);

            if (angle > attackAngle * 0.5f)
                continue;

            EnemyHealth enemy = FindEnemyHealth(target);

            if (enemy != null)
            {
                enemy.TakeDamage(attackDamage, attackDirection);
            }
        }
    }

    private EnemyHealth FindEnemyHealth(Collider2D target)
    {
        if (target == null)
            return null;

        EnemyHealth enemy = target.GetComponent<EnemyHealth>();
        if (enemy != null)
            return enemy;

        enemy = target.GetComponentInParent<EnemyHealth>();
        if (enemy != null)
            return enemy;

        Transform root = target.transform.root;
        if (root != null)
        {
            enemy = root.GetComponentInChildren<EnemyHealth>(true);
            if (enemy != null)
                return enemy;
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        if (attackOrigin == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(attackOrigin.position, attackRange);

        Vector3 center = attackOrigin.position;
        Vector3 dir = new Vector3(lastAttackDirection.x, lastAttackDirection.y, 0f);

        Quaternion leftRot = Quaternion.Euler(0f, 0f, -attackAngle * 0.5f);
        Quaternion rightRot = Quaternion.Euler(0f, 0f, attackAngle * 0.5f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(center, center + leftRot * dir * attackRange);
        Gizmos.DrawLine(center, center + rightRot * dir * attackRange);
    }
}