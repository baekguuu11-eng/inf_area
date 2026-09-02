using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponVisualController : MonoBehaviour
{
    [Header("Runtime Rig v5.5")]
    [SerializeField] private Transform weaponRig;
    [SerializeField] private Transform poseRoot;
    [SerializeField] private Transform kickRoot;
    [SerializeField] private Transform artRoot;
    [SerializeField] private Transform trailRoot;
    [SerializeField] private Transform muzzleAnchor;
    [SerializeField] private Transform casingAnchor;
    [SerializeField] private Transform rearHandRoot;
    [SerializeField] private Transform frontHandRoot;
    [SerializeField] private Transform rearArmRoot;
    [SerializeField] private Transform rearForearmRoot;
    [SerializeField] private Transform frontArmRoot;
    [SerializeField] private Transform frontForearmRoot;
    [SerializeField] private SpriteRenderer weaponRenderer;
    [SerializeField] private SpriteRenderer trailRenderer;
    [SerializeField] private SpriteRenderer rearHandRenderer;
    [SerializeField] private SpriteRenderer frontHandRenderer;
    [SerializeField] private SpriteRenderer rearArmRenderer;
    [SerializeField] private SpriteRenderer rearForearmRenderer;
    [SerializeField] private SpriteRenderer frontArmRenderer;
    [SerializeField] private SpriteRenderer frontForearmRenderer;
    [SerializeField] private MeleeArcTrail meleeArcTrail;

    [Header("Debug")]
    [SerializeField] private bool drawGripDebug;
    [SerializeField] private bool drawTipAndMuzzleDebug;

    private WeaponDefinition currentWeapon;
    private Vector2 aimDirection = Vector2.down;
    private WeaponAimCardinal cardinal = WeaponAimCardinal.Down;
    private SpriteRenderer playerBodyRenderer;
    private bool fireFrame;
    private bool aimLocked;
    private bool meleeAttacking;
    private Vector2 lockedAimDirection;
    private WeaponAimCardinal lockedCardinal;
    private float currentForwardAngle;
    private Vector2 currentGripOffset;
    private WeaponDefinition cachedHandWeapon;
    private Sprite cachedRearHandSprite;
    private Sprite cachedFrontHandSprite;

    private static readonly Dictionary<string, Sprite> handSpriteCache = new Dictionary<string, Sprite>();
    private static Sprite armSprite;

    public Transform Pivot { get { EnsureHierarchy(); return poseRoot; } }
    public SpriteRenderer WeaponRenderer { get { EnsureHierarchy(); return weaponRenderer; } }
    public SpriteRenderer TrailRenderer { get { EnsureHierarchy(); return trailRenderer; } }
    public Vector2 AimDirection => aimLocked ? lockedAimDirection : aimDirection;
    public WeaponAimCardinal CardinalDirection => aimLocked ? lockedCardinal : cardinal;
    public WeaponDefinition CurrentWeapon => currentWeapon;
    public float PlayerWorldHeight => ResolvePlayerWorldHeight();

    private void Awake()
    {
        EnsureHierarchy();
        ResolvePlayerBodyRenderer();
    }

    private void OnDisable()
    {
        if (meleeArcTrail != null)
            meleeArcTrail.CancelImmediate();
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.F2)) drawGripDebug = !drawGripDebug;
        if (Input.GetKeyDown(KeyCode.F3)) drawTipAndMuzzleDebug = !drawTipAndMuzzleDebug;
#endif
    }

    private void OnValidate()
    {
        // 편집 중 Inspector 변경만으로 Scene Hierarchy가 자동 생성되는 문제를 막는다.
        // 누락된 런타임 Rig는 Awake에서 안전하게 복구한다.
        if (!Application.isPlaying)
            ResolvePlayerBodyRenderer();
    }

    public static WeaponAimCardinal CardinalFromVector(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            return direction.x >= 0f ? WeaponAimCardinal.Right : WeaponAimCardinal.Left;
        return direction.y >= 0f ? WeaponAimCardinal.Up : WeaponAimCardinal.Down;
    }

    public static Vector2 CardinalVector(WeaponAimCardinal direction)
    {
        switch (direction)
        {
            case WeaponAimCardinal.Up: return Vector2.up;
            case WeaponAimCardinal.Left: return Vector2.left;
            case WeaponAimCardinal.Down: return Vector2.down;
            default: return Vector2.right;
        }
    }

    public static float CardinalAngle(WeaponAimCardinal direction)
    {
        switch (direction)
        {
            case WeaponAimCardinal.Up: return 90f;
            case WeaponAimCardinal.Left: return 180f;
            case WeaponAimCardinal.Down: return -90f;
            default: return 0f;
        }
    }

    public void SetWeapon(WeaponDefinition weapon, Vector2 direction)
    {
        if (!EnsureHierarchy()) return;

        currentWeapon = weapon;
        meleeAttacking = false;
        if (direction.sqrMagnitude > 0.0001f)
            SetDirection(direction, true);

        ShowTrail(false);
        ApplyFrame(false);

        if (currentWeapon == null)
        {
            if (rearHandRenderer != null) rearHandRenderer.enabled = false;
            if (frontHandRenderer != null) frontHandRenderer.enabled = false;
            if (rearArmRenderer != null) rearArmRenderer.enabled = false;
            if (rearForearmRenderer != null) rearForearmRenderer.enabled = false;
            if (frontArmRenderer != null) frontArmRenderer.enabled = false;
            if (frontForearmRenderer != null) frontForearmRenderer.enabled = false;
        }
        else
        {
            ApplyHoldPose();
        }

        SetWeaponKick(0f);
    }

    public void UpdateAim(Vector2 direction)
    {
        if (!EnsureHierarchy() || aimLocked || meleeAttacking) return;
        if (direction.sqrMagnitude > 0.0001f)
            SetDirection(direction, false);
        ApplyHoldPose();
    }

    public void LockAim(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            direction = aimDirection;

        lockedCardinal = CardinalFromVector(direction);
        lockedAimDirection = CardinalVector(lockedCardinal);
        aimLocked = true;
        cardinal = lockedCardinal;
        aimDirection = lockedAimDirection;
        ApplyHoldPose();
    }

    public void UnlockAim()
    {
        aimLocked = false;
        cardinal = lockedCardinal;
        aimDirection = lockedAimDirection;
        ApplyHoldPose();
    }

    public void BeginMeleeAttack(Vector2 direction)
    {
        LockAim(direction);
        meleeAttacking = true;
        ApplyHoldPose();
    }

    public void SetMeleeWindupProgress(float normalized)
    {
        if (currentWeapon == null || !EnsureHierarchy()) return;
        DirectionalWeaponPose pose = currentWeapon.GetPose(CardinalDirection);
        float t = Smooth01(normalized);
        ApplyPoseValues(
            Mathf.Lerp(pose.holdForwardAngle, pose.attackStartForwardAngle, t),
            pose.gripOffsetPlayerRatio + Vector2.Lerp(Vector2.zero, pose.attackGripStartOffsetPlayerRatio, t));
    }

    public void SetMeleeActiveProgress(float normalized)
    {
        if (currentWeapon == null || !EnsureHierarchy()) return;
        DirectionalWeaponPose pose = currentWeapon.GetPose(CardinalDirection);
        float t = Smooth01(normalized);
        ApplyPoseValues(
            Mathf.Lerp(pose.attackStartForwardAngle, pose.attackEndForwardAngle, t),
            pose.gripOffsetPlayerRatio + Vector2.Lerp(pose.attackGripStartOffsetPlayerRatio, pose.attackGripEndOffsetPlayerRatio, t));

        if (meleeArcTrail != null && meleeArcTrail.IsEmitting)
            meleeArcTrail.Sample(GetGripWorldPosition(), GetDisplayedAttackTipWorldPosition());
    }

    public void SetMeleeRecoveryProgress(float normalized)
    {
        if (currentWeapon == null || !EnsureHierarchy()) return;
        DirectionalWeaponPose pose = currentWeapon.GetPose(CardinalDirection);
        float t = Smooth01(normalized);
        ApplyPoseValues(
            Mathf.Lerp(pose.attackEndForwardAngle, pose.holdForwardAngle, t),
            pose.gripOffsetPlayerRatio + Vector2.Lerp(pose.attackGripEndOffsetPlayerRatio, Vector2.zero, t));
    }

    public void EndMeleeAttack()
    {
        meleeAttacking = false;
        ApplyHoldPose();
    }

    // 이전 코드 호환용. v5.5에서는 공격 전체를 절대 방향 Pose로 처리한다.
    public void SetSwingAngle(float angleOffset)
    {
        if (currentWeapon == null) return;
        DirectionalWeaponPose pose = currentWeapon.GetPose(CardinalDirection);
        ApplyPoseValues(pose.holdForwardAngle + angleOffset, pose.gripOffsetPlayerRatio);
    }

    public void SetSwingProgress(float progress)
    {
        SetMeleeActiveProgress(progress);
    }

    public void ResetSwing()
    {
        meleeAttacking = false;
        ApplyHoldPose();
    }

    public void SetFireFrame(bool firing)
    {
        fireFrame = firing;
        ApplyFrame(firing);
        if (!meleeAttacking) ApplyHoldPose();
    }

    public void SetWeaponKick(float normalizedKick)
    {
        if (!EnsureHierarchy()) return;
        if (currentWeapon == null)
        {
            kickRoot.localPosition = Vector3.zero;
            return;
        }

        float playerHeight = ResolvePlayerWorldHeight();
        float worldKick = playerHeight * currentWeapon.spriteKickPlayerRatio * Mathf.Clamp01(normalizedKick);
        kickRoot.localPosition = Vector3.left * worldKick;

        // 반동 중 손과 무기만 움직이고 팔이 남는 현상을 막는다.
        ApplyArms(playerHeight);
    }

    public void ShowTrail(bool visible)
    {
        if (!EnsureHierarchy()) return;

        // 기존 대형 PNG 한 장 방식은 축소 과정에서 참격이 조각나 보였다.
        // v6.0부터 실제 무기 끝점의 이동을 추적하는 연속형 TrailRenderer를 사용한다.
        if (trailRenderer != null)
            trailRenderer.enabled = false;

        if (!visible)
        {
            if (meleeArcTrail != null)
                meleeArcTrail.End();
            return;
        }

        if (meleeArcTrail == null || currentWeapon == null || currentWeapon.category != WeaponCategory.Melee)
            return;

        ResolvePlayerBodyRenderer();
        int sortingLayer = playerBodyRenderer != null ? playerBodyRenderer.sortingLayerID : 0;
        int sortingOrder = (playerBodyRenderer != null ? Mathf.Max(2, playerBodyRenderer.sortingOrder) : 5) + 5;
        meleeArcTrail.Begin(currentWeapon, ResolvePlayerWorldHeight(), sortingLayer, sortingOrder,
            GetGripWorldPosition(), GetDisplayedAttackTipWorldPosition());
    }

    public void SetTrailAlpha(float alpha)
    {
        if (!EnsureHierarchy()) return;
        if (meleeArcTrail != null)
            meleeArcTrail.SetAlpha(alpha);
    }

    public Vector2 GetMuzzleWorldPosition()
    {
        EnsureHierarchy();
        return muzzleAnchor != null ? (Vector2)muzzleAnchor.position : (Vector2)transform.position;
    }


    public Vector2 GetCasingEjectWorldPosition()
    {
        EnsureHierarchy();
        return casingAnchor != null ? (Vector2)casingAnchor.position : GetMuzzleWorldPosition();
    }

    public Vector2 GetCasingEjectWorldDirection()
    {
        Vector2 forward = AimDirection.sqrMagnitude > 0.0001f ? AimDirection.normalized : Vector2.right;
        if (Mathf.Abs(forward.x) >= Mathf.Abs(forward.y))
            return (Vector2.up - forward * 0.34f).normalized;

        Vector2 side = new Vector2(-forward.y, forward.x);
        float sideSign = CardinalDirection == WeaponAimCardinal.Down ? -1f : 1f;
        return (side * sideSign - forward * 0.18f).normalized;
    }

    public Vector2 GetMeleeOrigin()
    {
        EnsureHierarchy();
        return poseRoot != null ? (Vector2)poseRoot.position : (Vector2)transform.position;
    }

    public Vector2 GetGripWorldPosition()
    {
        return GetMeleeOrigin();
    }

    public Vector2 GetAttackTipWorldPosition()
    {
        if (!EnsureHierarchy() || weaponRenderer == null || weaponRenderer.sprite == null || currentWeapon == null)
            return GetGripWorldPosition() + AimDirection * ResolvePlayerWorldHeight() * 0.45f;

        Vector2 local = SpritePoint(weaponRenderer.sprite, currentWeapon.attackTipNormalized);
        return weaponRenderer.transform.TransformPoint(local);
    }

    public Vector2 GetAttackPointWorld(float normalizedReach)
    {
        Vector2 grip = GetGripWorldPosition();
        Vector2 tip = GetAttackTipWorldPosition();
        // V9: debug_whip은 스프라이트 자체를 실제 타격 거리만큼 늘린다.
        // 따라서 채찍만큼은 추가 reach multiplier를 다시 곱하지 않아 실제 판정 거리는 V8과 동일하게 유지한다.
        float multiplier = currentWeapon != null && currentWeapon.weaponId != "debug_whip"
            ? Mathf.Max(0.2f, currentWeapon.visibleReachMultiplier)
            : 1f;
        return Vector2.LerpUnclamped(grip, grip + (tip - grip) * multiplier, Mathf.Clamp01(normalizedReach));
    }

    private Vector2 GetDisplayedAttackTipWorldPosition()
    {
        // 디버그 채찍의 판정은 visibleReachMultiplier를 사용해 실제 스프라이트 끝보다 멀리까지 뻗는다.
        // 시각 효과도 동일한 끝점을 사용해 '보이는 범위 = 실제 타격 범위'가 되게 한다.
        if (currentWeapon != null && currentWeapon.weaponId == "debug_whip")
            return GetAttackPointWorld(1f);
        return GetAttackTipWorldPosition();
    }

    private void SetDirection(Vector2 direction, bool force)
    {
        if (aimLocked && !force) return;
        WeaponAimCardinal resolved = CardinalFromVector(direction);
        cardinal = resolved;
        aimDirection = CardinalVector(resolved);
        if (aimLocked)
        {
            lockedCardinal = resolved;
            lockedAimDirection = aimDirection;
        }
    }

    private void ApplyHoldPose()
    {
        if (currentWeapon == null || !EnsureHierarchy()) return;
        DirectionalWeaponPose pose = currentWeapon.GetPose(CardinalDirection);
        ApplyPoseValues(pose.holdForwardAngle, pose.gripOffsetPlayerRatio);
    }

    private void ApplyPoseValues(float forwardAngle, Vector2 gripOffsetPlayerRatio)
    {
        if (currentWeapon == null || !EnsureHierarchy()) return;

        DirectionalWeaponPose pose = currentWeapon.GetPose(CardinalDirection);
        float playerHeight = ResolvePlayerWorldHeight();
        currentForwardAngle = forwardAngle;
        currentGripOffset = gripOffsetPlayerRatio;

        Vector2 resolvedGripOffset = ResolvePresentationGripOffset(gripOffsetPlayerRatio, currentWeapon, CardinalDirection);
        poseRoot.localPosition = resolvedGripOffset * playerHeight;
        poseRoot.localRotation = Quaternion.Euler(0f, 0f, forwardAngle);
        artRoot.localRotation = Quaternion.Euler(0f, 0f, -currentWeapon.artForwardAngle);

        // 반대 수평 방향을 단순 180도 회전하면 총 손잡이와 윗면까지 뒤집힌다.
        // 원본 총구 방향과 조준 방향이 정반대인 경우 로컬 Y를 함께 반전해
        // 실제로는 수평 미러링된 것처럼 보이도록 한다.
        bool autoMirrorY = currentWeapon.category == WeaponCategory.Ranged &&
                           ShouldAutoMirrorArtY(currentWeapon.artForwardAngle, CardinalDirection);
        bool finalFlipY = pose.flipArtY ^ autoMirrorY;
        artRoot.localScale = new Vector3(pose.flipArtX ? -1f : 1f, finalFlipY ? -1f : 1f, 1f);

        ApplyFrame(fireFrame);
        ApplyHands(pose, playerHeight, finalFlipY);
        UpdateSorting();
    }

    private void ApplyFrame(bool firing)
    {
        if (!EnsureHierarchy() || weaponRenderer == null) return;

        Sprite sprite = currentWeapon == null
            ? null
            : (firing && currentWeapon.fireSprite != null ? currentWeapon.fireSprite : currentWeapon.idleSprite);

        weaponRenderer.sprite = sprite;
        weaponRenderer.enabled = sprite != null;
        if (sprite == null || currentWeapon == null) return;

        DirectionalWeaponPose pose = currentWeapon.GetPose(CardinalDirection);
        float ratio = pose.visualLengthPlayerRatio > 0.01f ? pose.visualLengthPlayerRatio : currentWeapon.visualLengthPlayerRatio;
        float targetBodyLength = ResolvePlayerWorldHeight() * Mathf.Max(0.05f, ratio);
        // V9: 디버그 채찍은 별도 사거리 표시선을 그리는 대신 실제 채찍 스프라이트 자체를
        // 기존 판정 끝점까지 확대한다. visibleReachMultiplier는 이제 '보이는 채찍 길이'에만 사용된다.
        if (currentWeapon.weaponId == "debug_whip")
            targetBodyLength *= Mathf.Max(1f, currentWeapon.visibleReachMultiplier);
        float worldBodyLengthAtScaleOne = currentWeapon.GetBodyPixelLength(firing) / Mathf.Max(1f, sprite.pixelsPerUnit);
        float scale = targetBodyLength / Mathf.Max(0.0001f, worldBodyLengthAtScaleOne);
        weaponRenderer.transform.localScale = Vector3.one * scale;

        Vector2 gripNormalized = currentWeapon.GetGripNormalized(firing) + pose.gripNormalizedOffset;
        Vector2 grip = SpritePoint(sprite, gripNormalized);
        weaponRenderer.transform.localPosition =
            -(Vector3)(grip * scale) +
            (Vector3)(pose.spriteOffsetPlayerRatio * ResolvePlayerWorldHeight());

        Vector2 muzzle = SpritePoint(sprite, currentWeapon.GetMuzzleNormalized(firing));
        muzzleAnchor.localPosition = weaponRenderer.transform.localPosition + (Vector3)(muzzle * scale);

        Vector2 ejectNormalized = ResolveEjectNormalized(currentWeapon, firing);
        Vector2 eject = SpritePoint(sprite, ejectNormalized);
        casingAnchor.localPosition = weaponRenderer.transform.localPosition + (Vector3)(eject * scale);
    }

    private void ApplyHands(DirectionalWeaponPose pose, float playerHeight, bool mirrorLocalY)
    {
        if (rearHandRenderer == null || frontHandRenderer == null || currentWeapon == null) return;

        if (!Application.isPlaying)
        {
            rearHandRenderer.enabled = false;
            frontHandRenderer.enabled = false;
            if (rearArmRenderer != null) rearArmRenderer.enabled = false;
            if (rearForearmRenderer != null) rearForearmRenderer.enabled = false;
            if (frontArmRenderer != null) frontArmRenderer.enabled = false;
            if (frontForearmRenderer != null) frontForearmRenderer.enabled = false;
            return;
        }

        if (cachedHandWeapon != currentWeapon || cachedRearHandSprite == null || cachedFrontHandSprite == null)
        {
            cachedHandWeapon = currentWeapon;
            cachedRearHandSprite = GetHandSprite(currentWeapon, false);
            cachedFrontHandSprite = GetHandSprite(currentWeapon, true);
        }

        rearHandRenderer.sprite = cachedRearHandSprite;
        frontHandRenderer.sprite = cachedFrontHandSprite;

        rearHandRenderer.enabled = pose.showRearHand && rearHandRenderer.sprite != null;
        frontHandRenderer.enabled = pose.showFrontHand && frontHandRenderer.sprite != null;

        if (rearHandRenderer.enabled)
            ApplyHandTransform(rearHandRoot, rearHandRenderer, ResolveHandOffset(pose, false), pose.rearHandScale,
                pose.rearHandRotation, playerHeight, mirrorLocalY);
        if (frontHandRenderer.enabled)
            ApplyHandTransform(frontHandRoot, frontHandRenderer, ResolveHandOffset(pose, true), pose.frontHandScale,
                pose.frontHandRotation, playerHeight, mirrorLocalY);

        ApplyArms(playerHeight);
    }

    private void ApplyHandTransform(Transform root, SpriteRenderer renderer, Vector2 localOffsetRatio,
        Vector2 scaleMultiplier, float rotation, float playerHeight, bool mirrorLocalY)
    {
        Vector2 resolvedOffset = localOffsetRatio;
        if (mirrorLocalY)
            resolvedOffset.y = -resolvedOffset.y;

        root.localPosition = resolvedOffset * playerHeight;
        root.localRotation = Quaternion.Euler(0f, 0f, mirrorLocalY ? -rotation : rotation);

        float styleMultiplier = 1f;
        if (currentWeapon.handStyle == WeaponHandStyle.HeavyTwoHand) styleMultiplier = 1.12f;
        else if (currentWeapon.handStyle == WeaponHandStyle.WhipGrip) styleMultiplier = 0.92f;

        float desiredWidth = Mathf.Max(0.018f, currentWeapon.handBaseSizePlayerRatio * playerHeight * styleMultiplier);
        float widthAtOne = renderer.sprite != null ? Mathf.Max(0.0001f, renderer.sprite.bounds.size.x) : 1f;
        float uniformScale = desiredWidth / widthAtOne;
        root.localScale = new Vector3(
            uniformScale * Mathf.Max(0.1f, scaleMultiplier.x),
            uniformScale * Mathf.Max(0.1f, scaleMultiplier.y),
            1f);

        renderer.color = Color.white;
    }

    private void ApplyArms(float playerHeight)
    {
        // v6.1.1: 라파엘의 비인간형 디자인에는 몸에서 길게 이어지는 팔보다
        // 무기 손잡이에 붙어 함께 움직이는 독립형 픽셀 손이 더 자연스럽다.
        // 기존 Arm Root는 하위 호환을 위해 남기되 항상 숨긴다.
        DisableArmRenderer(rearArmRenderer);
        DisableArmRenderer(rearForearmRenderer);
        DisableArmRenderer(frontArmRenderer);
        DisableArmRenderer(frontForearmRenderer);
    }

    private static void DisableArmRenderer(SpriteRenderer renderer)
    {
        if (renderer != null)
            renderer.enabled = false;
    }

    private void ApplyTwoSegmentArm(Transform upperRoot, SpriteRenderer upperRenderer,
        Transform forearmRoot, SpriteRenderer forearmRenderer, Transform handRoot, bool visible,
        Vector2 shoulderLocal, bool front, float playerHeight)
    {
        if (upperRoot == null || upperRenderer == null || forearmRoot == null || forearmRenderer == null ||
            handRoot == null || weaponRig == null || currentWeapon == null)
            return;

        upperRenderer.enabled = visible;
        forearmRenderer.enabled = visible;
        if (!visible) return;

        Sprite arm = GetArmSprite();
        upperRenderer.sprite = arm;
        forearmRenderer.sprite = arm;
        upperRenderer.color = currentWeapon.handFillColor;
        forearmRenderer.color = currentWeapon.handFillColor;

        Vector2 handLocal = weaponRig.InverseTransformPoint(handRoot.position);
        Vector2 delta = handLocal - shoulderLocal;
        if (delta.sqrMagnitude < 0.000004f)
        {
            upperRenderer.enabled = false;
            forearmRenderer.enabled = false;
            return;
        }

        Vector2 perpendicular = new Vector2(-delta.y, delta.x).normalized;
        float bendSign = ResolveElbowBendSign(CardinalDirection, front);
        float bendAmount = playerHeight * (front ? 0.032f : 0.026f);
        Vector2 elbow = Vector2.Lerp(shoulderLocal, handLocal, front ? 0.48f : 0.52f) + perpendicular * bendSign * bendAmount;

        ApplyArmSegment(upperRoot, upperRenderer, shoulderLocal, elbow, playerHeight);
        ApplyArmSegment(forearmRoot, forearmRenderer, elbow, handLocal, playerHeight);
    }

    private void ApplyArmSegment(Transform root, SpriteRenderer renderer, Vector2 from, Vector2 to, float playerHeight)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.002f)
        {
            renderer.enabled = false;
            return;
        }

        root.localPosition = (from + to) * 0.5f;
        root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        float baseWidth = renderer.sprite != null ? Mathf.Max(0.0001f, renderer.sprite.bounds.size.x) : 1f;
        float baseHeight = renderer.sprite != null ? Mathf.Max(0.0001f, renderer.sprite.bounds.size.y) : 1f;
        float thickness = Mathf.Max(0.010f, currentWeapon.handBaseSizePlayerRatio * playerHeight * 0.30f);
        root.localScale = new Vector3(length / baseWidth, thickness / baseHeight, 1f);
    }

    private static float ResolveElbowBendSign(WeaponAimCardinal direction, bool front)
    {
        switch (direction)
        {
            case WeaponAimCardinal.Left: return front ? -1f : 1f;
            case WeaponAimCardinal.Up: return front ? -1f : 1f;
            case WeaponAimCardinal.Down: return front ? 1f : -1f;
            default: return front ? 1f : -1f;
        }
    }

    private static Vector2 ResolveShoulderOffset(WeaponAimCardinal direction, bool front)
    {
        switch (direction)
        {
            case WeaponAimCardinal.Left:
                return front ? new Vector2(-0.018f, -0.040f) : new Vector2(0.030f, -0.055f);
            case WeaponAimCardinal.Up:
                return front ? new Vector2(0.036f, -0.015f) : new Vector2(-0.036f, -0.026f);
            case WeaponAimCardinal.Down:
                return front ? new Vector2(0.036f, -0.052f) : new Vector2(-0.036f, -0.058f);
            default:
                return front ? new Vector2(0.018f, -0.040f) : new Vector2(-0.030f, -0.055f);
        }
    }

    private Vector2 ResolveHandOffset(DirectionalWeaponPose pose, bool front)
    {
        Vector2 offset = front ? pose.frontHandOffsetPlayerRatio : pose.rearHandOffsetPlayerRatio;
        if (currentWeapon == null || currentWeapon.category != WeaponCategory.Ranged)
            return offset;

        string id = string.IsNullOrEmpty(currentWeapon.weaponId) ? string.Empty : currentWeapon.weaponId.ToLowerInvariant();
        bool pistol = id.Contains("pistol");
        bool shotgun = id.Contains("shotgun") || currentWeapon.rangedMode == RangedAttackMode.Shotgun;
        bool laser = id.Contains("laser") || currentWeapon.rangedMode == RangedAttackMode.HitscanBeam;

        if (!front)
        {
            offset.y = Mathf.Min(offset.y, -0.010f);
            offset.x += pistol ? 0f : -0.006f;
            return offset;
        }

        if (pistol)
            return new Vector2(0f, -0.008f);

        float supportDistance = shotgun ? 0.155f : laser ? 0.145f : 0.135f;
        offset.x = Mathf.Max(Mathf.Abs(offset.x), supportDistance) * (offset.x < 0f ? -1f : 1f);
        if (Mathf.Abs(offset.x) < 0.001f)
            offset.x = supportDistance;
        offset.y = Mathf.Min(offset.y, -0.012f);
        return offset;
    }

    private static Vector2 ResolvePresentationGripOffset(Vector2 authored, WeaponDefinition weapon, WeaponAimCardinal direction)
    {
        if (weapon == null || weapon.category != WeaponCategory.Ranged)
            return authored;

        string id = string.IsNullOrEmpty(weapon.weaponId) ? string.Empty : weapon.weaponId.ToLowerInvariant();
        bool pistol = id.Contains("pistol");
        bool shotgun = id.Contains("shotgun") || weapon.rangedMode == RangedAttackMode.Shotgun;
        bool longGun = !pistol;

        switch (direction)
        {
            case WeaponAimCardinal.Left:
                return new Vector2(-(pistol ? 0.050f : shotgun ? 0.024f : 0.028f),
                    pistol ? -0.072f : shotgun ? -0.088f : -0.084f);
            case WeaponAimCardinal.Up:
                // 위 조준에서도 무기가 눈 중앙을 관통하지 않도록 몸 뒤쪽·아래쪽에 안착시킨다.
                return new Vector2(0f, pistol ? -0.020f : -0.040f);
            case WeaponAimCardinal.Down:
                return new Vector2(0f, pistol ? -0.118f : -0.128f);
            default:
                return new Vector2(pistol ? 0.050f : shotgun ? 0.024f : 0.028f,
                    pistol ? -0.072f : shotgun ? -0.088f : -0.084f);
        }
    }

    private static Vector2 ResolveEjectNormalized(WeaponDefinition weapon, bool firing)
    {
        Vector2 grip = weapon.GetGripNormalized(firing);
        Vector2 muzzle = weapon.GetMuzzleNormalized(firing);
        Vector2 eject = Vector2.Lerp(grip, muzzle, 0.24f);
        eject.y = Mathf.Clamp01(eject.y + 0.26f);
        return eject;
    }

    private static Sprite GetHandSprite(WeaponDefinition weapon, bool front)
    {
        if (weapon == null) return null;

        string key = ((int)weapon.handStyle).ToString() + (front ? "_F_" : "_R_") +
                     ColorUtility.ToHtmlStringRGBA(weapon.handOutlineColor) + "_" +
                     ColorUtility.ToHtmlStringRGBA(weapon.handFillColor) + "_" +
                     ColorUtility.ToHtmlStringRGBA(weapon.handHighlightColor);

        Sprite sprite;
        if (handSpriteCache.TryGetValue(key, out sprite) && sprite != null)
            return sprite;

        sprite = CreateHandSprite(weapon.handStyle, front, weapon.handOutlineColor,
            weapon.handFillColor, weapon.handHighlightColor);
        handSpriteCache[key] = sprite;
        return sprite;
    }

    private static Sprite CreateHandSprite(WeaponHandStyle style, bool front, Color outline, Color fill, Color highlight)
    {
        int width;
        int height;

        switch (style)
        {
            case WeaponHandStyle.HeavyTwoHand:
                width = front ? 9 : 8;
                height = 6;
                break;
            case WeaponHandStyle.WhipGrip:
                width = front ? 7 : 6;
                height = 4;
                break;
            case WeaponHandStyle.TwoHand:
                width = front ? 8 : 7;
                height = 5;
                break;
            default:
                width = front ? 7 : 6;
                height = 5;
                break;
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "IA_V60_HandTexture";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        for (int y = 0; y < height; y++)
        {
            int inset = (y == 0 || y == height - 1) ? 1 : 0;
            for (int x = inset; x < width - inset; x++)
            {
                bool border = x == inset || x == width - inset - 1 || y == 0 || y == height - 1;
                pixels[y * width + x] = border ? outline : fill;
            }
        }

        if (width > 3 && height > 2)
        {
            pixels[(height - 2) * width + 1] = highlight;
            if (width > 7) pixels[(height - 2) * width + 2] = highlight;
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        Sprite result = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 16f);
        result.name = "IA_V60_" + style + (front ? "_SupportHand" : "_GripHand");
        return result;
    }

    private static Sprite GetArmSprite()
    {
        if (armSprite != null) return armSprite;

        Texture2D texture = new Texture2D(4, 2, TextureFormat.RGBA32, false);
        texture.name = "IA_V60_ArmTexture";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(new[]
        {
            Color.white, Color.white, Color.white, Color.white,
            Color.white, Color.white, Color.white, Color.white
        });
        texture.Apply(false, true);

        armSprite = Sprite.Create(texture, new Rect(0, 0, 4, 2), new Vector2(0.5f, 0.5f), 16f);
        armSprite.name = "IA_V60_ArmSprite";
        return armSprite;
    }

    private static bool ShouldAutoMirrorArtY(float artForwardAngle, WeaponAimCardinal target)
    {
        if (target != WeaponAimCardinal.Left && target != WeaponAimCardinal.Right)
            return false;

        float normalized = Mathf.Repeat(artForwardAngle, 360f);
        WeaponAimCardinal source = Mathf.Abs(Mathf.DeltaAngle(normalized, 180f)) <
                                   Mathf.Abs(Mathf.DeltaAngle(normalized, 0f))
            ? WeaponAimCardinal.Left
            : WeaponAimCardinal.Right;

        return source != target;
    }

    private void UpdateSorting()
    {
        if (!EnsureHierarchy()) return;
        ResolvePlayerBodyRenderer();

        DirectionalWeaponPose pose = currentWeapon != null ? currentWeapon.GetPose(CardinalDirection) : null;
        int bodyOrder = playerBodyRenderer != null ? Mathf.Max(2, playerBodyRenderer.sortingOrder) : 5;

        if (weaponRenderer != null)
        {
            if (playerBodyRenderer != null) weaponRenderer.sortingLayerID = playerBodyRenderer.sortingLayerID;
            weaponRenderer.sortingOrder = bodyOrder + (pose != null ? pose.weaponSortingOffset : 2);
        }

        if (rearHandRenderer != null)
        {
            if (playerBodyRenderer != null) rearHandRenderer.sortingLayerID = playerBodyRenderer.sortingLayerID;
            rearHandRenderer.sortingOrder = bodyOrder + (pose != null ? pose.rearHandSortingOffset : 1);
        }

        if (frontHandRenderer != null)
        {
            if (playerBodyRenderer != null) frontHandRenderer.sortingLayerID = playerBodyRenderer.sortingLayerID;
            frontHandRenderer.sortingOrder = bodyOrder + (pose != null ? pose.frontHandSortingOffset : 3);
        }

        if (rearArmRenderer != null)
        {
            if (playerBodyRenderer != null) rearArmRenderer.sortingLayerID = playerBodyRenderer.sortingLayerID;
            rearArmRenderer.sortingOrder = bodyOrder;
        }

        if (frontArmRenderer != null)
        {
            if (playerBodyRenderer != null) frontArmRenderer.sortingLayerID = playerBodyRenderer.sortingLayerID;
            frontArmRenderer.sortingOrder = bodyOrder + 1;
        }

        if (rearForearmRenderer != null)
        {
            if (playerBodyRenderer != null) rearForearmRenderer.sortingLayerID = playerBodyRenderer.sortingLayerID;
            rearForearmRenderer.sortingOrder = bodyOrder;
        }

        if (frontForearmRenderer != null)
        {
            if (playerBodyRenderer != null) frontForearmRenderer.sortingLayerID = playerBodyRenderer.sortingLayerID;
            frontForearmRenderer.sortingOrder = bodyOrder + 1;
        }

        if (trailRenderer != null)
        {
            if (playerBodyRenderer != null) trailRenderer.sortingLayerID = playerBodyRenderer.sortingLayerID;
            trailRenderer.sortingOrder = bodyOrder + 5;
        }
    }

    private float ResolvePlayerWorldHeight()
    {
        ResolvePlayerBodyRenderer();
        if (playerBodyRenderer != null && playerBodyRenderer.sprite != null)
            return Mathf.Max(0.3f, playerBodyRenderer.bounds.size.y);
        return 0.8f;
    }

    private void ResolvePlayerBodyRenderer()
    {
        if (playerBodyRenderer != null) return;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        float bestArea = -1f;
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || renderer == weaponRenderer || renderer == trailRenderer ||
                renderer == rearHandRenderer || renderer == frontHandRenderer || renderer.sprite == null)
                continue;

            string lower = renderer.gameObject.name.ToLowerInvariant();
            if (lower.Contains("weapon") || lower.Contains("trail") || lower.Contains("shadow") ||
                lower.Contains("hand") || lower.Contains("grip") || lower.Contains("arm"))
                continue;

            float area = renderer.bounds.size.x * renderer.bounds.size.y;
            if (area > bestArea)
            {
                bestArea = area;
                playerBodyRenderer = renderer;
            }
        }

        if (playerBodyRenderer != null && playerBodyRenderer.sortingOrder < 2)
            playerBodyRenderer.sortingOrder = 2;
    }

    private bool EnsureHierarchy()
    {
        weaponRig = ResolveTransform(weaponRig, transform, "WeaponRigV55");
        poseRoot = ResolveTransform(poseRoot, weaponRig, "PoseRoot");
        kickRoot = ResolveTransform(kickRoot, poseRoot, "KickRoot");
        artRoot = ResolveTransform(artRoot, kickRoot, "ArtRoot");
        trailRoot = ResolveTransform(trailRoot, weaponRig, "TrailRoot");
        rearHandRoot = ResolveTransform(rearHandRoot, kickRoot, "RearHandRoot");
        frontHandRoot = ResolveTransform(frontHandRoot, kickRoot, "FrontHandRoot");
        rearArmRoot = ResolveTransform(rearArmRoot, weaponRig, "RearUpperArmRoot");
        rearForearmRoot = ResolveTransform(rearForearmRoot, weaponRig, "RearForearmRoot");
        frontArmRoot = ResolveTransform(frontArmRoot, weaponRig, "FrontUpperArmRoot");
        frontForearmRoot = ResolveTransform(frontForearmRoot, weaponRig, "FrontForearmRoot");

        bool weaponCreated;
        weaponRenderer = ResolveSpriteRenderer(artRoot, "WeaponRenderer", out weaponCreated);

        bool trailCreated;
        trailRenderer = ResolveSpriteRenderer(trailRoot, "MeleeTrailRenderer", out trailCreated);
        if (trailRenderer != null && trailCreated) trailRenderer.enabled = false;

        bool rearCreated;
        rearHandRenderer = ResolveSpriteRenderer(rearHandRoot, "RearHandRenderer", out rearCreated);
        if (rearHandRenderer != null && rearCreated) rearHandRenderer.enabled = false;

        bool frontCreated;
        frontHandRenderer = ResolveSpriteRenderer(frontHandRoot, "FrontHandRenderer", out frontCreated);
        if (frontHandRenderer != null && frontCreated) frontHandRenderer.enabled = false;

        bool rearArmCreated;
        rearArmRenderer = ResolveSpriteRenderer(rearArmRoot, "RearArmRenderer", out rearArmCreated);
        if (rearArmRenderer != null && rearArmCreated) rearArmRenderer.enabled = false;

        bool rearForearmCreated;
        rearForearmRenderer = ResolveSpriteRenderer(rearForearmRoot, "RearForearmRenderer", out rearForearmCreated);
        if (rearForearmRenderer != null && rearForearmCreated) rearForearmRenderer.enabled = false;

        bool frontArmCreated;
        frontArmRenderer = ResolveSpriteRenderer(frontArmRoot, "FrontArmRenderer", out frontArmCreated);
        if (frontArmRenderer != null && frontArmCreated) frontArmRenderer.enabled = false;

        bool frontForearmCreated;
        frontForearmRenderer = ResolveSpriteRenderer(frontForearmRoot, "FrontForearmRenderer", out frontForearmCreated);
        if (frontForearmRenderer != null && frontForearmCreated) frontForearmRenderer.enabled = false;

        Transform proceduralTrailRoot = ResolveTransform(null, weaponRig, "ProceduralMeleeTrail");
        if (proceduralTrailRoot != null)
        {
            meleeArcTrail = proceduralTrailRoot.GetComponent<MeleeArcTrail>();
            if (meleeArcTrail == null && Application.isPlaying)
                meleeArcTrail = proceduralTrailRoot.gameObject.AddComponent<MeleeArcTrail>();
        }

        muzzleAnchor = ResolveTransform(muzzleAnchor, artRoot, "MuzzleAnchor");
        casingAnchor = ResolveTransform(casingAnchor, artRoot, "CasingEjectAnchor");

        return weaponRig != null && poseRoot != null && kickRoot != null && artRoot != null &&
               trailRoot != null && rearHandRoot != null && frontHandRoot != null &&
               rearArmRoot != null && rearForearmRoot != null && frontArmRoot != null && frontForearmRoot != null &&
               weaponRenderer != null && trailRenderer != null && rearHandRenderer != null &&
               frontHandRenderer != null && rearArmRenderer != null && rearForearmRenderer != null &&
               frontArmRenderer != null && frontForearmRenderer != null &&
               muzzleAnchor != null && casingAnchor != null && (meleeArcTrail != null || !Application.isPlaying);
    }

    private static Transform ResolveTransform(Transform serializedReference, Transform parent, string childName)
    {
        if (parent == null) return null;
        if (serializedReference != null && serializedReference.parent == parent && serializedReference.name == childName)
            return serializedReference;

        Transform child = parent.Find(childName);
        if (child != null) return child;

        GameObject gameObject = new GameObject(childName);
        gameObject.transform.SetParent(parent, false);
        return gameObject.transform;
    }

    private static SpriteRenderer ResolveSpriteRenderer(Transform parent, string childName, out bool created)
    {
        created = false;
        Transform child = ResolveTransform(null, parent, childName);
        if (child == null) return null;

        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = child.gameObject.AddComponent<SpriteRenderer>();
            created = true;
        }
        return renderer;
    }

    private static Vector2 SpritePoint(Sprite sprite, Vector2 normalized)
    {
        Bounds bounds = sprite.bounds;
        return new Vector2(
            Mathf.Lerp(bounds.min.x, bounds.max.x, Mathf.Clamp01(normalized.x)),
            Mathf.Lerp(bounds.min.y, bounds.max.y, Mathf.Clamp01(normalized.y)));
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private void OnDrawGizmos()
    {
        if (!drawGripDebug && !drawTipAndMuzzleDebug) return;

        if (drawGripDebug)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(GetGripWorldPosition(), 0.025f);
            if (rearHandRoot != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(rearHandRoot.position, 0.018f);
            }
            if (frontHandRoot != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(frontHandRoot.position, 0.018f);
            }
        }

        if (drawTipAndMuzzleDebug)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(GetAttackTipWorldPosition(), 0.022f);
            Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
            Gizmos.DrawSphere(GetMuzzleWorldPosition(), 0.022f);
            Gizmos.DrawLine(GetGripWorldPosition(), GetAttackTipWorldPosition());
        }
    }
}
