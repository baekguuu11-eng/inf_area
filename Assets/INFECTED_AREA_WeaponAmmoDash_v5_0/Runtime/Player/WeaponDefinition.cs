using System;
using UnityEngine;

public enum WeaponCategory { Melee, Ranged }
public enum WeaponFireMode { SemiAutomatic, Automatic }
public enum RangedAttackMode { Projectile, Shotgun, HitscanBeam }
public enum MeleeHitShape { Sector, Capsule, ImpactCircle, Box }
public enum WeaponAimCardinal { Right, Up, Left, Down }
public enum WeaponHandStyle { OneHand, TwoHand, HeavyTwoHand, WhipGrip }

[Serializable]
public sealed class DirectionalWeaponPose
{
    [Header("Grip / Hold")]
    [Tooltip("플레이어 로컬 좌표에서 실제 손잡이가 놓일 위치. 플레이어 높이를 1로 본 비율.")]
    public Vector2 gripOffsetPlayerRatio = new Vector2(0.11f, -0.01f);
    [Tooltip("공격하지 않을 때 무기 진행축(+X)이 향하는 절대 각도.")]
    public float holdForwardAngle;
    [Tooltip("공격 시작 순간 무기 진행축의 절대 각도.")]
    public float attackStartForwardAngle = 45f;
    [Tooltip("공격 종료 순간 무기 진행축의 절대 각도.")]
    public float attackEndForwardAngle = -35f;
    [Tooltip("공격 준비 지점에서 손잡이가 이동할 추가 위치.")]
    public Vector2 attackGripStartOffsetPlayerRatio;
    [Tooltip("공격 종료 지점에서 손잡이가 이동할 추가 위치.")]
    public Vector2 attackGripEndOffsetPlayerRatio;

    [Header("Sprite")]
    public bool flipArtX;
    public bool flipArtY;
    public int weaponSortingOffset = 2;
    [Min(0.05f)] public float visualLengthPlayerRatio = 0.45f;
    public Vector2 gripNormalizedOffset;
    public Vector2 spriteOffsetPlayerRatio;

    [Header("Hands - weapon-forward local axes")]
    public bool showRearHand;
    public bool showFrontHand = true;
    [Tooltip("무기 진행축(+X) 기준 뒷손 위치.")]
    public Vector2 rearHandOffsetPlayerRatio;
    [Tooltip("무기 진행축(+X) 기준 앞손 위치.")]
    public Vector2 frontHandOffsetPlayerRatio;
    public Vector2 rearHandScale = Vector2.one;
    public Vector2 frontHandScale = Vector2.one;
    public float rearHandRotation;
    public float frontHandRotation;
    public int rearHandSortingOffset = 1;
    public int frontHandSortingOffset = 3;

    [Header("Trail")]
    [Tooltip("참격 이미지가 향해야 하는 절대 공격 방향.")]
    public float trailForwardAngle;
    public Vector2 trailOffsetPlayerRatio;
    [Min(0.1f)] public float trailScaleMultiplier = 1f;

    // v5.3/v5.4 에디터 도구와 직렬화 호환용. v5.5 런타임에서는 사용하지 않는다.
    [HideInInspector] public Vector2 handOffsetPlayerRatio;
    [HideInInspector] public float baseRotation;
    [HideInInspector] public bool flipX;
    [HideInInspector] public bool flipY;
    [HideInInspector] public int sortingOffset;
    [HideInInspector] public Vector2 spriteOffsetPlayerRatioLegacy;
    [HideInInspector] public float swingStartAngle;
    [HideInInspector] public float swingEndAngle;
    [HideInInspector] public float trailRotationOffset;
}

[CreateAssetMenu(menuName = "INFECTED AREA/Weapon Definition", fileName = "WeaponDefinition")]
public sealed class WeaponDefinition : ScriptableObject
{
    [Header("Identity")]
    public string weaponId;
    public string displayName;
    public string shortTrait;
    public WeaponCategory category;
    public bool defaultOwned;
    public int shopPrice = 20;

    [Header("Visual Assets")]
    public Sprite idleSprite;
    public Sprite fireSprite;
    public Sprite projectileSprite;
    public Sprite trailSprite;
    public Color accentColor = new Color(0.25f, 0.95f, 1f, 1f);

    [Header("Legacy Visual Compatibility")]
    [Min(0.05f)] public float visualWorldLength = 0.5f;
    [Min(0.05f)] public float fireLengthMultiplier = 1f;
    [Min(0f)] public float visualDistanceFromPlayer = 0.1f;
    public float visualAngleOffset;
    [Min(0.01f)] public float fireSpriteDuration = 0.055f;

    [Header("Sprite Geometry")]
    [Tooltip("원본 Sprite에서 총구/칼끝 방향이 향하는 각도. +X=0, +Y=90, -X=180.")]
    public float artForwardAngle;
    public Vector2 idleGripNormalized = new Vector2(0.5f, 0.1f);
    public Vector2 fireGripNormalized = new Vector2(0.5f, 0.1f);
    public Vector2 idleMuzzleNormalized = new Vector2(1f, 0.5f);
    public Vector2 fireMuzzleNormalized = new Vector2(1f, 0.5f);
    public Vector2 attackTipNormalized = new Vector2(0.5f, 0.98f);
    [Min(0.05f)] public float visualLengthPlayerRatio = 0.45f;
    [Min(1f)] public float idleBodyPixelLength = 32f;
    [Min(1f)] public float fireBodyPixelLength = 32f;
    [Min(0f)] public float spriteKickPlayerRatio = 0.025f;

    [Header("Four Direction Poses v5.5")]
    public DirectionalWeaponPose rightPose = new DirectionalWeaponPose();
    public DirectionalWeaponPose upPose = new DirectionalWeaponPose();
    public DirectionalWeaponPose leftPose = new DirectionalWeaponPose();
    public DirectionalWeaponPose downPose = new DirectionalWeaponPose();

    [Header("Slime Hand Presentation")]
    public WeaponHandStyle handStyle = WeaponHandStyle.OneHand;
    [Min(0.025f)] public float handBaseSizePlayerRatio = 0.072f;
    public Color handOutlineColor = new Color(0.015f, 0.055f, 0.08f, 1f);
    public Color handFillColor = new Color(0.25f, 0.82f, 0.94f, 1f);
    public Color handHighlightColor = new Color(0.62f, 0.95f, 1f, 1f);

    [Header("Melee Visual / Sweep Match")]
    public MeleeHitShape meleeHitShape = MeleeHitShape.Sector;
    [Min(0f)] public float meleeStartOffset = 0.08f;
    [Min(0.05f)] public float meleeReach = 0.72f;
    [Min(0.03f)] public float meleeWidth = 0.45f;
    [Min(0.05f)] public float trailLengthPlayerRatio = 0.82f;
    [Min(1f)] public float trailBodyPixelLength = 128f;
    public float trailArtForwardAngle;
    public float trailCenterDistance = 0.34f;
    public Vector2 trailLocalOffset;
    [Min(0.02f)] public float sweepThicknessPlayerRatio = 0.12f;
    [Min(0.2f)] public float visibleReachMultiplier = 1f;
    [Min(0f)] public float impactRadiusPlayerRatio = 0.16f;

    [Header("Shared Combat")]
    [Min(1)] public int damage = 20;
    [Min(0.03f)] public float attackInterval = 0.45f;
    [Min(0.02f)] public float minimumAttackInterval = 0.08f;
    [Min(0f)] public float knockbackBonus = 0.1f;
    [Min(0f)] public float cameraKick = 0.08f;
    [Min(0f)] public float cameraShake = 0.02f;

    [Header("Melee Timing")]
    [Min(0.01f)] public float meleeWindup = 0.08f;
    [Min(0.01f)] public float meleeActiveTime = 0.08f;
    [Min(0f)] public float meleeRecovery = 0.3f;
    [Min(0.1f)] public float meleeRange = 0.8f;
    [Range(10f, 360f)] public float meleeAngle = 105f;
    [Min(0.05f)] public float trailWorldLength = 0.8f;
    [Min(0.01f)] public float trailDuration = 0.1f;
    [Min(0f)] public int burnDamagePerTick;
    [Min(0)] public int burnTicks;
    [Min(0.01f)] public float burnTickInterval = 0.4f;

    [Header("Ranged")]
    public WeaponFireMode fireMode = WeaponFireMode.SemiAutomatic;
    public RangedAttackMode rangedMode = RangedAttackMode.Projectile;
    [Min(1)] public int magazineShotCapacity = 12;
    [Min(1)] public int ammoCostPerShot = 1;
    [Min(0.1f)] public float reloadDuration = 1f;
    [Min(0.1f)] public float projectileSpeed = 12f;
    [Tooltip("Gameplay range is unlimited. This is only a technical cleanup lifetime for projectiles that never hit anything.")]
    [Min(0.1f)] public float projectileLifetime = 8f;
    [Min(0f)] public float falloffStartDistance = 5f;
    [Tooltip("No longer a hard range limit. Damage reaches minimumDamageMultiplier at this distance.")]
    [Min(0.1f)] public float maximumRange = 16f;
    [Range(0.05f, 1f)] public float minimumDamageMultiplier = 0.7f;
    [Min(0)] public int pierceCount;
    [Min(1)] public int pelletCount = 1;
    [Range(0f, 50f)] public float spreadDegrees;
    [Min(0.01f)] public float projectileWorldLength = 0.14f;
    [Min(0.01f)] public float projectileWorldThickness = 0.055f;

    [Header("Projectile Visibility")]
    public Color projectileCoreColor = new Color(0.98f, 1f, 1f, 1f);
    public Color projectileGlowColor = new Color(0.15f, 0.9f, 1f, 0.55f);
    [Min(1f)] public float projectileGlowMultiplier = 1.8f;
    [Min(0f)] public float projectileTrailDuration = 0.05f;
    public float projectileArtForwardAngle;

    [Header("Beam")]
    [Min(0.1f)] public float beamRange = 10f;
    [Min(0.005f)] public float beamCoreWidth = 0.03f;
    [Min(0.005f)] public float beamGlowWidth = 0.11f;
    [Min(0.01f)] public float beamDuration = 0.08f;
    [Min(1)] public int beamMaxTargets = 4;
    [Range(0.1f, 1f)] public float beamPerTargetMultiplier = 0.82f;

    [Header("Balance Targets")]
    [Min(0.1f)] public float targetBasicEnemyTtk = 1.15f;
    [Min(0.1f)] public float targetTankTtk = 8.5f;

    [Header("Debug")]
    public KeyCode debugHotkey = KeyCode.None;

    // 이전 버전이 참조할 수 있는 호환 필드.
    [HideInInspector] public bool mirrorForOppositeHorizontal;
    [HideInInspector] public float holdDistancePlayerRatio;
    [HideInInspector] public float handSideOffsetPlayerRatio;
    [HideInInspector] public bool showGripSocket;
    [HideInInspector] public float gripSocketScalePlayerRatio;
    [HideInInspector] public Vector2 gripSocketOffsetPlayerRatio;
    [HideInInspector] public Color gripSocketColor;
    [HideInInspector] public float meleeSwingStartAngle;
    [HideInInspector] public float meleeSwingEndAngle;
    [HideInInspector] public float trailAngleOffset;

    public int MagazineEnergyCapacity => Mathf.Max(1, magazineShotCapacity) * Mathf.Max(1, ammoCostPerShot);
    public bool UsesAmmo => category == WeaponCategory.Ranged;
    public Vector2 GetGripNormalized(bool fireFrame) => fireFrame && fireSprite != null ? fireGripNormalized : idleGripNormalized;
    public Vector2 GetMuzzleNormalized(bool fireFrame) => fireFrame && fireSprite != null ? fireMuzzleNormalized : idleMuzzleNormalized;
    public float GetBodyPixelLength(bool fireFrame) => fireFrame && fireSprite != null ? Mathf.Max(1f, fireBodyPixelLength) : Mathf.Max(1f, idleBodyPixelLength);

    public DirectionalWeaponPose GetPose(WeaponAimCardinal direction)
    {
        switch (direction)
        {
            case WeaponAimCardinal.Up: return upPose;
            case WeaponAimCardinal.Left: return leftPose;
            case WeaponAimCardinal.Down: return downPose;
            default: return rightPose;
        }
    }
}
