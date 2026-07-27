using System.Collections;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private Transform weaponPivot;
    [SerializeField] private GameObject weaponVisual;

    [Header("Melee Attack Settings (근접 공격)")]
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackAngle = 120f;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackCooldown = 0.25f; // 근접 공격 연사 속도
    [SerializeField] private float swingDuration = 0.12f;
    [SerializeField] private float swingArc = 100f;
    [SerializeField] private float attackOriginDistance = 0.6f;

    [Header("Ranged Attack Settings (원거리 공격)")]
    [SerializeField] private GameObject bulletPrefab;          // 총알 프리팹
    [SerializeField] private float bulletSpeed = 12f;          // 총알 속도
    [SerializeField] private float rangedAttackCooldown = 0.15f;// 원거리 공격 연사 속도

    [Header("Weapon Mode State")]
    [SerializeField] private bool isRangedMode = false;        // Tab 키로 무기 전환

    private bool isAttacking = false;
    private Vector2 lastAttackDirection = Vector2.down;
    private CameraRecoil cameraRecoil;

    public bool IsAttacking => isAttacking;
    public Vector2 AimDirection => lastAttackDirection;
    public bool IsRangedMode => isRangedMode;

    private void Awake()
    {
        Transform core = transform.Find("Core");

        if (core != null)
        {
            if (attackOrigin == null)
            {
                Transform found = core.Find("AttackOrigin");
                if (found != null) attackOrigin = found;
            }

            if (weaponPivot == null)
            {
                Transform found = core.Find("WeaponPivot");
                if (found != null) weaponPivot = found;
            }
        }

        if (weaponVisual == null && weaponPivot != null)
        {
            Transform found = weaponPivot.Find("WeaponVisual");
            if (found != null) weaponVisual = found.gameObject;
        }
    }

    private void Start()
    {
        if (weaponVisual != null)
            weaponVisual.SetActive(false);

        UpdateAttackOriginPosition();
        UpdateWeaponBaseRotation();

        if (Camera.main != null)
        {
            cameraRecoil = Camera.main.GetComponent<CameraRecoil>();
        }
    }

    private void Update()
    {
        if (GameInputState.IsLocked)
        {
            if (!isAttacking && weaponVisual != null)
                weaponVisual.SetActive(false);

            return;
        }

        // Tab 키로 근접/원거리 무기 전환
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            isRangedMode = !isRangedMode;
            Debug.Log($"무기 전환: {(isRangedMode ? "원거리 모드" : "근접 모드")}");
        }

        // 화살표 키를 꾹 누르고 있으면 continuous 공격
        if (isRangedMode)
        {
            HandleRangedInput();
        }
        else
        {
            HandleMeleeInput();
        }
    }

    private void HandleMeleeInput()
    {
        if (isAttacking) return;

        // GetKey를 사용하여 누르고 있는 동안 지속적으로 감지
        Vector2 attackDir = GetArrowInputDirection();
        if (attackDir != Vector2.zero)
        {
            lastAttackDirection = attackDir;
            UpdateAttackOriginPosition();
            UpdateWeaponBaseRotation();

            StartCoroutine(DoAttack(attackDir));
        }
    }

    private void HandleRangedInput()
    {
        if (isAttacking) return;

        // GetKey를 사용하여 누르고 있는 동안 지속적으로 감지
        Vector2 fireDir = GetArrowInputDirection();
        if (fireDir != Vector2.zero)
        {
            lastAttackDirection = fireDir;
            UpdateAttackOriginPosition();
            UpdateWeaponBaseRotation();

            StartCoroutine(DoRangedAttack(fireDir));
        }
    }

    // GetKeyDown -> GetKey로 변경하여 꾹 누르는 입력 지원
    private Vector2 GetArrowInputDirection()
    {
        if (Input.GetKey(KeyCode.UpArrow)) return Vector2.up;
        if (Input.GetKey(KeyCode.DownArrow)) return Vector2.down;
        if (Input.GetKey(KeyCode.LeftArrow)) return Vector2.left;
        if (Input.GetKey(KeyCode.RightArrow)) return Vector2.right;
        return Vector2.zero;
    }

    private void UpdateAttackOriginPosition()
    {
        if (attackOrigin != null)
            attackOrigin.localPosition = (Vector3)(lastAttackDirection * attackOriginDistance);
    }

    private void UpdateWeaponBaseRotation()
    {
        if (weaponPivot == null) return;

        float baseAngle = Mathf.Atan2(lastAttackDirection.y, lastAttackDirection.x) * Mathf.Rad2Deg;
        weaponPivot.localRotation = Quaternion.Euler(0f, 0f, baseAngle);
    }

    private IEnumerator DoAttack(Vector2 attackDirection)
    {
        isAttacking = true;

        Vector2 lockedAttackDirection = attackDirection.normalized;
        if (lockedAttackDirection == Vector2.zero) lockedAttackDirection = Vector2.down;

        float baseAngle = Mathf.Atan2(lockedAttackDirection.y, lockedAttackDirection.x) * Mathf.Rad2Deg;
        float startAngle = baseAngle - swingArc * 0.5f;
        float endAngle = baseAngle + swingArc * 0.5f;

        if (weaponVisual != null) weaponVisual.SetActive(true);

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

        if (weaponVisual != null) weaponVisual.SetActive(false);

        yield return new WaitForSeconds(attackCooldown);
        isAttacking = false;
    }

    private IEnumerator DoRangedAttack(Vector2 attackDir)
    {
        isAttacking = true;

        Vector3 spawnPos = attackOrigin != null ? attackOrigin.position : transform.position;

        if (bulletPrefab != null)
        {
            GameObject bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
            PlayerProjectile projectile = bullet.GetComponent<PlayerProjectile>();
            if (projectile != null)
            {
                projectile.Setup(attackDir, bulletSpeed, attackDamage);
            }
        }

        if (cameraRecoil != null)
        {
            cameraRecoil.TriggerRecoil(attackDir);
        }

        yield return new WaitForSeconds(rangedAttackCooldown);
        isAttacking = false;
    }

    private void DealDamage(Vector2 attackDirection)
    {
        if (attackOrigin == null) return;

        Collider2D[] targets = Physics2D.OverlapCircleAll(attackOrigin.position, attackRange);

        foreach (Collider2D target in targets)
        {
            Vector2 toTarget = ((Vector2)target.bounds.center - (Vector2)attackOrigin.position).normalized;
            float angle = Vector2.Angle(attackDirection, toTarget);

            if (angle > attackAngle * 0.5f) continue;

            EnemyHealth enemy = FindEnemyHealth(target);
            if (enemy != null)
            {
                enemy.TakeDamage(attackDamage, attackDirection);
            }
        }
    }

    private EnemyHealth FindEnemyHealth(Collider2D target)
    {
        if (target == null) return null;

        EnemyHealth enemy = target.GetComponent<EnemyHealth>();
        if (enemy != null) return enemy;

        enemy = target.GetComponentInParent<EnemyHealth>();
        if (enemy != null) return enemy;

        Transform root = target.transform.root;
        if (root != null)
        {
            enemy = root.GetComponentInChildren<EnemyHealth>(true);
            if (enemy != null) return enemy;
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        if (attackOrigin == null) return;

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