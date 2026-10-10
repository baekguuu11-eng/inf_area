using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public enum JHLPatternKind
{
    HandSlam,
    HandSweep,
    StraightLaser,
    FingerBarrage,
    DoubleTapSlam,
    SidePunch,
    TripleAimLaser,
    ProjectileFan,
    EnhancedSlam,
    EnhancedSweep,
    Compression,
    HandPrison,
    CrossSlam,
    TwinSlam,
    SlamChain,
    ScissorSweep,
    LaserSweep,
    CrossLaser,
    PrisonBarrage,
    CompressionBurst,
    MassiveCentralBeam,
    TrackingThinBeam,
    SplitBeam,
    LaserCurtain,
    SpiralBeam,
    CrossfireBarrage,
    SweepLaserCombo,
    QuadSlam,
    RapidLaserBurst,
    FinalCompression,
    MovingGate,
    DiagonalCrossPunch,
    PredictiveBombardment,
    BeamPinch,
    RoarRepulse,
    RemoteSuppression,
    FullAccess
}

[DisallowMultipleComponent]
public sealed partial class JHLBossController : MonoBehaviour, IEnemyDeathOverride
{
    private enum BossState { Dormant, Intro, Combat, Transition, DebugPattern, Dead }

    // V12: layered attack presets combine independent Hand / Face-Energy / Space
    // channels. This gives JHL actual simultaneous pressure instead of inflating
    // the pattern count with sequential variants of the same attack.
    private enum LayeredComboV12
    {
        SweepAim,
        CompressionFan,
        GateBombardment,
        ScissorRapidLaser,
        GateCurtain,
        CrossPunchSplit,
        CompressionTracking
    }

    private const int BossMaxHealth = 3000;
    private const float PhaseTwoRatio = 0.70f;
    private const float PhaseThreeRatio = 0.40f;
    private const float FullAccessRatio = 0.15f;

    private MapManager mapManager;
    private RoomController ownerRoom;
    private BoxCollider2D arenaBounds;
    private EnemyHealth health;
    private Transform visualRoot;
    private Transform face;
    private Transform leftHand;
    private Transform rightHand;
    private Transform faceFireOrigin;
    private Transform leftFingerTip;
    private Transform rightFingerTip;
    private BoxCollider2D faceHurtbox;
    private BoxCollider2D leftHurtbox;
    private BoxCollider2D rightHurtbox;
    private JHLHandBarrier leftHandBarrier;
    private JHLHandBarrier rightHandBarrier;
    private JHLPartMotion faceMotion;
    private JHLPartMotion leftMotion;
    private JHLPartMotion rightMotion;
    private Vector2 faceSize;
    private Vector2 handSize;
    private SpriteRenderer[] structureRenderers;
    private JHLCombatEffects combatEffects;
    private JHLPatternDirector patternDirector;
    private JHLBossAudio bossAudio;
    private JHLBossMusic bossMusic;
    private TrailRenderer leftTrail;
    private TrailRenderer rightTrail;

    private PlayerHealth playerHealth;
    private Transform player;
    private Rigidbody2D playerBody;
    private JHLBossHUD hud;
    private JHLCameraFX cameraFx;
    private JHLCameraDirector cameraDirector;
    private JHLCutsceneIsolation cutsceneIsolation;
    private IDisposable inputLock;
    private Coroutine combatRoutine;
    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private readonly Queue<JHLPatternKind> recentPatterns = new Queue<JHLPatternKind>();

    private BossState state = BossState.Dormant;
    private int phase = 1;
    private bool combatStarted;
    private bool dead;
    private bool fullAccessUsed;
    private bool suppressTransitions;
    private bool patternRunning;
    private JHLPatternKind currentPattern;
    private string lastPatternName = "-";
    private float skipAllowedAt;
    private JHLPatternKind debugSelectedPattern = JHLPatternKind.HandSlam;
    private bool debugForceSelectedPattern;

    private Vector3 faceHome;
    private Vector3 leftHome;
    private Vector3 rightHome;
    private float debugSafeWidth = -1f;
    private float staggerDamage;
    private float staggerWindowUntil;

    // Anti-pressure counter: sustained face damage forces the player to disengage.
    private float pressureWindowUntil;
    private float meleePressureDamage;
    private float rangedPressureDamage;
    private float antiPressureCooldownUntil;
    private bool antiPressurePending;
    private JHLPatternKind antiPressurePattern;
    private Coroutine antiPressureInterruptRoutine;
    private Coroutine auxiliaryPatternRoutine;
    private float lastLayoutAspect = -1f;
    private int lastLayeredComboV12 = -1;
    private Coroutine faceHitFeedbackRoutineV12;
    private float nextFaceHitFeedbackTimeV12;

    private JHLCombatV25 combatV25;
    public void InitializeV25(JHLCombatV25 combat)
    {
        combatV25 = combat; health = combat.Health; ownerRoom = combat.Room;
        arenaBounds = ownerRoom.EnemySpawnArea; face = combat.Face;
        leftHand = combat.Left; rightHand = combat.Right;
    }
    public EnemyHealth Health => health;
    public RoomController OwnerRoom => ownerRoom;
    public int Phase => combatV25 != null ? combatV25.Phase : phase;
    public bool IsDead => combatV25 != null ? combatV25.IsDead : dead;
    public string DebugStateName => state.ToString();
    public string DebugCurrentPatternName => combatV25 != null ? combatV25.Pattern : patternRunning ? currentPattern.ToString() : "-";
    public string DebugLastPatternName => lastPatternName;
    public string DebugSelectedPatternName => debugSelectedPattern.ToString();
    public BoxCollider2D ArenaBounds => arenaBounds;
    public Transform Face => face;
    public Transform LeftHand => leftHand;
    public Transform RightHand => rightHand;
    public JHLPartMotion FaceMotion => faceMotion;
    public JHLPartMotion LeftMotion => leftMotion;
    public JHLPartMotion RightMotion => rightMotion;
    public JHLCameraFX CameraFX => cameraFx;
    public Vector2 FaceSize => faceSize;
    public Vector2 HandSize => handSize;
    public string DebugPatternFamilyName => patternDirector != null ? patternDirector.LastFamily.ToString() : "-";
    public float DebugSafeWidth => debugSafeWidth;
    public Vector3 DebugFaceVelocity => faceMotion != null ? faceMotion.Velocity : Vector3.zero;
    public Vector3 DebugLeftVelocity => leftMotion != null ? leftMotion.Velocity : Vector3.zero;
    public Vector3 DebugRightVelocity => rightMotion != null ? rightMotion.Velocity : Vector3.zero;

    public void Initialize(
        MapManager manager,
        RoomController room,
        BoxCollider2D arena,
        EnemyHealth enemyHealth,
        Transform bossVisualRoot,
        Transform faceTransform,
        Transform leftHandTransform,
        Transform rightHandTransform,
        Transform faceOrigin,
        Transform leftTip,
        Transform rightTip,
        BoxCollider2D faceBox,
        BoxCollider2D leftBox,
        BoxCollider2D rightBox,
        JHLPartMotion facePartMotion,
        JHLPartMotion leftPartMotion,
        JHLPartMotion rightPartMotion,
        Vector2 faceWorldSize,
        Vector2 handWorldSize,
        SpriteRenderer[] renderers,
        JHLCombatEffects effects)
    {
        mapManager = manager;
        ownerRoom = room;
        arenaBounds = arena;
        health = enemyHealth;
        visualRoot = bossVisualRoot;
        face = faceTransform;
        leftHand = leftHandTransform;
        rightHand = rightHandTransform;
        faceFireOrigin = faceOrigin;
        leftFingerTip = leftTip;
        rightFingerTip = rightTip;
        faceHurtbox = faceBox;
        leftHurtbox = leftBox;
        rightHurtbox = rightBox;
        leftHandBarrier = leftHand != null ? leftHand.GetComponentInChildren<JHLHandBarrier>(true) : null;
        rightHandBarrier = rightHand != null ? rightHand.GetComponentInChildren<JHLHandBarrier>(true) : null;
        faceMotion = facePartMotion;
        leftMotion = leftPartMotion;
        rightMotion = rightPartMotion;
        faceSize = faceWorldSize;
        handSize = handWorldSize;
        structureRenderers = renderers ?? Array.Empty<SpriteRenderer>();
        combatEffects = effects;

        playerHealth = FindAnyObjectByType<PlayerHealth>();
        player = playerHealth != null ? playerHealth.transform : null;
        playerBody = player != null ? player.GetComponent<Rigidbody2D>() : null;
        // V16: JHL can be attacked continuously; no opening multiplier or bracket UI.
        hud = JHLBossHUD.Create();
        if (hud != null && ownerRoom != null)
            hud.transform.SetParent(ownerRoom.transform, false);
        cameraFx = JHLCameraFX.CreateOrGet();
        patternDirector = GetComponent<JHLPatternDirector>();
        if (patternDirector == null) patternDirector = gameObject.AddComponent<JHLPatternDirector>();
        bossAudio = JHLBossAudio.Create(transform);
        bossMusic = JHLBossMusic.Create(transform);
        cameraDirector = GetComponent<JHLCameraDirector>();
        if (cameraDirector == null) cameraDirector = gameObject.AddComponent<JHLCameraDirector>();
        cameraDirector.ConfigureRoomBounds(ownerRoom);
        lastLayoutAspect = Camera.main != null ? Camera.main.aspect : -1f;
        cutsceneIsolation = new JHLCutsceneIsolation();

        // V3: no persistent hand trails. The oversized white placeholders stay readable and heavy;
        // impact feedback comes from movement, telegraph and camera instead of smeary trails.
        leftTrail = null;
        rightTrail = null;

        if (health != null)
        {
            health.SetMaxHealth(BossMaxHealth, true);
            health.Damaged += OnBossDamaged;
        }

        SetCombatColliders(false);
        SetVisualAlpha(1f);
        RecalculateScreenHomes(1);
        SnapPartsOffscreen();
        if (hud != null) hud.ReportHealth(BossMaxHealth, BossMaxHealth, true);
    }

    private void LateUpdate()
    {
        if (combatV25 != null) return;
        if (dead || faceMotion == null || leftMotion == null || rightMotion == null) return;
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;
        if (lastLayoutAspect <= 0f) { lastLayoutAspect = cam.aspect; return; }
        if (Mathf.Abs(cam.aspect - lastLayoutAspect) < 0.02f) return;

        lastLayoutAspect = cam.aspect;
        float baseHalfHeight = cameraDirector != null && cameraDirector.CombatOrthoSize > 0.1f
            ? cameraDirector.CombatOrthoSize
            : cam.orthographicSize;
        float screenHeight = Mathf.Max(4f, baseHalfHeight * 2f);
        float screenWidth = Mathf.Max(6f, screenHeight * cam.aspect);
        if (RoomLayoutV24.TryGetBounds(ownerRoom, out Bounds fixedFrame))
        { screenWidth = fixedFrame.size.x; screenHeight = fixedFrame.size.y; }
        faceSize = new Vector2(screenWidth * 0.50f, screenHeight * 0.30f);
        handSize = new Vector2(screenWidth * 0.38f, screenHeight * 0.45f);

        faceMotion.SetBaseVisualScale(new Vector3(faceSize.x, faceSize.y, 1f));
        leftMotion.SetBaseVisualScale(new Vector3(handSize.x, handSize.y, 1f));
        rightMotion.SetBaseVisualScale(new Vector3(handSize.x, handSize.y, 1f));
        if (faceHurtbox != null) faceHurtbox.size = faceSize;
        if (leftHurtbox != null) leftHurtbox.size = handSize;
        if (rightHurtbox != null) rightHurtbox.size = handSize;
        if (leftHandBarrier != null) leftHandBarrier.ResizeEllipse(handSize);
        if (rightHandBarrier != null) rightHandBarrier.ResizeEllipse(handSize);
        if (faceFireOrigin != null) faceFireOrigin.localPosition = new Vector3(0f, -faceSize.y * 0.48f, 0f);
        if (leftFingerTip != null) leftFingerTip.localPosition = new Vector3(handSize.x * 0.42f, 0f, 0f);
        if (rightFingerTip != null) rightFingerTip.localPosition = new Vector3(-handSize.x * 0.42f, 0f, 0f);

        RecalculateScreenHomes(phase);
        if (state == BossState.Combat && !patternRunning)
            SetHomeTargets(JHLPartMotionState.Recovery);
    }

    private void OnDestroy()
    {
        if (combatV25 != null) return;
        if (health != null) health.Damaged -= OnBossDamaged;
        if (antiPressureInterruptRoutine != null) StopCoroutine(antiPressureInterruptRoutine);
        if (auxiliaryPatternRoutine != null) StopCoroutine(auxiliaryPatternRoutine);
        ReleaseInputLock();
        if (cutsceneIsolation != null) cutsceneIsolation.Restore();
        if (cameraDirector != null) cameraDirector.SnapToCombat();
        if (cameraFx != null) cameraFx.EndBoss();
        if (bossMusic != null && !dead) bossMusic.RestoreGameplayImmediate();
        CleanupSpawnedObjects();
        if (hud != null) Destroy(hud.gameObject);
    }

    public void BeginIntro()
    {
        if (combatV25 != null) { combatV25.Begin(); return; }
        if (dead || combatStarted || state != BossState.Dormant) return;
        StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        state = BossState.Intro;
        inputLock = GameInputState.Acquire("JHLIntro");
        if (bossMusic != null) bossMusic.PrepareIntro();
        SetCombatColliders(false);
        RecalculateScreenHomes(1);
        SnapPartsOffscreen();
        SetVisualAlpha(1f);

        if (hud != null)
        {
            hud.HideAllImmediate();
            hud.SetSkipVisible(true);
            StartCoroutine(hud.AnimateCinematicBars(true, 0.42f));
        }

        if (cutsceneIsolation != null && hud != null)
            cutsceneIsolation.Begin(hud.CanvasComponent, transform);

        if (cameraDirector != null)
        {
            cameraDirector.ConfigureRoomBounds(ownerRoom);
            cameraDirector.Capture();
        }
        if (cameraFx != null)
        {
            // CameraDirector owns framing during the intro; keep runtime zoom FX disabled
            // so the two systems never fight over orthographic size.
            cameraFx.EndBoss();
        }

        skipAllowedAt = Time.unscaledTime + 0.75f;
        bool skipRequested = false;
        Func<bool> skipCheck = () =>
        {
            if (skipRequested) return true;
            if (Time.unscaledTime >= skipAllowedAt && Input.anyKeyDown && !Input.GetKeyDown(KeyCode.Escape))
                skipRequested = true;
            return skipRequested;
        };

        float baseSize = cameraDirector != null ? Mathf.Max(0.1f, cameraDirector.CombatOrthoSize) : (Camera.main != null ? Camera.main.orthographicSize : 5.625f);
        Vector3 playerFocus = player != null ? player.position + new Vector3(0f, 0.15f, 0f) : transform.position;
        GetCameraFrame(out Vector3 screenCenter, out _, out float halfH);
        Vector3 upperFocus = screenCenter + Vector3.up * halfH * 0.27f;

        // 0.0~1.0: quiet player shot, like the existing Executor intro.
        if (cameraDirector != null)
            yield return cameraDirector.MoveTo(playerFocus, baseSize * 0.96f, 0.62f, skipCheck);
        if (skipRequested) { FinalizeIntroImmediate(); yield break; }
        yield return WaitUnscaled(0.28f);
        if (skipCheck()) { FinalizeIntroImmediate(); yield break; }

        // 1.0~2.4: the screen itself is occupied by the face, not a room-sized normal boss.
        faceMotion.SetMotionProfile(2.15f, 1.18f, 17f);
        faceMotion.SetScaleProfile(3.4f, 1.15f);
        faceMotion.SetScaleMultiplier(new Vector3(0.96f, 0.96f, 1f), true);
        faceMotion.SetTarget(faceHome + Vector3.up * 0.28f, JHLPartMotionState.Anticipation);
        if (bossAudio != null) bossAudio.PlayIntroFace();
        if (cameraDirector != null)
            yield return cameraDirector.MoveTo(upperFocus, baseSize * 1.045f, 0.92f, skipCheck);
        if (skipRequested) { FinalizeIntroImmediate(); yield break; }
        if (cameraFx != null) cameraFx.PulseLight(0.45f);
        if (cameraDirector != null) cameraDirector.Punch(Vector2.down, 0.10f, 0.025f);
        yield return WaitUnscaled(0.34f);
        if (skipCheck()) { FinalizeIntroImmediate(); yield break; }

        // 2.4~4.0: hands enter one at a time with deliberate heavy motion. No rubbery spin.
        leftMotion.SetMotionProfile(2.55f, 1.16f, 23f);
        rightMotion.SetMotionProfile(2.55f, 1.16f, 23f);
        leftMotion.SetRotationProfile(3.2f, 1.20f);
        rightMotion.SetRotationProfile(3.2f, 1.20f);
        leftMotion.SetTarget(leftHome + Vector3.left * handSize.x * 0.08f, JHLPartMotionState.Anticipation);
        if (bossAudio != null) bossAudio.PlayIntroHand();
        leftMotion.SetRotation(-5f, JHLPartMotionState.Anticipation);
        yield return WaitUnscaled(0.52f);
        if (skipCheck()) { FinalizeIntroImmediate(); yield break; }
        if (cameraDirector != null) cameraDirector.Punch(Vector2.right, 0.13f, 0.035f);

        rightMotion.SetTarget(rightHome + Vector3.right * handSize.x * 0.08f, JHLPartMotionState.Anticipation);
        if (bossAudio != null) bossAudio.PlayIntroHand();
        rightMotion.SetRotation(5f, JHLPartMotionState.Anticipation);
        yield return WaitUnscaled(0.52f);
        if (skipCheck()) { FinalizeIntroImmediate(); yield break; }
        if (cameraDirector != null) cameraDirector.Punch(Vector2.left, 0.13f, 0.035f);

        SetHomeTargets(JHLPartMotionState.Recovery);
        leftMotion.SetRotation(0f, JHLPartMotionState.Recovery);
        rightMotion.SetRotation(0f, JHLPartMotionState.Recovery);
        faceMotion.SetScaleMultiplier(Vector3.one);
        if (cameraFx != null) cameraFx.PulseTransition(1);
        CameraFeedbackController introFeedback = CameraFeedbackController.Instance;
        if (introFeedback != null) introFeedback.Impact(CameraImpactLevelV11.Heavy, Vector2.down, false);
        yield return WaitUnscaled(0.26f);
        if (skipCheck()) { FinalizeIntroImmediate(); yield break; }

        // 4.0~5.5: simple title reveal. No PHASE 1 banner.
        if (hud != null)
        {
            hud.ShowIntro("관리자 권한 응답 없음");
            yield return hud.TypeCinematicTitle("JHL", 0.16f, 0.55f, skipCheck);
            hud.ShowIntro(string.Empty);
        }
        if (skipRequested) { FinalizeIntroImmediate(); yield break; }

        if (cameraDirector != null)
            yield return cameraDirector.ReturnToCombat(0.62f, skipCheck);
        if (skipRequested) { FinalizeIntroImmediate(); yield break; }
        if (hud != null)
            yield return hud.AnimateCinematicBars(false, 0.30f);

        FinalizeIntroImmediate();
    }

    private void FinalizeIntroImmediate()
    {
        RecalculateScreenHomes(1);
        SnapPartsToHomes();
        SetVisualAlpha(1f);
        if (hud != null)
        {
            hud.ShowIntro(string.Empty);
            hud.ShowPhase(string.Empty);
            hud.HideCinematicTitle();
            hud.SetSkipVisible(false);
            hud.SetCinematicBarsImmediate(false);
            StartCoroutine(hud.RevealBossBar(health != null ? health.CurrentHealth : BossMaxHealth,
                health != null ? health.MaxHealth : BossMaxHealth, 0.48f));
        }
        if (cutsceneIsolation != null) cutsceneIsolation.Restore();
        if (cameraDirector != null) cameraDirector.SnapToCombat();
        if (cameraFx != null)
        {
            cameraFx.BeginBoss(1);
            cameraFx.SetPressure(0f);
            cameraFx.SetCharge(0f);
            cameraFx.SetFraming(0f);
        }
        ReleaseInputLock();
        SetCombatColliders(true);
        if (bossMusic != null) bossMusic.PlayPhase(1, 0.60f);
        combatStarted = true;
        state = BossState.Combat;
        if (combatRoutine != null) StopCoroutine(combatRoutine);
        combatRoutine = StartCoroutine(CombatLoop());
    }

    public void DebugCyclePattern(int delta)
    {
        if (combatV25 != null) { combatV25.DebugCycle(delta); return; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Array values = Enum.GetValues(typeof(JHLPatternKind));
        int count = values.Length;
        int current = (int)debugSelectedPattern;
        current = (current + delta) % count;
        if (current < 0) current += count;
        debugSelectedPattern = (JHLPatternKind)current;
#endif
    }

    public void DebugForcePattern()
    {
        if (combatV25 != null) { combatV25.DebugForce(); return; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (combatStarted && !dead) debugForceSelectedPattern = true;
#endif
    }

    public void DebugBeginCombatAtHealth(float normalizedHealth)
    {
        if (combatV25 != null) { combatV25.DebugStart(normalizedHealth); return; }
        if (dead) return;
        DebugResetCombat(normalizedHealth, normalizedHealth <= PhaseThreeRatio ? 3 : normalizedHealth <= PhaseTwoRatio ? 2 : 1);
    }

    public void DebugResetCombat(float normalizedHealth, int requestedPhase)
    {
        if (combatV25 != null) { combatV25.DebugStart(normalizedHealth); return; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (dead || health == null) return;
        StopAllCoroutines();
        patternRunning = false;
        CleanupSpawnedObjects();
        SetHandsSolid(false);
        ReleaseInputLock();
        if (cutsceneIsolation != null) cutsceneIsolation.Restore();
        if (cameraDirector != null) cameraDirector.SnapToCombat();
        health.ResetHealth();
        suppressTransitions = true;
        float clamped = Mathf.Clamp(normalizedHealth, 0.02f, 1f);
        int targetHealth = Mathf.Max(1, Mathf.RoundToInt(health.MaxHealth * clamped));
        int damage = health.CurrentHealth - targetHealth;
        if (damage > 0)
            health.TakeDamage(damage, Vector2.down, EnemyHitKind.Environment);
        suppressTransitions = false;
        phase = Mathf.Clamp(requestedPhase, 1, 3);
        fullAccessUsed = false;
        antiPressurePending = false;
        meleePressureDamage = 0f;
        rangedPressureDamage = 0f;
        pressureWindowUntil = 0f;
        antiPressureCooldownUntil = 0f;
        recentPatterns.Clear();
        if (patternDirector != null) patternDirector.ResetHistory();
        if (openingV15 != null) openingV15.ResetCombat();
        attacksSinceComboV15 = 0;
        combatStarted = true;
        state = BossState.Combat;
        RecalculateScreenHomes(phase);
        SnapPartsToHomes();
        SetVisualAlpha(1f);
        SetCombatColliders(true);
        if (hud != null)
        {
            hud.HideAllImmediate();
            hud.ReportHealth(health.CurrentHealth, health.MaxHealth, true);
            hud.ShowBossBar(true);
            hud.ShowPhase(string.Empty);
        }
        if (bossMusic != null) bossMusic.PlayPhase(phase, 0.20f);
        if (cameraFx != null)
        {
            cameraFx.BeginBoss(phase);
            cameraFx.SetPhase(phase);
            cameraFx.SetPressure(0f);
            cameraFx.SetFraming(0f);
        }
        combatRoutine = StartCoroutine(CombatLoop());
#endif
    }

    public void DebugForcePattern(JHLPatternKind pattern)
    {
        if (combatV25 != null) { combatV25.DebugForce(); return; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (dead || !combatStarted) return;
        if (combatRoutine != null) StopCoroutine(combatRoutine);
        CleanupSpawnedObjects();
        SetHandsSolid(false);
        combatRoutine = StartCoroutine(DebugPatternRoutine(pattern));
#endif
    }

    private IEnumerator DebugPatternRoutine(JHLPatternKind pattern)
    {
        state = BossState.DebugPattern;
        currentPattern = pattern;
        patternRunning = true;
        yield return RunPattern(pattern);
        SetHandsSolid(false);
        patternRunning = false;
        if (dead) yield break;
        yield return ReturnPartsToHomeAnimated(0.34f);
        state = BossState.Combat;
        combatRoutine = StartCoroutine(CombatLoop());
    }

    public void DebugForceCleanup()
    {
        if (combatV25 != null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        StopAllCoroutines();
        patternRunning = false;
        CleanupSpawnedObjects();
        SetTrails(false, false);
        SetHandsSolid(false);
        RecalculateScreenHomes(phase);
        SnapPartsToHomes();
        if (cameraFx != null)
        {
            cameraFx.SetPressure(0f);
            cameraFx.SetCharge(0f);
            cameraFx.SetFraming(0f);
        }
        state = BossState.Combat;
        if (!dead && combatStarted) combatRoutine = StartCoroutine(CombatLoop());
#endif
    }

    public void DebugCameraLight()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (cameraFx != null) cameraFx.PulseLight(1f);
        CameraFeedbackController feedback = CameraFeedbackController.Instance;
        if (feedback != null) feedback.Impact(CameraImpactLevelV11.Medium, Vector2.down, false);
#endif
    }

    public void DebugCameraHeavy()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (cameraFx != null) cameraFx.PulseHeavy(1f);
        CameraFeedbackController feedback = CameraFeedbackController.Instance;
        if (feedback != null) feedback.Impact(CameraImpactLevelV11.Boss, Vector2.down, false);
#endif
    }

    public void DebugCameraTransition()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (cameraFx != null) cameraFx.PulseTransition(phase);
#endif
    }

    private IEnumerator CombatLoop()
    {
        yield return new WaitForSeconds(0.42f);
        while (!dead && health != null && !health.IsDead && (playerHealth == null || !playerHealth.IsDead))
        {
            RecalculateScreenHomes(phase);
            yield return HandlePendingPhaseTransitions();
            if (dead) yield break;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugForceSelectedPattern)
            {
                currentPattern = debugSelectedPattern;
                debugForceSelectedPattern = false;
            }
            else
#endif
            if (phase >= 3 && health.NormalizedHealth <= 0.15f && !fullAccessUsed)
                currentPattern = JHLPatternKind.FullAccess;
            else if (antiPressurePending && Time.time >= antiPressureCooldownUntil)
            {
                currentPattern = JHLPatternKind.RoarRepulse; // Same evadable response for every weapon
                antiPressurePending = false;
                meleePressureDamage = rangedPressureDamage = 0f;
                antiPressureCooldownUntil = Time.time + (phase == 1 ? 8f : phase == 2 ? 7f : 6f);
            }
            else
            {
                currentPattern = SelectPattern();

            }
            patternRunning = true;
            state = BossState.Combat;
            lastPatternName = currentPattern == JHLPatternKind.SweepLaserCombo ? "SWEEP + FINGER GUN" : currentPattern.ToString();
            RememberPattern(currentPattern);
            yield return RunPattern(currentPattern);
            patternRunning = false;
            if (dead) yield break;
            // Each projectile pattern owns and drains its wave before another spatial pattern starts.
            SetHandsSolid(false);
            SetTrails(false, false);
            float gap = phase == 1 ? 0.16f : phase == 2 ? 0.10f : 0.07f;
            if (currentPattern == JHLPatternKind.MassiveCentralBeam) gap += 0.28f;
            if (currentPattern == JHLPatternKind.FullAccess) gap = 0.85f;
            yield return ReturnPartsToHomeAnimated(gap);
        }
        CleanupSpawnedObjects();
        SetHandsSolid(false);
        SetTrails(false, false);
        ReleaseInputLock();
    }

    private bool ShouldRunLayeredComboV12()
    {
        if (phase < 2 || dead || state == BossState.Transition) return false;
        // Phase 2 teaches two-channel overlap. Phase 3 makes it the default rhythm
        // while keeping each component's existing telegraph intact.
        return Random.value < (phase == 2 ? 0.44f : 0.74f);
    }

    private IEnumerator RunLayeredComboV12()
    {
        LayeredComboV12[] phaseTwo =
        {
            LayeredComboV12.SweepAim,
            LayeredComboV12.CompressionFan,
            LayeredComboV12.GateBombardment
        };
        LayeredComboV12[] phaseThree =
        {
            LayeredComboV12.SweepAim,
            LayeredComboV12.GateBombardment,
            LayeredComboV12.ScissorRapidLaser,
            LayeredComboV12.GateCurtain,
            LayeredComboV12.CrossPunchSplit,
            LayeredComboV12.CompressionTracking
        };

        LayeredComboV12[] pool = phase == 2 ? phaseTwo : phaseThree;
        int pick = Random.Range(0, pool.Length);
        if (pool.Length > 1 && (int)pool[pick] == lastLayeredComboV12)
            pick = (pick + Random.Range(1, pool.Length)) % pool.Length;
        LayeredComboV12 combo = pool[pick];
        lastLayeredComboV12 = (int)combo;

        JHLPatternKind primary;
        JHLPatternKind secondary;
        float delay;

        switch (combo)
        {
            case LayeredComboV12.CompressionFan:
                primary = JHLPatternKind.Compression;
                secondary = JHLPatternKind.ProjectileFan;
                delay = 0.34f;
                break;
            case LayeredComboV12.GateBombardment:
                primary = JHLPatternKind.MovingGate;
                secondary = JHLPatternKind.PredictiveBombardment;
                delay = 0.38f;
                break;
            case LayeredComboV12.ScissorRapidLaser:
                primary = JHLPatternKind.ScissorSweep;
                secondary = JHLPatternKind.RapidLaserBurst;
                delay = 0.18f;
                break;
            case LayeredComboV12.GateCurtain:
                primary = JHLPatternKind.MovingGate;
                secondary = JHLPatternKind.LaserCurtain;
                delay = 0.26f;
                break;
            case LayeredComboV12.CrossPunchSplit:
                primary = JHLPatternKind.DiagonalCrossPunch;
                secondary = JHLPatternKind.SplitBeam;
                delay = 0.20f;
                break;
            case LayeredComboV12.CompressionTracking:
                primary = JHLPatternKind.Compression;
                secondary = JHLPatternKind.TrackingThinBeam;
                delay = 0.28f;
                break;
            default:
                primary = JHLPatternKind.HandSweep;
                secondary = JHLPatternKind.TripleAimLaser;
                delay = 0.22f;
                break;
        }

        currentPattern = primary;
        lastPatternName = "LAYERED " + combo + " [" + primary + " + " + secondary + "]";
        RememberPattern(primary);
        RememberPattern(secondary);

        auxiliaryPatternRoutine = StartCoroutine(RunAuxiliaryPatternV12(secondary, delay));
        yield return RunPattern(primary);

        // The primary can finish before the face/energy channel. Do not snap the
        // hands home or begin another pattern until both channels are complete.
        while (!dead && auxiliaryPatternRoutine != null)
            yield return null;
    }

    private IEnumerator RunAuxiliaryPatternV12(JHLPatternKind pattern, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (!dead) yield return RunPattern(pattern);
        auxiliaryPatternRoutine = null;
    }

    private IEnumerator HandlePendingPhaseTransitions()
    {
        if (suppressTransitions || health == null) yield break;
        float ratio = health.NormalizedHealth;
        if (ratio <= PhaseThreeRatio && phase < 3)
        {
            if (phase < 2) yield return PhaseTransitionRoutine(2);
            if (!dead) yield return PhaseTransitionRoutine(3);
        }
        else if (ratio <= PhaseTwoRatio && phase < 2)
        {
            yield return PhaseTransitionRoutine(2);
        }
    }

    private IEnumerator PhaseTransitionRoutine(int targetPhase)
    {
        state = BossState.Transition;
        patternRunning = false;
        CleanupSpawnedObjects();
        SetTrails(false, false);
        inputLock = GameInputState.Acquire("JHLPhase" + targetPhase);
        SetCombatColliders(false);
        phase = targetPhase;
        // Preserve a queued anti-pressure response through a phase transition.
        meleePressureDamage = 0f;
        rangedPressureDamage = 0f;
        antiPressureCooldownUntil = Time.time + 1.25f;
        RecalculateScreenHomes(phase);
        if (bossAudio != null) bossAudio.PlayPhaseChange();
        if (bossMusic != null) bossMusic.PlayPhase(phase, targetPhase >= 3 ? 0.70f : 0.58f);

        if (hud != null) hud.ShowPhase(targetPhase >= 3 ? "ADMIN OVERRIDE" : "CONTROL OVERRIDE");
        if (cameraFx != null)
        {
            cameraFx.SetPressure(targetPhase >= 3 ? 0.45f : 0.28f);
            cameraFx.SetFraming(targetPhase >= 3 ? 0.045f : 0.025f);
            cameraFx.SetPhase(phase);
        }

        CameraFeedbackController feedback = CameraFeedbackController.Instance;
        if (feedback != null) feedback.Impact(CameraImpactLevelV11.Boss, Vector2.down, phase >= 3);

        faceMotion.SetMotionProfile(targetPhase >= 3 ? 3.5f : 3.0f, 1.10f, 27f);
        leftMotion.SetMotionProfile(targetPhase >= 3 ? 3.9f : 3.4f, 1.08f, 34f);
        rightMotion.SetMotionProfile(targetPhase >= 3 ? 3.9f : 3.4f, 1.08f, 34f);

        faceMotion.SetTarget(faceHome, JHLPartMotionState.Attack);
        leftMotion.SetTarget(leftHome + Vector3.right * (targetPhase >= 3 ? 0.60f : 0.32f), JHLPartMotionState.Attack);
        rightMotion.SetTarget(rightHome + Vector3.left * (targetPhase >= 3 ? 0.60f : 0.32f), JHLPartMotionState.Attack);
        faceMotion.SetScaleMultiplier(targetPhase >= 3 ? new Vector3(1.05f, 0.95f, 1f) : new Vector3(1.03f, 0.97f, 1f));
        leftMotion.SetScaleMultiplier(new Vector3(1.04f, 0.96f, 1f));
        rightMotion.SetScaleMultiplier(new Vector3(1.04f, 0.96f, 1f));
        faceMotion.AddAngularImpulse(targetPhase >= 3 ? 18f : 10f);
        leftMotion.AddAngularImpulse(22f);
        rightMotion.AddAngularImpulse(-22f);

        float duration = targetPhase >= 3 ? 1.15f : 0.88f;
        yield return WaitUnscaled(duration * 0.45f);

        SetHomeTargets(JHLPartMotionState.Recovery);
        faceMotion.SetScaleMultiplier(Vector3.one);
        leftMotion.SetScaleMultiplier(Vector3.one);
        rightMotion.SetScaleMultiplier(Vector3.one);
        yield return WaitUnscaled(duration * 0.55f);

        if (cameraFx != null)
        {
            cameraFx.SetPressure(0f);
            cameraFx.SetFraming(0f);
        }
        if (hud != null) hud.ShowPhase(string.Empty);
        SetCombatColliders(true);
        ReleaseInputLock();
        state = BossState.Combat;
    }

    private JHLPatternKind SelectPattern()
    {
        if (patternDirector == null) patternDirector = GetComponent<JHLPatternDirector>();
        if (patternDirector == null) patternDirector = gameObject.AddComponent<JHLPatternDirector>();
        GetCameraFrame(out Vector3 center, out float halfW, out _);
        Vector2 playerPos = player != null ? (Vector2)player.position : (Vector2)center;
        Vector2 playerVelocity = playerBody != null ? playerBody.linearVelocity : Vector2.zero;
        return patternDirector.Select(phase, health != null ? health.NormalizedHealth : 1f, playerPos, playerVelocity, center, halfW, fullAccessUsed);
    }

    private void RememberPattern(JHLPatternKind pattern)
    {
        recentPatterns.Enqueue(pattern);
        while (recentPatterns.Count > 3) recentPatterns.Dequeue();
        if (patternDirector != null) patternDirector.Remember(pattern);
    }

    private IEnumerator RunPattern(JHLPatternKind pattern)
    {
        PrepareHandsV18(pattern);
        switch (pattern)
        {
            case JHLPatternKind.HandSlam:
                yield return HandSlamRoutine(Random.value < 0.5f ? leftMotion : rightMotion, 0.85f, 1);
                if (phase >= 3 && !dead) { yield return new WaitForSeconds(0.25f); yield return HandSlamRoutine(Random.value < 0.5f ? leftMotion : rightMotion, 0.78f, 1); }
                break;
            case JHLPatternKind.HandSweep: yield return HandSweepRoutine(Random.value < 0.5f, 0.74f, 1); break;
            case JHLPatternKind.StraightLaser: yield return StraightLaserRoutine(0.78f, 0.34f, 1); break;
            case JHLPatternKind.FingerBarrage: yield return GunVolleyV18(false, false); break;
            case JHLPatternKind.DoubleTapSlam: yield return DoubleTapSlamRoutine(); break;
            case JHLPatternKind.SidePunch: yield return SidePunchRoutine(); break;
            case JHLPatternKind.TripleAimLaser: yield return TripleAimLaserRoutine(); break;
            case JHLPatternKind.ProjectileFan: yield return GunVolleyV18(false, true); break;
            case JHLPatternKind.EnhancedSlam: yield return EnhancedSlamRoutine(); break;
            case JHLPatternKind.EnhancedSweep: yield return EnhancedSweepRoutine(); break;
            case JHLPatternKind.Compression: yield return CompressionRoutine(false); break;
            case JHLPatternKind.HandPrison: yield return HandPrisonRoutine(); break;
            case JHLPatternKind.CrossSlam: yield return CrossSlamRoutine(); break;
            case JHLPatternKind.TwinSlam: yield return TwinSlamRoutine(); break;
            case JHLPatternKind.SlamChain: yield return SlamChainRoutine(); break;
            case JHLPatternKind.ScissorSweep: yield return ScissorSweepRoutine(); break;
            case JHLPatternKind.LaserSweep: yield return LaserSweepRoutine(); break;
            case JHLPatternKind.CrossLaser: yield return CrossLaserRoutine(); break;
            case JHLPatternKind.PrisonBarrage: yield return PrisonBarrageRoutine(); break;
            case JHLPatternKind.CompressionBurst: yield return CompressionBurstRoutine(); break;
            case JHLPatternKind.MassiveCentralBeam: yield return MassiveCentralBeamRoutine(); break;
            case JHLPatternKind.TrackingThinBeam: yield return TrackingBeamRoutine(2.4f, 42f, 0.9f); break;
            case JHLPatternKind.SplitBeam: yield return SplitBeamRoutine(); break;
            case JHLPatternKind.LaserCurtain: yield return LaserCurtainRoutine(); break;
            case JHLPatternKind.SpiralBeam: yield return SpiralBeamRoutine(); break;
            case JHLPatternKind.CrossfireBarrage: yield return GunVolleyV18(true, true); break;
            case JHLPatternKind.SweepLaserCombo: yield return SweepBarrageRoutineV17(); break;
            case JHLPatternKind.QuadSlam: yield return QuadSlamRoutine(); break;
            case JHLPatternKind.RapidLaserBurst: yield return RapidLaserBurstRoutine(); break;
            case JHLPatternKind.FinalCompression: yield return FinalCompressionRoutine(); break;
            case JHLPatternKind.MovingGate: yield return ApproachGateRoutineV17(); break;
            case JHLPatternKind.DiagonalCrossPunch: yield return DiagonalCrossPunchRoutine(); break;
            case JHLPatternKind.PredictiveBombardment: yield return PredictiveBombardmentRoutine(); break;
            case JHLPatternKind.BeamPinch: yield return BeamPinchRoutine(); break;
            case JHLPatternKind.RoarRepulse: yield return RoarRepulseRoutine(); break;
            case JHLPatternKind.RemoteSuppression: yield return RemoteSuppressionRoutine(); break;
            case JHLPatternKind.FullAccess: yield return FullAccessRoutine(); break;
        }
    }

    private IEnumerator HandSlamRoutine(JHLPartMotion motion, float warning, int damage)
    {
        if (motion == null || player == null) yield break;

        warning = Mathf.Max(warning, phase == 1 ? 0.85f : phase == 2 ? 0.75f : 0.68f);
        Vector2 target = ClampToArena(player.position, 0.92f);
        float slamRadius = Mathf.Clamp(handSize.x * 0.16f, 0.78f, 1.18f);
        GameObject telegraph = CreateCircleTelegraph(target, slamRadius, new Color(1f, 0.08f, 0.25f, 0.24f), warning);
        if (bossAudio != null) bossAudio.PlayHandWindup();
        float sideSign = motion == leftMotion ? -1f : 1f;
        Vector3 prep = new Vector3(target.x + sideSign * 0.18f, target.y + Mathf.Max(1.80f, handSize.y * 0.72f), 0f);

        motion.SetMotionProfile(4.0f, 1.08f, 30f);
        motion.SetRotationProfile(4.6f, 1.10f);
        motion.SetScaleProfile(5.5f, 1.10f);
        motion.SetTarget(prep, JHLPartMotionState.Anticipation);
        motion.SetRotation(sideSign * -8f, JHLPartMotionState.Anticipation);
        motion.SetScaleMultiplier(new Vector3(1.04f, 0.96f, 1f));
        yield return new WaitForSeconds(warning * 0.62f);

        motion.SetTarget(prep + Vector3.up * 0.22f, JHLPartMotionState.Anticipation);
        motion.AddVelocityImpulse(Vector2.up * 1.1f);
        yield return new WaitForSeconds(warning * 0.38f);

        SetTrailForMotion(motion, true);
        motion.SetMotionProfile(10.5f, 0.42f, 62f);
        motion.SetRotation(0f, JHLPartMotionState.Attack);
        motion.SetScaleMultiplier(new Vector3(0.95f, 1.06f, 1f));
        motion.SetTarget(new Vector3(target.x, target.y, 0f), JHLPartMotionState.Attack);
        motion.AddVelocityImpulse(Vector2.down * 2.2f);
        // V11: damage is tied to the hand reaching the impact point, not a guessed timer.
        yield return motion.WaitForSettle(0.65f, 0.10f);
        DestroyTracked(telegraph);
        if (motion.DistanceToTarget > 0.12f)
        {
            SetTrailForMotion(motion, false);
            motion.SetScaleMultiplier(Vector3.one);
            motion.SetTarget(HomeFor(motion), JHLPartMotionState.Recovery);
            yield break;
        }

        DamagePlayerCircle(target, slamRadius, damage);
        SpawnImpact(target, slamRadius * 0.92f);
        if (bossAudio != null) bossAudio.PlayHandImpact();
        motion.SetScaleMultiplier(new Vector3(1.06f, 0.94f, 1f));
        motion.AddVelocityImpulse(Vector2.up * 3.2f);
        motion.AddAngularImpulse(sideSign * 16f);
        CameraFeedbackController feedback = CameraFeedbackController.Instance;
        if (feedback != null) feedback.Impact(phase >= 3 ? CameraImpactLevelV11.Heavy : CameraImpactLevelV11.Medium, Vector2.down, phase >= 3);
        if (cameraFx != null) cameraFx.PulseLight(1f);
        yield return new WaitForSeconds(ApproachDurationV17());

        SetTrailForMotion(motion, false);
        motion.SetScaleMultiplier(Vector3.one);
        motion.SetMotionProfile(4.1f, 1.08f, 32f);
        motion.SetTarget(HomeFor(motion), JHLPartMotionState.Recovery);
        motion.SetRotation(0f, JHLPartMotionState.Recovery);
        yield return new WaitForSeconds(0.16f);
    }

    private IEnumerator HandSweepRoutine(bool leftToRight, float warning, int damage)
    {
        warning = Mathf.Max(warning, 0.95f);
        Bounds b = GetCombatBounds();
        float y = player != null ? Mathf.Clamp(player.position.y, b.min.y + 0.9f, b.max.y - 0.9f) : b.center.y;
        Vector2 center = new Vector2(b.center.x, y);
        Vector2 size = new Vector2(Mathf.Max(3f, b.size.x + handSize.x * 0.96f), Mathf.Max(1.85f, handSize.y * 0.94f));
        GameObject telegraph = CreateRectTelegraph(center, size, 0f, new Color(1f, 0.10f, 0.28f, 0.22f), warning);
        if (bossAudio != null) bossAudio.PlayHandWindup();

        JHLPartMotion motion = leftToRight ? leftMotion : rightMotion;
        if (motion == null) yield break;
        GetCameraFrame(out Vector3 cameraCenter, out float halfW, out _);
        float startX = cameraCenter.x + (leftToRight ? -1f : 1f) * (halfW + handSize.x * 0.72f);
        float endX = cameraCenter.x + (leftToRight ? 1f : -1f) * (halfW + handSize.x * 0.90f);
        Vector3 start = new Vector3(startX, y, 0f);
        Vector3 windup = start + Vector3.right * (leftToRight ? -handSize.x * 0.22f : handSize.x * 0.22f);
        Vector3 end = new Vector3(endX, y, 0f);
        float sign = leftToRight ? 1f : -1f;

        motion.SetMotionProfile(3.9f, 1.08f, 34f);
        motion.SetTarget(start, JHLPartMotionState.Anticipation);
        motion.SetRotation(-sign * 8f, JHLPartMotionState.Anticipation);
        motion.SetScaleMultiplier(new Vector3(0.97f, 1.03f, 1f));
        yield return new WaitForSeconds(warning * 0.62f);
        motion.SetTarget(windup, JHLPartMotionState.Anticipation);
        motion.AddVelocityImpulse(Vector2.right * (-sign * 1.4f));
        yield return new WaitForSeconds(warning * 0.38f);
        DestroyTracked(telegraph);

        SetTrailForMotion(motion, true);
        motion.SetMotionProfile(7.0f, 0.95f, 72f);
        motion.SetRotation(sign * 5f, JHLPartMotionState.Attack);
        motion.SetScaleMultiplier(new Vector3(1.05f, 0.95f, 1f));
        motion.SetTarget(end, JHLPartMotionState.Attack);
        motion.AddVelocityImpulse(Vector2.right * (sign * 4.5f));

        float duration = 1.4f;
        if (bossAudio != null) bossAudio.PlaySweep();
        float elapsed = 0f;
        bool damaged = false;
        while (elapsed < duration && !dead && motion.DistanceToTarget > 0.12f)
        {
            elapsed += Time.deltaTime;

            // Sweep telegraph is only a warning lane. Damage is resolved from the
            // CURRENT hand shape every frame, so standing somewhere inside the
            // warned lane is harmless unless the moving hand physically reaches
            // the player. This also keeps fast dashes through the lane fair.
            if (!damaged && TryDamagePlayerFromHandContact(motion.transform, damage))
                damaged = true;

            yield return null;
        }

        CameraFeedbackController feedback = CameraFeedbackController.Instance;
        if (feedback != null) feedback.Shake(0.16f, 0.060f, leftToRight ? Vector2.right : Vector2.left);
        if (cameraFx != null) cameraFx.PulseLight(0.72f);
        motion.AddVelocityImpulse(Vector2.right * (sign * 2.0f));
        yield return new WaitForSeconds(0.07f);
        SetTrailForMotion(motion, false);
        motion.SetScaleMultiplier(Vector3.one);
        motion.SetMotionProfile(4.0f, 1.08f, 34f);
        motion.SetTarget(HomeFor(motion), JHLPartMotionState.Recovery);
        motion.SetRotation(0f, JHLPartMotionState.Recovery);
    }

    private IEnumerator StraightLaserRoutine(float warning, float width, int damage)
    {
        if (face == null || player == null) yield break;
        warning = Mathf.Max(warning, 0.60f);
        Vector2 origin = faceFireOrigin != null ? faceFireOrigin.position : face.position;
        Vector2 dir = ((Vector2)player.position - origin).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.down;
        float length = BeamLengthToArena(origin, dir);
        GameObject telegraph = CreateLineTelegraph(origin, dir, length, width, new Color(1f, 0.08f, 0.30f, 0.26f), warning);
        if (combatEffects != null) combatEffects.SpawnCharge(faceFireOrigin != null ? faceFireOrigin : face, warning, Mathf.Max(0.20f, width * 1.7f), new Color(1f, 0.72f, 0.95f, 0.95f));
        if (bossAudio != null) bossAudio.PlayBeamCharge();

        faceMotion.SetMotionProfile(3.8f, 1.10f, 20f);
        faceMotion.SetScaleMultiplier(new Vector3(1.02f, 0.97f, 1f));
        faceMotion.SetTarget(faceHome + (Vector3)(-dir * 0.22f), JHLPartMotionState.Anticipation);
        if (cameraFx != null)
        {
            cameraFx.SetCharge(0.70f);
            cameraFx.SetFraming(0.012f);
        }
        yield return new WaitForSeconds(warning);
        DestroyTracked(telegraph);

        // Use exactly the world geometry declared by the warning.
        GameObject beam = CreateBeam(origin, dir, length, width, 0.75f, new Color(1f, 0.14f, 0.44f, 0.68f), Color.white);
        if (bossAudio != null) bossAudio.PlayBeamFire();
        faceMotion.SetScaleMultiplier(new Vector3(1.01f, 1.04f, 1f));
        faceMotion.AddVelocityImpulse(-dir * 1.8f);
        CameraFeedbackController feedback = CameraFeedbackController.Instance;
        if (feedback != null) feedback.Impact(CameraImpactLevelV11.Medium, dir, false);
        if (cameraFx != null)
        {
            cameraFx.SetCharge(0f);
            cameraFx.PulseLight(1f);
        }
        yield return SustainStaticBeamDamage(origin, dir, length, width, damage, 0.75f);
        DestroyTracked(beam);
        faceMotion.SetScaleMultiplier(Vector3.one);
        faceMotion.SetTarget(faceHome, JHLPartMotionState.Recovery);
        if (cameraFx != null) cameraFx.SetFraming(0f);
    }

    private IEnumerator FingerBarrageRoutine()
    {
        if (player == null) yield break;
        bool useLeft = Random.value < 0.5f;
        JHLPartMotion motion = useLeft ? leftMotion : rightMotion;
        Transform tip = useLeft ? leftFingerTip : rightFingerTip;
        if (motion == null || tip == null) yield break;

        GetCameraFrame(out Vector3 center, out float halfW, out float halfH);
        Vector3 firePosition = center + new Vector3((useLeft ? -1f : 1f) * halfW * 0.62f, halfH * 0.05f, 0f);
        motion.SetMotionProfile(3.8f, 1.08f, 32f);
        motion.SetTarget(firePosition, JHLPartMotionState.Anticipation);
        motion.SetScaleMultiplier(new Vector3(0.98f, 1.02f, 1f));
        yield return new WaitForSeconds(0.40f);

        int count = phase == 1 ? 9 : phase == 2 ? 12 : 15;
        Vector2 lockedOrigin = tip.position;
        Vector2 lockedDirection = ((Vector2)player.position - lockedOrigin).normalized;
        if (lockedDirection.sqrMagnitude < 0.01f) lockedDirection = Vector2.down;
        List<GameObject> aimWarnings = new List<GameObject>();
        for (int ray = -1; ray <= 1; ray++)
            aimWarnings.Add(CreateLineTelegraph(lockedOrigin, RotateVector(lockedDirection, ray * 16f), 3f, 0.3f,
                new Color(1f, 0.12f, 0.32f, 0.3f), 0.75f));
        yield return new WaitForSeconds(0.75f);
        foreach (GameObject aimWarning in aimWarnings) DestroyTracked(aimWarning);
        if (bossAudio != null) bossAudio.PlayHandWindup();
        for (int i = 0; i < count; i++)
        {
            if (dead || player == null) yield break;
            Vector2 origin = lockedOrigin;
            Vector2 direction = RotateVector(lockedDirection, (i % 3 - 1) * 16f);
            JHLProjectile projectile = JHLProjectile.Create(this, origin, direction,
                phase == 1 ? 8.5f : phase == 2 ? 9.5f : 10.5f, 1);
            if (projectile != null) projectile.SetLifetimeV17(1.4f);
            if (bossAudio != null) bossAudio.PlayProjectile();
            if (projectile != null) RegisterSpawnedObject(projectile.gameObject);
            motion.AddVelocityImpulse(-direction * 0.55f);
            motion.AddAngularImpulse((useLeft ? -1f : 1f) * 7f);
            if (cameraFx != null && i == 0) cameraFx.PulseLight(0.35f);
            yield return new WaitForSeconds(phase >= 3 ? 0.105f : 0.135f);
        }
        motion.SetScaleMultiplier(Vector3.one);
        motion.SetRotation(0f, JHLPartMotionState.Recovery);
        yield return new WaitForSeconds(0.12f);
    }

    private IEnumerator EnhancedSlamRoutine()
    {
        JHLPartMotion first = Random.value < 0.5f ? leftMotion : rightMotion;
        JHLPartMotion second = first == leftMotion ? rightMotion : leftMotion;
        yield return HandSlamRoutine(first, 0.48f, 1);
        yield return new WaitForSeconds(0.07f);
        yield return HandSlamRoutine(second, 0.40f, 1);
    }

    private IEnumerator EnhancedSweepRoutine()
    {
        bool direction = Random.value < 0.5f;
        yield return HandSweepRoutine(direction, 0.54f, 1);
        yield return new WaitForSeconds(0.09f);
        yield return HandSweepRoutine(!direction, 0.42f, 1);
    }

    private IEnumerator CompressionRoutine(bool aggressive)
    {
        if (leftMotion == null || rightMotion == null) yield break;
        Bounds b = GetCombatBounds();
        float y = player != null ? Mathf.Clamp(player.position.y, b.min.y + 1f, b.max.y - 1f) : b.center.y;
        Vector2 center = new Vector2(b.center.x, y);
        Vector2 lane = new Vector2(b.size.x + handSize.x, handSize.y * 1.08f);
        // Leave the horizontal lane above/below; only the actual moving hands deal damage.
        GameObject warning = CreateRectTelegraph(center, lane, 0f, new Color(1f, 0.08f, 0.25f, 0.24f), 1.35f);
        if (bossAudio != null) bossAudio.PlayCompression();
        SetHandsSolid(false);
        leftMotion.SetKinematicProfile(34f, 100f, 120f);
        rightMotion.SetKinematicProfile(34f, 100f, 120f);
        leftMotion.SetTarget(new Vector3(b.min.x - handSize.x * 0.55f, y, 0f), JHLPartMotionState.Anticipation);
        rightMotion.SetTarget(new Vector3(b.max.x + handSize.x * 0.55f, y, 0f), JHLPartMotionState.Anticipation);
        yield return new WaitForSeconds(1.35f);
        DestroyTracked(warning);
        if (leftMotion.DistanceToTarget > 0.2f || rightMotion.DistanceToTarget > 0.2f) yield break;
        SetHandsSolid(true);
        SetTrails(true, true);
        leftMotion.SetKinematicProfile(18f, 65f, 100f);
        rightMotion.SetKinematicProfile(18f, 65f, 100f);
        leftMotion.SetTarget(new Vector3(center.x - handSize.x * 0.45f, y, 0f), JHLPartMotionState.Attack);
        rightMotion.SetTarget(new Vector3(center.x + handSize.x * 0.45f, y, 0f), JHLPartMotionState.Attack);
        float elapsed = 0f;
        bool hit = false;
        while (elapsed < 1.4f && !dead)
        {
            elapsed += Time.deltaTime;
            if (!hit) hit = TryDamagePlayerFromHandContact(leftHand, aggressive ? 2 : 1) ||
                TryDamagePlayerFromHandContact(rightHand, aggressive ? 2 : 1);
            if (leftMotion.DistanceToTarget < 0.12f && rightMotion.DistanceToTarget < 0.12f) break;
            yield return null;
        }
        if (!dead && leftMotion.DistanceToTarget < 0.2f && rightMotion.DistanceToTarget < 0.2f)
        {
            SpawnImpact(center, 1.2f);
            if (bossAudio != null) bossAudio.PlayHandImpact();
        }
        SetHandsSolid(false);
        SetTrails(false, false);
    }

    private IEnumerator HandPrisonRoutine()
    {
        if (player == null || leftMotion == null || rightMotion == null) yield break;
        Vector2 center = ClampToArena(player.position, 1.7f);
        float prisonSafeWidth = Mathf.Max(3.0f, handSize.x * 0.38f);
        float prisonOffset = prisonSafeWidth * 0.5f + handSize.x * 0.5f;
        Vector3 leftTarget = new Vector3(center.x - prisonOffset, center.y, 0f);
        Vector3 rightTarget = new Vector3(center.x + prisonOffset, center.y, 0f);
        debugSafeWidth = prisonSafeWidth;
        leftMotion.SetMotionProfile(3.3f, 1.08f, 30f);
        rightMotion.SetMotionProfile(3.3f, 1.08f, 30f);
        leftMotion.SetTarget(leftTarget, JHLPartMotionState.Attack);
        rightMotion.SetTarget(rightTarget, JHLPartMotionState.Attack);
        leftMotion.SetScaleMultiplier(new Vector3(0.98f, 1.02f, 1f));
        rightMotion.SetScaleMultiplier(new Vector3(0.98f, 1.02f, 1f));
        if (cameraFx != null) cameraFx.SetPressure(0.46f);
        yield return WaitForPartsSettle(leftMotion, rightMotion, 0.82f, 0.12f);
        SetHandsSolid(true);

        for (int i = 0; i < 3; i++)
        {
            yield return StraightLaserRoutine(0.46f, 0.29f, 1);
            yield return new WaitForSeconds(0.12f);
        }

        SetHandsSolid(false);
        leftMotion.SetScaleMultiplier(Vector3.one);
        rightMotion.SetScaleMultiplier(Vector3.one);
        if (cameraFx != null) cameraFx.SetPressure(0f);
        debugSafeWidth = -1f;
    }

    private IEnumerator CrossSlamRoutine()
    {
        JHLPartMotion first = Random.value < 0.5f ? leftMotion : rightMotion;
        JHLPartMotion second = first == leftMotion ? rightMotion : leftMotion;
        yield return HandSlamRoutine(first, phase >= 3 ? 0.39f : 0.49f, 1);
        yield return new WaitForSeconds(0.06f);
        yield return HandSlamRoutine(second, phase >= 3 ? 0.34f : 0.42f, 1);
    }

    private IEnumerator MassiveCentralBeamRoutine()
    {
        if (face == null) yield break;
        GetCameraFrame(out Vector3 center, out float halfW, out float halfH);
        Vector2 origin = faceFireOrigin != null ? faceFireOrigin.position : face.position;
        Vector2 dir = Vector2.down;
        float length = Mathf.Max(2f, origin.y - (center.y - halfH) + 0.4f);
        float width = Mathf.Clamp(halfW * 0.58f, 2.8f, 5.2f);
        float warning = 1.12f;
        GameObject marker = CreateLineTelegraph(origin, dir, length, width, new Color(1f, 0.04f, 0.22f, 0.20f), warning);
        if (combatEffects != null) combatEffects.SpawnCharge(faceFireOrigin != null ? faceFireOrigin : face, warning, Mathf.Max(0.55f, width * 0.18f), new Color(1f, 0.72f, 0.95f, 1f));
        if (bossAudio != null) bossAudio.PlayBeamCharge();

        faceMotion.SetMotionProfile(3.0f, 1.10f, 24f);
        faceMotion.SetTarget(faceHome, JHLPartMotionState.Anticipation);
        faceMotion.SetScaleMultiplier(new Vector3(1.03f, 0.95f, 1f));
        if (cameraFx != null)
        {
            cameraFx.SetCharge(1f);
            cameraFx.SetPressure(0.28f);
            cameraFx.SetFraming(0.032f);
        }
        yield return new WaitForSeconds(warning);
        DestroyTracked(marker);

        // Preserve warning origin/length during firing.
        GameObject beam = CreateBeam(origin, dir, length, width, 0.78f, new Color(1f, 0.08f, 0.34f, 0.68f), Color.white);
        if (bossAudio != null) bossAudio.PlayBeamFire();
        faceMotion.SetScaleMultiplier(new Vector3(1.04f, 1.03f, 1f));
        faceMotion.AddVelocityImpulse(Vector2.up * 2.2f);
        CameraFeedbackController feedback = CameraFeedbackController.Instance;
        if (feedback != null) feedback.Impact(CameraImpactLevelV11.Boss, Vector2.down, true);
        if (cameraFx != null)
        {
            cameraFx.SetCharge(0f);
            cameraFx.PulseHeavy(1f);
        }
        yield return SustainStaticBeamDamage(origin, dir, length, width, 2, 0.78f);
        DestroyTracked(beam);
        faceMotion.SetScaleMultiplier(Vector3.one);
        faceMotion.SetTarget(faceHome, JHLPartMotionState.Recovery);
        if (cameraFx != null)
        {
            cameraFx.SetPressure(0f);
            cameraFx.SetFraming(0f);
        }
    }

    private IEnumerator TrackingBeamRoutine(float duration, float angularSpeed, float warning)
    {
        if (face == null || player == null) yield break;
        Vector2 origin = faceFireOrigin != null ? faceFireOrigin.position : face.position;
        Vector2 dir = ((Vector2)player.position - origin).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.down;
        float length = BeamLengthToArena(origin, dir);
        const float trackingBeamWidth = 0.69f;
        GameObject marker = CreateLineTelegraph(origin, dir, length, trackingBeamWidth, new Color(1f, 0.08f, 0.32f, 0.25f), warning);
        if (combatEffects != null) combatEffects.SpawnCharge(faceFireOrigin != null ? faceFireOrigin : face, warning, 0.34f, new Color(0.84f, 0.78f, 1f, 0.95f));
        if (bossAudio != null) bossAudio.PlayBeamCharge();
        faceMotion.SetScaleMultiplier(new Vector3(1.02f, 0.97f, 1f));
        if (cameraFx != null) cameraFx.SetCharge(0.45f);
        yield return new WaitForSeconds(warning);
        DestroyTracked(marker);

        GameObject beam = CreateBeam(origin, dir, length, trackingBeamWidth, duration, new Color(0.70f, 0.15f, 1f, 0.62f), Color.white);
        if (bossAudio != null) bossAudio.PlayBeamFire();
        JHLBeamVisual beamVisual = beam != null ? beam.GetComponent<JHLBeamVisual>() : null;
        if (cameraFx != null)
        {
            cameraFx.SetCharge(0f);
            cameraFx.PulseLight(0.8f);
        }

        float elapsed = 0f;
        float nextDamage = 0f;
        float beamAngularVelocity = 0f;
        while (elapsed < duration && !dead)
        {
            elapsed += Time.deltaTime;
            origin = faceFireOrigin != null ? (Vector2)faceFireOrigin.position : (Vector2)face.position;
            Vector2 desired = player != null ? ((Vector2)player.position - origin).normalized : dir;
            float currentAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(currentAngle, targetAngle);
            float desiredAngularVelocity = Mathf.Sign(delta) * Mathf.Min(angularSpeed, Mathf.Sqrt(Mathf.Max(0f, 2f * angularSpeed * 3.2f * Mathf.Abs(delta))));
            beamAngularVelocity = Mathf.MoveTowards(beamAngularVelocity, desiredAngularVelocity, angularSpeed * 3.2f * Time.deltaTime);
            float nextAngle = currentAngle + beamAngularVelocity * Time.deltaTime;
            dir = new Vector2(Mathf.Cos(nextAngle * Mathf.Deg2Rad), Mathf.Sin(nextAngle * Mathf.Deg2Rad));
            length = BeamLengthToArena(origin, dir);
            if (beamVisual != null) beamVisual.SetGeometry(origin, dir, length, trackingBeamWidth);
            if (elapsed >= nextDamage)
            {
                DamagePlayerBeam(origin, dir, length, trackingBeamWidth, 1);
                nextDamage = elapsed + 0.66f;
            }
            yield return null;
        }
        DestroyTracked(beam);
        faceMotion.SetScaleMultiplier(Vector3.one);
    }

    private IEnumerator SplitBeamRoutine()
    {
        if (face == null) yield break;
        float[] angles = { -126f, -108f, -90f, -72f, -54f };
        List<GameObject> warnings = new List<GameObject>();
        Vector2 origin = faceFireOrigin != null ? faceFireOrigin.position : face.position;
        for (int i = 0; i < angles.Length; i++)
        {
            Vector2 dir = AngleToVector(angles[i]);
            float length = BeamLengthToArena(origin, dir);
            warnings.Add(CreateLineTelegraph(origin, dir, length, 0.09f, new Color(1f, 0.08f, 0.32f, 0.25f), 0.88f));
        }

        faceMotion.SetScaleMultiplier(new Vector3(1.03f, 0.96f, 1f));
        if (combatEffects != null) combatEffects.SpawnCharge(faceFireOrigin != null ? faceFireOrigin : face, 0.88f, 0.46f, new Color(0.76f, 0.88f, 1f, 0.96f));
        if (bossAudio != null) bossAudio.PlayBeamCharge();
        if (cameraFx != null)
        {
            cameraFx.SetCharge(0.75f);
            cameraFx.SetFraming(0.022f);
        }
        yield return new WaitForSeconds(0.88f);
        for (int i = 0; i < warnings.Count; i++) DestroyTracked(warnings[i]);

        List<GameObject> beams = new List<GameObject>();
        origin = faceFireOrigin != null ? faceFireOrigin.position : face.position;
        for (int i = 0; i < angles.Length; i++)
        {
            Vector2 dir = AngleToVector(angles[i]);
            float length = BeamLengthToArena(origin, dir);
            beams.Add(CreateBeam(origin, dir, length, 0.19f, 0.48f, new Color(0.30f, 0.65f, 1f, 0.58f), Color.white));
        }

        faceMotion.SetScaleMultiplier(new Vector3(1.02f, 1.04f, 1f));
        if (bossAudio != null) bossAudio.PlayBeamFire();
        faceMotion.AddVelocityImpulse(Vector2.up * 1.5f);
        CameraFeedbackController feedback = CameraFeedbackController.Instance;
        if (feedback != null) feedback.Impact(CameraImpactLevelV11.Heavy, Vector2.down, false);
        if (cameraFx != null)
        {
            cameraFx.SetCharge(0f);
            cameraFx.PulseHeavy(0.82f);
        }
        bool splitHit = false;
        float splitElapsed = 0f;
        while (splitElapsed < 0.48f)
        {
            splitElapsed += Time.deltaTime;
            if (!splitHit)
            {
                origin = faceFireOrigin != null ? faceFireOrigin.position : face.position;
                for (int i = 0; i < angles.Length && !splitHit; i++)
                {
                    Vector2 beamDir = AngleToVector(angles[i]);
                    float beamLength = BeamLengthToArena(origin, beamDir);
                    splitHit = TryDamagePlayerBeam(origin, beamDir, beamLength, 0.19f, 1);
                }
            }
            yield return null;
        }
        for (int i = 0; i < beams.Count; i++) DestroyTracked(beams[i]);
        faceMotion.SetScaleMultiplier(Vector3.one);
        if (cameraFx != null) cameraFx.SetFraming(0f);
    }


    private IEnumerator DoubleTapSlamRoutine()
    {
        JHLPartMotion motion = Random.value < 0.5f ? leftMotion : rightMotion;
        yield return HandSlamRoutine(motion, 0.58f, 1);
        yield return new WaitForSeconds(0.09f);
        yield return HandSlamRoutine(motion, 0.44f, 1);
    }

    private IEnumerator SidePunchRoutine()
    {
        if (player == null) yield break;
        bool fromLeft = Random.value < 0.5f;
        JHLPartMotion motion = fromLeft ? leftMotion : rightMotion;
        if (motion == null) yield break;
        GetCameraFrame(out Vector3 center, out float halfW, out _);
        float y = ClampToArena(player.position, 0.95f).y;
        float sign = fromLeft ? 1f : -1f;
        Vector3 start = new Vector3(center.x - sign * (halfW + handSize.x * 0.56f), y, 0f);
        Vector3 target = new Vector3(Mathf.Clamp(player.position.x, center.x - halfW * 0.38f, center.x + halfW * 0.38f), y, 0f);
        float laneWidth = Mathf.Abs(target.x - start.x) + handSize.x * 0.32f;
        Vector2 warningCenter = new Vector2((start.x + target.x) * 0.5f, y);
        Vector2 warningSize = new Vector2(laneWidth, Mathf.Clamp(handSize.y * 0.32f, 0.85f, 1.30f));
        GameObject warning = CreateRectTelegraph(warningCenter, warningSize, 0f, new Color(1f, 0.08f, 0.24f, 0.22f), 0.66f);
        if (bossAudio != null) bossAudio.PlayHandWindup();
        motion.SetMotionProfile(4.2f, 1.08f, 36f);
        motion.SetTarget(start, JHLPartMotionState.Anticipation);
        motion.SetRotation(fromLeft ? -7f : 7f, JHLPartMotionState.Anticipation);
        yield return new WaitForSeconds(0.66f);
        DestroyTracked(warning);
        motion.SetMotionProfile(9.0f, 0.78f, 68f);
        motion.SetTarget(target, JHLPartMotionState.Attack);
        motion.AddVelocityImpulse(Vector2.right * sign * 3.8f);
        // Telegraph is only the announced lane. Like Sweep, Side Punch only hurts when
        // the live hand ellipse actually reaches the player's collider.
        float punchElapsed = 0f;
        bool punchHit = false;
        while (punchElapsed < 0.34f)
        {
            punchElapsed += Time.deltaTime;
            if (!punchHit && TryDamagePlayerFromHandContact(motion.transform, 1))
                punchHit = true;
            yield return null;
        }
        SpawnImpact(target, 0.9f);
        if (bossAudio != null) bossAudio.PlayHandImpact();
        CameraFeedbackController feedback = CameraFeedbackController.Instance;
        if (feedback != null) feedback.Impact(CameraImpactLevelV11.Medium, Vector2.right * sign, false);
        motion.AddVelocityImpulse(Vector2.left * sign * 2.8f);
        yield return new WaitForSeconds(0.12f);
        motion.SetTarget(HomeFor(motion), JHLPartMotionState.Recovery);
        motion.SetRotation(0f, JHLPartMotionState.Recovery);
    }

    private IEnumerator TripleAimLaserRoutine()
    {
        for (int i = 0; i < 3; i++)
        {
            yield return StraightLaserRoutine(i == 0 ? 0.52f : 0.38f, 0.20f, 1);
            yield return new WaitForSeconds(0.08f);
        }
    }

    private IEnumerator ProjectileFanRoutine()
    {
        if (player == null || face == null) yield break;
        Vector2 origin = faceFireOrigin != null ? faceFireOrigin.position : face.position;
        Vector2 aim = ((Vector2)player.position - origin).normalized;
        if (aim.sqrMagnitude < 0.001f) aim = Vector2.down;
        if (combatEffects != null) combatEffects.SpawnCharge(faceFireOrigin != null ? faceFireOrigin : face, 0.52f, 0.32f, Color.white);
        if (bossAudio != null) bossAudio.PlayBeamCharge();
        yield return new WaitForSeconds(0.52f);
        int count = phase >= 3 ? 9 : 7;
        float spread = phase >= 3 ? 58f : 48f;
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0.5f : i / (float)(count - 1);
            float angle = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
            Vector2 dir = RotateVector(aim, angle);
            JHLProjectile projectile = JHLProjectile.Create(this, origin, dir, phase >= 3 ? 9.2f : 7.8f, 1);
            if (bossAudio != null) bossAudio.PlayProjectile();
            if (projectile != null) RegisterSpawnedObject(projectile.gameObject);
        }
        faceMotion.AddVelocityImpulse(-aim * 1.3f);
        if (cameraFx != null) cameraFx.PulseLight(0.46f);
        yield return new WaitForSeconds(0.32f);
    }

    private IEnumerator TwinSlamRoutine()
    {
        if (player == null || leftMotion == null || rightMotion == null) yield break;
        Vector2 center = ClampToArena(player.position, 1.45f);
        float radius = Mathf.Clamp(handSize.x * 0.145f, 0.72f, 1.02f);
        float offset = Mathf.Max(radius * 1.75f, 1.38f);
        Vector2 leftTarget = ClampToArena(center + Vector2.left * offset, 0.75f);
        Vector2 rightTarget = ClampToArena(center + Vector2.right * offset, 0.75f);
        GameObject a = CreateCircleTelegraph(leftTarget, radius, new Color(1f, 0.07f, 0.24f, 0.24f), 0.72f);
        GameObject b = CreateCircleTelegraph(rightTarget, radius, new Color(1f, 0.07f, 0.24f, 0.24f), 0.72f);
        leftMotion.SetTarget(new Vector3(leftTarget.x, leftTarget.y + 1.65f, 0f), JHLPartMotionState.Anticipation);
        rightMotion.SetTarget(new Vector3(rightTarget.x, rightTarget.y + 1.65f, 0f), JHLPartMotionState.Anticipation);
        yield return new WaitForSeconds(0.72f);
        DestroyTracked(a); DestroyTracked(b);
        leftMotion.SetMotionProfile(9.0f, 0.62f, 64f);
        rightMotion.SetMotionProfile(9.0f, 0.62f, 64f);
        leftMotion.SetTarget(leftTarget, JHLPartMotionState.Attack);
        rightMotion.SetTarget(rightTarget, JHLPartMotionState.Attack);
        leftMotion.AddVelocityImpulse(Vector2.down * 2.4f);
        rightMotion.AddVelocityImpulse(Vector2.down * 2.4f);
        yield return WaitForPartsSettle(leftMotion, rightMotion, 0.32f, 0.11f);
        DamagePlayerCircle(leftTarget, radius, 1);
        DamagePlayerCircle(rightTarget, radius, 1);
        SpawnImpact(leftTarget, radius * 1.15f); SpawnImpact(rightTarget, radius * 1.15f);
        if (bossAudio != null) bossAudio.PlayHandImpact();
        CameraFeedbackController feedback = CameraFeedbackController.Instance;
        if (feedback != null) feedback.Impact(CameraImpactLevelV11.Heavy, Vector2.down, false);
        leftMotion.AddVelocityImpulse(Vector2.up * 2.8f); rightMotion.AddVelocityImpulse(Vector2.up * 2.8f);
        yield return new WaitForSeconds(0.15f);
    }

    private IEnumerator SlamChainRoutine()
    {
        int count = phase >= 3 ? 4 : 3;
        JHLPartMotion motion = Random.value < 0.5f ? leftMotion : rightMotion;
        for (int i = 0; i < count; i++)
        {
            yield return HandSlamRoutine(motion, Mathf.Max(0.31f, 0.48f - i * 0.045f), 1);
            motion = motion == leftMotion ? rightMotion : leftMotion;
            yield return new WaitForSeconds(0.055f);
        }
    }

    private IEnumerator ScissorSweepRoutine()
    {
        bool first = Random.value < 0.5f;
        yield return HandSweepRoutine(first, 0.48f, 1);
        yield return new WaitForSeconds(0.05f);
        yield return HandSweepRoutine(!first, 0.36f, 1);
    }

    private IEnumerator LaserSweepRoutine()
    {
        if (face == null) yield break;
        float startAngle = Random.value < 0.5f ? -148f : -32f;
        float endAngle = startAngle < -90f ? -32f : -148f;
        Vector2 origin = faceFireOrigin != null ? faceFireOrigin.position : face.position;
        Vector2 startDir = AngleToVector(startAngle);
        Vector2 endDir = AngleToVector(endAngle);
        GameObject a = CreateLineTelegraph(origin, startDir, BeamLengthToArena(origin, startDir), 0.09f, new Color(1f, 0.07f, 0.26f, 0.22f), 0.70f);
        GameObject b = CreateLineTelegraph(origin, endDir, BeamLengthToArena(origin, endDir), 0.09f, new Color(1f, 0.07f, 0.26f, 0.16f), 0.70f);
        if (combatEffects != null) combatEffects.SpawnCharge(faceFireOrigin != null ? faceFireOrigin : face, 0.70f, 0.38f, Color.white);
        yield return new WaitForSeconds(0.70f);
        DestroyTracked(a); DestroyTracked(b);
        Vector2 dir = startDir;
        GameObject beam = CreateBeam(origin, dir, BeamLengthToArena(origin, dir), 0.17f, 1.45f, new Color(1f, 0.12f, 0.40f, 0.58f), Color.white);
        JHLBeamVisual visual = beam != null ? beam.GetComponent<JHLBeamVisual>() : null;
        float elapsed = 0f, nextDamage = 0f;
        while (elapsed < 1.45f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 1.45f);
            float angle = Mathf.Lerp(startAngle, endAngle, t);
            dir = AngleToVector(angle);
            origin = faceFireOrigin != null ? faceFireOrigin.position : face.position;
            float length = BeamLengthToArena(origin, dir);
            if (visual != null) visual.SetGeometry(origin, dir, length, 0.17f);
            if (elapsed >= nextDamage)
            {
                DamagePlayerBeam(origin, dir, length, 0.17f, 1);
                nextDamage = elapsed + 0.34f;
            }
            yield return null;
        }
        DestroyTracked(beam);
    }

    private IEnumerator CrossLaserRoutine()
    {
        if (face == null) yield break;
        float[] angles = { -121f, -59f };
        List<GameObject> warnings = new List<GameObject>();
        Vector2 origin = faceFireOrigin != null ? faceFireOrigin.position : face.position;
        for (int i = 0; i < angles.Length; i++)
        {
            Vector2 d = AngleToVector(angles[i]);
            warnings.Add(CreateLineTelegraph(origin, d, BeamLengthToArena(origin, d), 0.10f, new Color(1f, 0.06f, 0.28f, 0.24f), 0.72f));
        }
        yield return new WaitForSeconds(0.72f);
        for (int i = 0; i < warnings.Count; i++) DestroyTracked(warnings[i]);
        List<GameObject> beams = new List<GameObject>();
        origin = faceFireOrigin != null ? faceFireOrigin.position : face.position;
        for (int i = 0; i < angles.Length; i++)
        {
            Vector2 d = AngleToVector(angles[i]);
            float length = BeamLengthToArena(origin, d);
            beams.Add(CreateBeam(origin, d, length, 0.20f, 0.46f, new Color(1f, 0.10f, 0.38f, 0.62f), Color.white));
        }
        if (bossAudio != null) bossAudio.PlayBeamFire();
        if (cameraFx != null) cameraFx.PulseLight(0.68f);
        bool crossHit = false;
        float crossElapsed = 0f;
        while (crossElapsed < 0.46f)
        {
            crossElapsed += Time.deltaTime;
            if (!crossHit)
            {
                origin = faceFireOrigin != null ? faceFireOrigin.position : face.position;
                for (int i = 0; i < angles.Length && !crossHit; i++)
                {
                    Vector2 beamDir = AngleToVector(angles[i]);
                    float beamLength = BeamLengthToArena(origin, beamDir);
                    crossHit = TryDamagePlayerBeam(origin, beamDir, beamLength, 0.20f, 1);
                }
            }
            yield return null;
        }
        for (int i = 0; i < beams.Count; i++) DestroyTracked(beams[i]);
    }

    private IEnumerator PrisonBarrageRoutine()
    {
        if (player == null || leftMotion == null || rightMotion == null) yield break;
        Vector2 center = ClampToArena(player.position, 1.55f);
        float safeWidth = 3.25f;
        float offset = safeWidth * 0.5f + handSize.x * 0.5f;
        leftMotion.SetTarget(new Vector3(center.x - offset, center.y, 0f), JHLPartMotionState.Attack);
        rightMotion.SetTarget(new Vector3(center.x + offset, center.y, 0f), JHLPartMotionState.Attack);
        debugSafeWidth = safeWidth;
        if (cameraFx != null) cameraFx.SetPressure(0.42f);
        yield return WaitForPartsSettle(leftMotion, rightMotion, 0.80f, 0.12f);
        SetHandsSolid(true);
        for (int volley = 0; volley < 4; volley++)
        {
            if (player == null) break;
            Vector2 leftOrigin = leftFingerTip != null ? leftFingerTip.position : leftHand.position;
            Vector2 rightOrigin = rightFingerTip != null ? rightFingerTip.position : rightHand.position;
            Vector2 ld = ((Vector2)player.position - leftOrigin).normalized;
            Vector2 rd = ((Vector2)player.position - rightOrigin).normalized;
            JHLProjectile lp = JHLProjectile.Create(this, leftOrigin, ld, 8.5f, 1);
            JHLProjectile rp = JHLProjectile.Create(this, rightOrigin, rd, 8.5f, 1);
            if (bossAudio != null) bossAudio.PlayProjectile();
            if (lp != null) RegisterSpawnedObject(lp.gameObject);
            if (rp != null) RegisterSpawnedObject(rp.gameObject);
            yield return new WaitForSeconds(0.22f);
        }
        SetHandsSolid(false);
        if (cameraFx != null) cameraFx.SetPressure(0f);
        debugSafeWidth = -1f;
    }

    private IEnumerator CompressionBurstRoutine()
    {
        yield return CompressionRoutine(false);
        yield return new WaitForSeconds(0.14f);
        yield return ProjectileFanRoutine();
    }

    private IEnumerator LaserCurtainRoutine()
    {
        GetCameraFrame(out Vector3 center, out float halfW, out float halfH);
        int columns = 7;
        int safeIndex = columns / 2;
        float spacing = (halfW * 1.76f) / (columns - 1);
        float beamWidth = Mathf.Min(0.46f, spacing * 0.42f);
        List<GameObject> warnings = new List<GameObject>();
        List<Vector2> origins = new List<Vector2>();
        Vector2 down = Vector2.down;
        for (int i = 0; i < columns; i++)
        {
            if (Mathf.Abs(i - safeIndex) <= 1) continue;
            float x = center.x - halfW * 0.88f + spacing * i;
            Vector2 origin = new Vector2(x, center.y + halfH * 0.93f);
            origins.Add(origin);
            warnings.Add(CreateLineTelegraph(origin, down, halfH * 1.90f, beamWidth, new Color(1f, 0.05f, 0.26f, 0.23f), 0.90f));
        }
        yield return new WaitForSeconds(0.90f);
        for (int i = 0; i < warnings.Count; i++) DestroyTracked(warnings[i]);
        List<GameObject> beams = new List<GameObject>();
        for (int i = 0; i < origins.Count; i++)
        {
            float length = halfH * 1.90f;
            beams.Add(CreateBeam(origins[i], down, length, beamWidth, 1.35f, new Color(1f, 0.08f, 0.34f, 0.62f), Color.white));
        }
        if (bossAudio != null) bossAudio.PlayBeamFire();
        if (cameraFx != null) cameraFx.PulseHeavy(0.70f);
        bool curtainHit = false;
        float curtainElapsed = 0f;
        while (curtainElapsed < 1.35f && !dead)
        {
            curtainElapsed += Time.deltaTime;
            if (!curtainHit)
            {
                for (int i = 0; i < origins.Count && !curtainHit; i++)
                    curtainHit = TryDamagePlayerBeam(origins[i], down, halfH * 1.90f, beamWidth, 1);
            }
            yield return null;
        }
        for (int i = 0; i < beams.Count; i++) DestroyTracked(beams[i]);
    }

    private IEnumerator SpiralBeamRoutine()
    {
        if (face == null) yield break;
        Vector2 origin = faceFireOrigin != null ? faceFireOrigin.position : face.position;
        float[] offsets = { -24f, 0f, 24f };
        List<GameObject> warnings = new List<GameObject>();
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2 d = AngleToVector(-140f + offsets[i]);
            warnings.Add(CreateLineTelegraph(origin, d, BeamLengthToArena(origin, d), 0.07f, new Color(1f, 0.07f, 0.30f, 0.18f), 0.82f));
        }
        yield return new WaitForSeconds(0.82f);
        for (int i = 0; i < warnings.Count; i++) DestroyTracked(warnings[i]);
        List<GameObject> beams = new List<GameObject>();
        List<JHLBeamVisual> visuals = new List<JHLBeamVisual>();
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2 d = AngleToVector(-140f + offsets[i]);
            GameObject beam = CreateBeam(origin, d, BeamLengthToArena(origin, d), 0.13f, 1.75f, new Color(0.74f, 0.32f, 1f, 0.50f), Color.white);
            beams.Add(beam); visuals.Add(beam != null ? beam.GetComponent<JHLBeamVisual>() : null);
        }
        float elapsed = 0f, nextDamage = 0f;
        while (elapsed < 1.75f)
        {
            elapsed += Time.deltaTime;
            float baseAngle = Mathf.Lerp(-140f, -40f, Mathf.Clamp01(elapsed / 1.75f));
            origin = faceFireOrigin != null ? faceFireOrigin.position : face.position;
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2 d = AngleToVector(baseAngle + offsets[i]);
                float length = BeamLengthToArena(origin, d);
                if (visuals[i] != null) visuals[i].SetGeometry(origin, d, length, 0.13f);
                if (elapsed >= nextDamage) DamagePlayerBeam(origin, d, length, 0.13f, 1);
            }
            if (elapsed >= nextDamage) nextDamage = elapsed + 0.38f;
            yield return null;
        }
        for (int i = 0; i < beams.Count; i++) DestroyTracked(beams[i]);
    }

    private IEnumerator CrossfireBarrageRoutine()
    {
        if (player == null) yield break;
        GetCameraFrame(out Vector3 center, out float halfW, out _);
        leftMotion.SetTarget(center + Vector3.left * halfW * 0.62f, JHLPartMotionState.Attack);
        rightMotion.SetTarget(center + Vector3.right * halfW * 0.62f, JHLPartMotionState.Attack);
        yield return new WaitForSeconds(0.42f);
        int volleys = 7;
        for (int i = 0; i < volleys; i++)
        {
            if (player == null) break;
            bool leftFirst = i % 2 == 0;
            Transform tip = leftFirst ? leftFingerTip : rightFingerTip;
            Vector2 origin = tip != null ? tip.position : (leftFirst ? (Vector2)leftHand.position : (Vector2)rightHand.position);
            Vector2 dir = ((Vector2)player.position - origin).normalized;
            dir = RotateVector(dir, Random.Range(-6f, 6f));
            JHLProjectile p = JHLProjectile.Create(this, origin, dir, 9.2f, 1);
            if (bossAudio != null) bossAudio.PlayProjectile();
            if (p != null) RegisterSpawnedObject(p.gameObject);
            yield return new WaitForSeconds(0.115f);
        }
    }

    private IEnumerator SweepLaserComboRoutine()
    {
        bool sweepDirection = Random.value < 0.5f;
        yield return HandSweepRoutine(sweepDirection, 0.44f, 1);
        yield return new WaitForSeconds(0.04f);
        yield return StraightLaserRoutine(0.34f, 0.22f, 1);
    }

    private IEnumerator QuadSlamRoutine()
    {
        JHLPartMotion motion = Random.value < 0.5f ? leftMotion : rightMotion;
        for (int i = 0; i < 4; i++)
        {
            yield return HandSlamRoutine(motion, Mathf.Max(0.28f, 0.43f - i * 0.035f), 1);
            motion = motion == leftMotion ? rightMotion : leftMotion;
            yield return new WaitForSeconds(0.04f);
        }
    }

    private IEnumerator RapidLaserBurstRoutine()
    {
        for (int i = 0; i < 5; i++)
        {
            yield return StraightLaserRoutine(i == 0 ? 0.38f : 0.27f, 0.16f, 1);
            yield return new WaitForSeconds(0.045f);
        }
    }


    private IEnumerator MovingGateRoutine()
    {
        if (leftMotion == null || rightMotion == null || player == null) yield break;
        GetCameraFrame(out Vector3 center, out float halfW, out float halfH);
        float gapHalf = Mathf.Clamp(handSize.x * 0.24f, 1.45f, 2.15f);
        float startOffset = Random.Range(-halfW * 0.24f, halfW * 0.24f);
        float endOffset = -startOffset;
        Vector3 leftStart = center + new Vector3(-halfW - handSize.x * 0.58f, 0f, 0f);
        Vector3 rightStart = center + new Vector3(halfW + handSize.x * 0.58f, 0f, 0f);
        Vector3 leftGate = center + new Vector3(startOffset - gapHalf - handSize.x * 0.46f, 0f, 0f);
        Vector3 rightGate = center + new Vector3(startOffset + gapHalf + handSize.x * 0.46f, 0f, 0f);

        GameObject leftWarn = CreateRectTelegraph(center + Vector3.left * halfW * 0.55f,
            new Vector2(halfW * 0.72f, halfH * 1.65f), 0f, new Color(1f, 0.10f, 0.26f, 0.18f), 0.64f);
        GameObject rightWarn = CreateRectTelegraph(center + Vector3.right * halfW * 0.55f,
            new Vector2(halfW * 0.72f, halfH * 1.65f), 0f, new Color(1f, 0.10f, 0.26f, 0.18f), 0.64f);
        leftMotion.SetTarget(leftStart, JHLPartMotionState.Anticipation);
        rightMotion.SetTarget(rightStart, JHLPartMotionState.Anticipation);
        if (bossAudio != null) bossAudio.PlayHandWindup();
        yield return new WaitForSeconds(0.64f);
        DestroyTracked(leftWarn); DestroyTracked(rightWarn);

        leftMotion.SetMotionProfile(4.8f, 1.12f, 34f);
        rightMotion.SetMotionProfile(4.8f, 1.12f, 34f);
        leftMotion.SetTarget(leftGate, JHLPartMotionState.Attack);
        rightMotion.SetTarget(rightGate, JHLPartMotionState.Attack);
        if (cameraFx != null) cameraFx.SetPressure(0.42f);
        yield return WaitForPartsSettle(leftMotion, rightMotion, 0.86f, 0.12f);
        SetHandsSolid(true);

        float elapsed = 0f;
        while (elapsed < 1.15f && !dead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 1.15f));
            float gapCenter = Mathf.Lerp(startOffset, endOffset, t);
            leftMotion.SetTarget(center + new Vector3(gapCenter - gapHalf - handSize.x * 0.46f, 0f, 0f), JHLPartMotionState.Attack);
            rightMotion.SetTarget(center + new Vector3(gapCenter + gapHalf + handSize.x * 0.46f, 0f, 0f), JHLPartMotionState.Attack);
            yield return null;
        }
        SetHandsSolid(false);
        if (cameraFx != null) cameraFx.SetPressure(0f);
    }

    private IEnumerator DiagonalCrossPunchRoutine()
    {
        if (leftMotion == null || rightMotion == null || player == null) yield break;
        GetCameraFrame(out Vector3 center, out float halfW, out float halfH);
        bool invert = Random.value < 0.5f;
        Vector3 leftStart = center + new Vector3(-halfW - handSize.x * 0.5f, invert ? -halfH * 0.55f : halfH * 0.55f, 0f);
        Vector3 rightStart = center + new Vector3(halfW + handSize.x * 0.5f, invert ? halfH * 0.55f : -halfH * 0.55f, 0f);
        Vector3 leftEnd = center + new Vector3(halfW + handSize.x * 0.55f, invert ? halfH * 0.52f : -halfH * 0.52f, 0f);
        Vector3 rightEnd = center + new Vector3(-halfW - handSize.x * 0.55f, invert ? -halfH * 0.52f : halfH * 0.52f, 0f);
        Vector2 ld = ((Vector2)leftEnd - (Vector2)leftStart).normalized;
        Vector2 rd = ((Vector2)rightEnd - (Vector2)rightStart).normalized;
        float ll = Vector2.Distance(leftStart, leftEnd);
        float rl = Vector2.Distance(rightStart, rightEnd);
        GameObject a = CreateLineTelegraph(leftStart, ld, ll, handSize.y * 0.50f, new Color(1f,0.08f,0.24f,0.20f), 0.70f);
        GameObject b = CreateLineTelegraph(rightStart, rd, rl, handSize.y * 0.50f, new Color(1f,0.08f,0.24f,0.20f), 0.70f);
        leftMotion.SetTarget(leftStart, JHLPartMotionState.Anticipation);
        rightMotion.SetTarget(rightStart, JHLPartMotionState.Anticipation);
        yield return new WaitForSeconds(0.70f);
        DestroyTracked(a); DestroyTracked(b);
        leftMotion.SetMotionProfile(7.6f, 0.96f, 58f);
        rightMotion.SetMotionProfile(7.6f, 0.96f, 58f);
        leftMotion.SetTarget(leftEnd, JHLPartMotionState.Attack);
        rightMotion.SetTarget(rightEnd, JHLPartMotionState.Attack);
        bool leftHit=false, rightHit=false;
        float elapsed=0f;
        while (elapsed<0.55f)
        {
            elapsed += Time.deltaTime;
            if (!leftHit && TryDamagePlayerFromHandContact(leftHand,1)) leftHit=true;
            if (!rightHit && TryDamagePlayerFromHandContact(rightHand,1)) rightHit=true;
            yield return null;
        }
        if (bossAudio != null) bossAudio.PlaySweep();
        CameraFeedbackController f=CameraFeedbackController.Instance;
        if (f!=null) f.Impact(CameraImpactLevelV11.Heavy, Vector2.down, false);
    }

    private IEnumerator PredictiveBombardmentRoutine()
    {
        if (player == null) yield break;
        int count = phase >= 3 ? 7 : 5;
        float warning = phase >= 3 ? 0.42f : 0.52f;
        float radius = 0.88f;
        List<Vector2> targets = new List<Vector2>();
        List<GameObject> warnings = new List<GameObject>();
        Vector2 velocity = playerBody != null ? playerBody.linearVelocity : Vector2.zero;
        for (int i=0;i<count;i++)
        {
            float lead = 0.16f + i * 0.075f;
            Vector2 predicted = (Vector2)player.position + velocity * lead + Random.insideUnitCircle * 0.42f;
            predicted = ClampToArena(predicted, radius + 0.12f);
            targets.Add(predicted);
            warnings.Add(CreateCircleTelegraph(predicted, radius, new Color(1f,0.10f,0.26f,0.22f), warning + i*0.045f));
        }
        if (combatEffects != null) combatEffects.SpawnCharge(faceFireOrigin != null ? faceFireOrigin : face, warning, 0.50f, Color.white);
        if (bossAudio != null) bossAudio.PlayBeamCharge();
        yield return new WaitForSeconds(warning);
        for (int i=0;i<targets.Count;i++)
        {
            DestroyTracked(warnings[i]);
            SpawnImpact(targets[i], radius);
            DamagePlayerCircle(targets[i], radius, 1);
            yield return new WaitForSeconds(0.10f);
        }
    }

    private IEnumerator BeamPinchRoutine()
    {
        if (face == null || player == null) yield break;
        Vector2 origin = faceFireOrigin != null ? faceFireOrigin.position : face.position;
        float leftAngle = -138f;
        float rightAngle = -42f;
        const float beamWidth = 0.34f;
        GameObject lw = CreateLineTelegraph(origin, AngleToVector(leftAngle), BeamLengthToArena(origin, AngleToVector(leftAngle)), beamWidth*0.8f, new Color(1f,0.08f,0.30f,0.23f), 0.65f);
        GameObject rw = CreateLineTelegraph(origin, AngleToVector(rightAngle), BeamLengthToArena(origin, AngleToVector(rightAngle)), beamWidth*0.8f, new Color(1f,0.08f,0.30f,0.23f), 0.65f);
        yield return new WaitForSeconds(0.65f);
        DestroyTracked(lw); DestroyTracked(rw);
        GameObject lb = CreateBeam(origin, AngleToVector(leftAngle), BeamLengthToArena(origin, AngleToVector(leftAngle)), beamWidth, 1.8f, new Color(0.75f,0.12f,1f,0.62f), Color.white);
        GameObject rb = CreateBeam(origin, AngleToVector(rightAngle), BeamLengthToArena(origin, AngleToVector(rightAngle)), beamWidth, 1.8f, new Color(0.75f,0.12f,1f,0.62f), Color.white);
        JHLBeamVisual lv=lb!=null?lb.GetComponent<JHLBeamVisual>():null;
        JHLBeamVisual rv=rb!=null?rb.GetComponent<JHLBeamVisual>():null;
        float elapsed=0f, nextDamage=0f;
        while (elapsed<1.8f && !dead)
        {
            elapsed+=Time.deltaTime;
            float t=Mathf.Clamp01(elapsed/1.8f);
            float la=Mathf.Lerp(-138f,-103f,t);
            float ra=Mathf.Lerp(-42f,-77f,t);
            origin=faceFireOrigin!=null?faceFireOrigin.position:face.position;
            Vector2 ld=AngleToVector(la), rd=AngleToVector(ra);
            float llen=BeamLengthToArena(origin,ld), rlen=BeamLengthToArena(origin,rd);
            if(lv!=null)lv.SetGeometry(origin,ld,llen,beamWidth);
            if(rv!=null)rv.SetGeometry(origin,rd,rlen,beamWidth);
            if(elapsed>=nextDamage){DamagePlayerBeam(origin,ld,llen,beamWidth,1);DamagePlayerBeam(origin,rd,rlen,beamWidth,1);nextDamage=elapsed+0.48f;}
            yield return null;
        }
        DestroyTracked(lb); DestroyTracked(rb);
    }

    private IEnumerator RoarRepulseRoutine()
    {
        if (player == null || playerBody == null || faceMotion == null) yield break;
        CleanupSpawnedObjects(); SetHandsSolid(false);
        // Visible success before defensive pressure. Face stays damageable throughout.
        if (combatEffects != null) combatEffects.SpawnImpact(face.position, 0.9f, Color.white);
        if (bossAudio != null) bossAudio.PlayHit(true);
        yield return ReturnPartsToHomeAnimated(0.35f);
        if (dead || playerHealth == null || playerHealth.IsDead) yield break;
        Vector2 origin = face.position;
        Vector2 away = ((Vector2)player.position - origin).normalized;
        if (away.sqrMagnitude < 0.01f) away = Vector2.down;
        float length = BeamLengthToArena(origin, away);
        const float width = 2.1f;
        float warning = phase == 1 ? 0.85f : phase == 2 ? 0.75f : 0.65f;
        GameObject mark = CreateLineTelegraph(origin, away, length, width,
            new Color(1f, 0.68f, 0.12f, 0.35f), warning);
        faceMotion.SetScaleMultiplier(new Vector3(0.94f, 1.06f, 1f));
        if (bossAudio != null) bossAudio.PlayRoar();
        yield return new WaitForSeconds(warning);
        DestroyTracked(mark);
        if (dead || playerHealth == null || playerHealth.IsDead) yield break;
        Vector2 delta = playerBody.position - origin;
        float forward = Vector2.Dot(delta, away);
        float lateral = Mathf.Abs(delta.x * away.y - delta.y * away.x);
        Collider2D body = player.GetComponent<Collider2D>();
        float radius = body != null ? Mathf.Max(body.bounds.extents.x, body.bounds.extents.y) : 0.3f;
        PlayerDashController dash = player.GetComponent<PlayerDashController>();
        // Escaping the declared strip avoids the push; no automatic arena-edge displacement.
        if (forward >= -radius && forward <= length + radius && lateral <= width * 0.5f + radius)
        {
            if (dash != null) dash.CancelForExternalForce();
            inputLock = GameInputState.Acquire("JHLRoarRepulse");
            Vector2 start = playerBody.position;
            Vector2 target = ClampToArena(start + away * (phase == 1 ? 2.8f : phase == 2 ? 3.2f : 3.6f), 0.55f);
            target = ResolveForcePushTarget(start, target, radius);
            playerHealth.GrantTemporaryInvulnerability(0.95f);
            float elapsed = 0f;
            while (elapsed < 0.30f && !dead && playerBody != null && !playerHealth.IsDead)
            {
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / 0.30f);
                playerBody.MovePosition(Vector2.Lerp(start, target, 1f - Mathf.Pow(1f - t, 3f)));
                playerBody.linearVelocity = Vector2.zero;
                yield return new WaitForFixedUpdate();
            }
            ReleaseInputLock();
        }
        if (cameraFx != null) cameraFx.PulseLight(0.65f);
        faceMotion.SetScaleMultiplier(Vector3.one);
        faceMotion.SetTarget(faceHome, JHLPartMotionState.Recovery);
        yield return new WaitForSeconds(0.65f);
    }

    private IEnumerator RemoteSuppressionRoutine()
    {
        CleanupSpawnedObjects();
        if (bossAudio != null) bossAudio.PlayRemoteLock();
        for (int i = 0; i < 3 && !dead; i++)
        {
            yield return StraightLaserRoutine(phase >= 3 ? 0.60f : 0.72f, 0.52f, 1);
            yield return new WaitForSeconds(0.10f);
        }
    }

    private static Vector2 RotateVector(Vector2 vector, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
    }

    private IEnumerator FinalCompressionRoutine()
    {
        auxiliaryPatternRoutine = StartCoroutine(TrackingBeamRoutine(3.1f, 38f, 0.46f));
        yield return new WaitForSeconds(0.34f);
        yield return CompressionRoutine(true);
        if (auxiliaryPatternRoutine != null) StopCoroutine(auxiliaryPatternRoutine);
        auxiliaryPatternRoutine = null;
        CleanupBeamObjectsOnly();
    }

    private IEnumerator FullAccessRoutine()
    {
        fullAccessUsed = true;
        if (hud != null) hud.ShowPhase("FULL ACCESS");
        if (bossAudio != null) bossAudio.PlayFullAccess();
        yield return ReturnPartsToHomeAnimated(0.8f);
        yield return HandSlamRoutine(leftMotion, 0.95f, 2);
        yield return new WaitForSeconds(0.25f);
        if (!dead) yield return HandSlamRoutine(rightMotion, 0.85f, 2);
        yield return new WaitForSeconds(0.40f);
        if (!dead) yield return MassiveCentralBeamRoutine();
        if (hud != null) hud.ShowPhase(string.Empty);
    }

    private IEnumerator BeamBurstAtAngle(float angle, float warning, float width, int damage)
    {
        Vector2 origin = faceFireOrigin != null ? faceFireOrigin.position : face.position;
        Vector2 dir = AngleToVector(angle);
        float length = BeamLengthToArena(origin, dir);
        GameObject marker = CreateLineTelegraph(origin, dir, length, width, new Color(1f, 0.06f, 0.26f, 0.25f), warning);
        if (cameraFx != null) cameraFx.SetCharge(0.38f);
        yield return new WaitForSeconds(warning);
        DestroyTracked(marker);

        origin = faceFireOrigin != null ? faceFireOrigin.position : face.position;
        length = BeamLengthToArena(origin, dir);
        GameObject beam = CreateBeam(origin, dir, length, width, 0.20f, new Color(1f, 0.12f, 0.42f, 0.62f), Color.white);
        faceMotion.AddVelocityImpulse(-dir * 1.8f);
        if (cameraFx != null)
        {
            cameraFx.SetCharge(0f);
            cameraFx.PulseLight(0.72f);
        }
        yield return SustainStaticBeamDamage(origin, dir, length, width, damage, 0.20f);
        DestroyTracked(beam);
    }

    public bool HandleRequestedDeath(EnemyHealth requestedHealth, Vector2 hitDirection, EnemyHitKind hitKind)
    {
        if (combatV25 != null) return combatV25.Die(hitDirection);
        if (requestedHealth != health || dead) return false;
        dead = true;
        if (openingV15 != null) openingV15.ResetCombat();
        state = BossState.Dead;
        patternRunning = false;
        StopAllCoroutines();
        CleanupSpawnedObjects();
        SetTrails(false, false);
        ReleaseInputLock();
        SetCombatColliders(false);
        StartCoroutine(DeathRoutine(hitDirection));
        return true;
    }

    private IEnumerator DeathRoutine(Vector2 hitDirection)
    {
        inputLock = GameInputState.Acquire("JHLDeath");
        if (bossAudio != null) bossAudio.PlayDeath();
        if (bossMusic != null) bossMusic.CrossFadeBackToGameplay(1.55f);
        if (hud != null)
        {
            hud.ReportHealth(0, health != null ? health.MaxHealth : BossMaxHealth, true);
            hud.HideBossBarImmediate();
            hud.ShowPhase("ACCESS REVOKED");
        }

        GetCameraFrame(out Vector3 center, out float halfW, out float halfH);
        if (cameraFx != null)
        {
            cameraFx.SetPressure(1f);
            cameraFx.SetFraming(0.08f);
            cameraFx.PulseHeavy(1f);
        }
        CameraFeedbackController feedback = CameraFeedbackController.Instance;
        if (feedback != null) feedback.Impact(CameraImpactLevelV11.Boss, hitDirection, true);

        faceMotion.SetMotionProfile(2.0f, 1.0f, 36f);
        leftMotion.SetMotionProfile(2.1f, 1.0f, 40f);
        rightMotion.SetMotionProfile(2.1f, 1.0f, 40f);
        faceMotion.SetTarget(center + Vector3.up * (halfH + faceSize.y), JHLPartMotionState.Attack);
        leftMotion.SetTarget(center + new Vector3(-halfW - handSize.x, -halfH * 0.2f, 0f), JHLPartMotionState.Attack);
        rightMotion.SetTarget(center + new Vector3(halfW + handSize.x, -halfH * 0.2f, 0f), JHLPartMotionState.Attack);
        faceMotion.AddVelocityImpulse(new Vector2(0f, 6.5f));
        leftMotion.AddVelocityImpulse(new Vector2(-7f, 2.5f));
        rightMotion.AddVelocityImpulse(new Vector2(7f, 2.5f));
        faceMotion.AddAngularImpulse(42f);
        leftMotion.AddAngularImpulse(-58f);
        rightMotion.AddAngularImpulse(58f);
        faceMotion.SetScaleMultiplier(new Vector3(1.06f, 0.94f, 1f));
        leftMotion.SetScaleMultiplier(new Vector3(0.94f, 1.06f, 1f));
        rightMotion.SetScaleMultiplier(new Vector3(0.94f, 1.06f, 1f));

        float elapsed = 0f;
        const float duration = 1.55f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (t > 0.18f) SetVisualAlpha(1f - Mathf.SmoothStep(0f, 1f, (t - 0.18f) / 0.82f));
            yield return null;
        }

        if (cameraFx != null)
        {
            cameraFx.SetPressure(0f);
            cameraFx.SetFraming(0f);
            cameraFx.EndBoss();
        }
        if (hud != null) hud.HideAllImmediate();
        if (playerHealth != null && !playerHealth.IsDead) playerHealth.RestoreToFull();
        SpawnRewards();
        yield return WaitUnscaled(0.75f);
        ReleaseInputLock();
        if (mapManager != null) mapManager.NotifyJHLDefeated(ownerRoom);
        if (health != null) health.CompleteDeferredDeath(hitDirection);
    }

    private void SpawnRewards()
    {
        Vector3 origin = face != null ? face.position : transform.position;
        ByteDropper dropper = GetComponent<ByteDropper>();
        if (dropper == null) dropper = gameObject.AddComponent<ByteDropper>();
        dropper.ConfigureDrops(6, 6, 4);
        dropper.DropBytes(origin);
        for (int i = 0; i < 3; i++)
            AmmoDropper.SpawnPickupAt(origin + (Vector3)(Random.insideUnitCircle * 0.24f), 8, ownerRoom);
    }

    private void OnBossDamaged(EnemyHealth sender, int damage, Vector2 direction)
    {
        if (hud != null && health != null) hud.ReportHealth(health.CurrentHealth, health.MaxHealth, false);
        if (dead || health == null || health.IsDead) return;
        RegisterPressureV16(damage);
        bool heavy = damage >= 24;
        Vector2 hitPoint = health.LastDamageContext.HitPoint;
        if (Time.unscaledTime >= nextFaceHitFeedbackTimeV12)
        {
            nextFaceHitFeedbackTimeV12 = Time.unscaledTime + (heavy ? 0.10f : 0.075f);
            if (bossAudio != null) bossAudio.PlayHit(heavy);
            if (combatEffects != null) RegisterSpawnedObject(combatEffects.SpawnFaceHit(hitPoint, heavy));
            if (faceHitFeedbackRoutineV12 != null) StopCoroutine(faceHitFeedbackRoutineV12);
            faceHitFeedbackRoutineV12 = StartCoroutine(FaceHitFeedbackV12(heavy));
        }
        if (Time.time > staggerWindowUntil) { staggerDamage = 0f; staggerWindowUntil = Time.time + 0.8f; }
        staggerDamage += damage;
        if ((heavy || staggerDamage >= 65f) && Time.unscaledTime >= nextImpactV15)
        {
            nextImpactV15 = Time.unscaledTime + 0.20f;
            staggerDamage = 0f;
            if (GameFeelManager.Instance != null) GameFeelManager.Instance.DoHitStop(heavy ? 0.025f : 0.014f);
            if (cameraFx != null) cameraFx.PulseLight(0.23f);
            CameraFeedbackController feedback = CameraFeedbackController.Instance;
            if (feedback != null) feedback.Impact(CameraImpactLevelV11.Small, direction, false);
        }
    }

    private IEnumerator FaceHitFeedbackV12(bool heavy)
    {
        SpriteRenderer faceRenderer = face != null ? face.GetComponentInChildren<SpriteRenderer>() : null;
        if (faceRenderer == null) yield break;

        Color original = new Color(1f, 1f, 1f, faceRenderer.color.a);
        float duration = heavy ? 0.085f : 0.050f;
        float elapsed = 0f;
        while (elapsed < duration && !dead)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Digital boss: use cyan/magenta signal tearing instead of a generic white flash.
            Color glitchA = new Color(0.42f, 0.94f, 1f, original.a);
            Color glitchB = new Color(1f, 0.32f, 0.88f, original.a);
            Color target = Mathf.FloorToInt(t * 6f) % 2 == 0 ? glitchA : glitchB;
            faceRenderer.color = Color.Lerp(target, original, t);
            yield return null;
        }
        if (faceRenderer != null) faceRenderer.color = original;
        faceHitFeedbackRoutineV12 = null;
    }

    private void RequestAntiPressureCounter(JHLPatternKind pattern)
    {
        if (dead || Time.time < antiPressureCooldownUntil || antiPressurePending) return;
        antiPressurePending = true;
        antiPressurePattern = pattern;
        if (combatEffects != null && face != null) combatEffects.SpawnImpact(face.position, 0.65f, new Color(0.5f, 1f, 1f));
        if (bossAudio != null) bossAudio.PlayHit(true);
        // Finish the current committed attack; never cancel its warning into another hit.
    }

    /// <summary>
    /// Sustained face DPS is an explicit boss response, not another random attack. Interrupt
    /// the current pattern cleanly, clear its telegraphs/projectiles, run the counter, then
    /// restart the normal combat loop. Full Access is never interrupted.
    /// </summary>
    private IEnumerator InterruptForAntiPressure(JHLPatternKind pattern)
    {
        yield return null; // avoid mutating the currently executing damage callback stack
        if (dead || state != BossState.Combat || Time.time < antiPressureCooldownUntil)
        {
            antiPressureInterruptRoutine = null;
            yield break;
        }
        if (currentPattern == JHLPatternKind.FullAccess)
        {
            antiPressureInterruptRoutine = null;
            yield break;
        }

        if (combatRoutine != null)
        {
            StopCoroutine(combatRoutine);
            combatRoutine = null;
        }

        patternRunning = false;
        if (auxiliaryPatternRoutine != null)
        {
            StopCoroutine(auxiliaryPatternRoutine);
            auxiliaryPatternRoutine = null;
        }
        CleanupSpawnedObjects();
        SetTrails(false, false);
        SetHandsSolid(false);
        ReleaseInputLock();
        if (cameraFx != null)
        {
            cameraFx.SetPressure(0f);
            cameraFx.SetCharge(0f);
            cameraFx.SetFraming(0f);
        }

        currentPattern = pattern;
        lastPatternName = currentPattern.ToString();
        RememberPattern(currentPattern);
        antiPressurePending = false;
        meleePressureDamage = 0f;
        rangedPressureDamage = 0f;
        pressureWindowUntil = Time.time;
        antiPressureCooldownUntil = Time.time + 7.5f;
        state = BossState.Combat;
        patternRunning = true;

        yield return RunPattern(currentPattern);
        SetHandsSolid(false);
        patternRunning = false;
        if (!dead)
            yield return ReturnPartsToHomeAnimated(0.24f);

        antiPressureInterruptRoutine = null;
        if (!dead && health != null && !health.IsDead)
            combatRoutine = StartCoroutine(CombatLoop());
    }

    public void RegisterSpawnedObject(GameObject target)
    {
        spawnedObjects.RemoveAll(item => item == null);
        if (target != null && !spawnedObjects.Contains(target)) spawnedObjects.Add(target);
    }

    private void CleanupSpawnedObjects()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            GameObject go = spawnedObjects[i];
            if (go != null) Destroy(go);
        }
        spawnedObjects.Clear();
    }

    private void CleanupBeamObjectsOnly()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            GameObject go = spawnedObjects[i];
            if (go == null) { spawnedObjects.RemoveAt(i); continue; }
            if (!go.name.Contains("Beam") && !go.name.Contains("Laser")) continue;
            Destroy(go);
            spawnedObjects.RemoveAt(i);
        }
    }

    private void DestroyTracked(GameObject go)
    {
        if (go == null) return;
        spawnedObjects.Remove(go);
        Destroy(go);
    }

    private GameObject CreateCircleTelegraph(Vector2 center, float radius, Color color, float duration)
    {
        GameObject root = new GameObject("JHL_Telegraph_Circle");
        root.transform.SetParent(ownerRoom != null ? ownerRoom.transform : null, true);
        root.transform.position = center;
        root.transform.localScale = Vector3.one * radius * 2f;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(root.transform, false);
        SpriteRenderer fillSr = fill.AddComponent<SpriteRenderer>();
        fillSr.sprite = JHLRuntimeSprites.FilledCircle;
        fillSr.color = new Color(color.r, color.g, color.b, color.a * 0.42f);
        fillSr.sortingOrder = 34;

        GameObject ring = new GameObject("Ring");
        ring.transform.SetParent(root.transform, false);
        SpriteRenderer ringSr = ring.AddComponent<SpriteRenderer>();
        ringSr.sprite = JHLRuntimeSprites.Ring;
        ringSr.color = color;
        ringSr.sortingOrder = 35;

        GameObject countdown = new GameObject("Countdown");
        countdown.transform.SetParent(root.transform, false);
        SpriteRenderer countdownSr = countdown.AddComponent<SpriteRenderer>();
        countdownSr.sprite = JHLRuntimeSprites.Ring;
        countdownSr.color = new Color(1f, 0.92f, 0.96f, Mathf.Clamp01(color.a * 1.8f));
        countdownSr.sortingOrder = 36;

        JHLTelegraphVisual visual = root.AddComponent<JHLTelegraphVisual>();
        visual.Configure(duration, 12f, 0.055f);
        RegisterSpawnedObject(root);
        return root;
    }

    private GameObject CreateRectTelegraph(Vector2 center, Vector2 size, float angle, Color color, float duration)
    {
        GameObject root = CreateRectVisual("JHL_Telegraph_Rect", center, size, angle, color, 35);
        if (root != null)
        {
            JHLTelegraphVisual visual = root.AddComponent<JHLTelegraphVisual>();
            visual.Configure(duration, 11f, 0.025f);
        }
        return root;
    }

    private GameObject CreateLineTelegraph(Vector2 origin, Vector2 dir, float length, float width, Color color, float duration)
    {
        GameObject root = new GameObject("JHL_LaserTelegraph");
        root.transform.SetParent(ownerRoom != null ? ownerRoom.transform : null, true);
        SpriteRenderer sr = root.AddComponent<SpriteRenderer>();
        sr.sprite = JHLRuntimeSprites.WhitePixel;
        sr.color = color;
        sr.sortingOrder = 36;
        UpdateLineGeometry(root, origin, dir, length, width);
        JHLTelegraphVisual visual = root.AddComponent<JHLTelegraphVisual>();
        visual.Configure(duration, 15f, 0.015f);
        RegisterSpawnedObject(root);
        return root;
    }

    private GameObject CreateRectVisual(string name, Vector2 center, Vector2 size, float angle, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(ownerRoom != null ? ownerRoom.transform : null, true);
        go.transform.position = new Vector3(center.x, center.y, 0f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = JHLRuntimeSprites.WhitePixel;
        sr.color = color;
        sr.sortingOrder = order;
        RegisterSpawnedObject(go);
        return go;
    }

    private GameObject CreateBeam(Vector2 origin, Vector2 dir, float length, float width, float lifetime, Color outerColor, Color coreColor)
    {
        GameObject root = new GameObject("JHL_Beam");
        root.transform.SetParent(ownerRoom != null ? ownerRoom.transform : null, true);

        SpriteRenderer outer = root.AddComponent<SpriteRenderer>();
        outer.sprite = JHLRuntimeSprites.WhitePixel;
        outer.color = outerColor;
        outer.sortingOrder = 39;

        GameObject middleObject = new GameObject("Middle");
        middleObject.transform.SetParent(root.transform, false);
        SpriteRenderer middle = middleObject.AddComponent<SpriteRenderer>();
        middle.sprite = JHLRuntimeSprites.WhitePixel;
        middle.color = Color.Lerp(outerColor, coreColor, 0.58f);
        middle.sortingOrder = 40;

        GameObject coreObject = new GameObject("Core");
        coreObject.transform.SetParent(root.transform, false);
        SpriteRenderer core = coreObject.AddComponent<SpriteRenderer>();
        core.sprite = JHLRuntimeSprites.WhitePixel;
        core.color = coreColor;
        core.sortingOrder = 41;

        JHLBeamVisual beam = root.AddComponent<JHLBeamVisual>();
        beam.Initialize(outer, middle, core, width, lifetime);
        beam.SetGeometry(origin, dir, length, width);
        RegisterSpawnedObject(root);
        return root;
    }

    private void UpdateLineGeometry(GameObject go, Vector2 origin, Vector2 dir, float length, float width)
    {
        if (go == null) return;
        Vector2 safeDir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.down;
        Vector2 center = origin + safeDir * (length * 0.5f);
        go.transform.position = new Vector3(center.x, center.y, 0f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(safeDir.y, safeDir.x) * Mathf.Rad2Deg);
        go.transform.localScale = new Vector3(length, width, 1f);
    }

    private float BeamLengthToArena(Vector2 origin, Vector2 direction)
    {
        GetCameraFrame(out Vector3 cameraCenter, out float halfW, out float halfH);
        Bounds b = new Bounds(cameraCenter, new Vector3(halfW * 2f, halfH * 2f, 0f));
        Vector2 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
        float best = 40f;
        if (Mathf.Abs(dir.x) > 0.0001f)
        {
            float tx1 = (b.min.x - origin.x) / dir.x;
            float tx2 = (b.max.x - origin.x) / dir.x;
            if (tx1 > 0f) best = Mathf.Min(best, tx1);
            if (tx2 > 0f) best = Mathf.Min(best, tx2);
        }
        if (Mathf.Abs(dir.y) > 0.0001f)
        {
            float ty1 = (b.min.y - origin.y) / dir.y;
            float ty2 = (b.max.y - origin.y) / dir.y;
            if (ty1 > 0f) best = Mathf.Min(best, ty1);
            if (ty2 > 0f) best = Mathf.Min(best, ty2);
        }
        return Mathf.Clamp(best, 1f, 40f);
    }

    /// <summary>
    /// Sweep/contact damage for JHL hands. The wide sweep rectangle is Telegraph only;
    /// this method checks the hand's live world position and rotation against the
    /// player's collider. The visible stretched circle is approximated as an ellipse
    /// rather than using the entire warning lane or the hand's bounding rectangle.
    /// </summary>
    private bool TryDamagePlayerFromHandContact(Transform handTransform, int damage)
    {
        if (handTransform == null || damage <= 0)
            return false;

        JHLArticulatedHandV18 rig = handTransform.GetComponent<JHLArticulatedHandV18>();
        if (rig != null)
        {
            if (playerHealth == null || playerHealth.IsDead) return false;
            Collider2D body = playerHealth.GetComponent<Collider2D>();
            if (!rig.Touches(body)) return false;
            playerHealth.TakeDamage(damage);
            return true;
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        int mask = playerLayer >= 0 ? 1 << playerLayer : Physics2D.AllLayers;

        // Slightly inset from the placeholder sprite edge. This prevents the empty
        // corners of the stretched circular sprite's bounding box from causing hits.
        Vector2 broadSize = new Vector2(
            Mathf.Max(0.1f, handSize.x * 0.94f),
            Mathf.Max(0.1f, handSize.y * 0.94f));
        float angle = handTransform.eulerAngles.z;
        Collider2D[] candidates = Physics2D.OverlapBoxAll(handTransform.position, broadSize, angle, mask);

        float radiusX = Mathf.Max(0.05f, handSize.x * 0.47f);
        float radiusY = Mathf.Max(0.05f, handSize.y * 0.47f);
        Quaternion inverseRotation = Quaternion.Euler(0f, 0f, -angle);
        Vector2 center = handTransform.position;

        for (int i = 0; i < candidates.Length; i++)
        {
            Collider2D candidate = candidates[i];
            if (candidate == null)
                continue;

            PlayerHealth target = candidate.GetComponentInParent<PlayerHealth>();
            if (target == null)
                continue;

            // ClosestPoint means the player's actual collider must touch the live hand
            // ellipse. Merely being somewhere in the sweep warning rectangle is not a hit.
            Vector2 closestWorld = candidate.ClosestPoint(center);
            Vector3 local3 = inverseRotation * (Vector3)(closestWorld - center);
            float nx = local3.x / radiusX;
            float ny = local3.y / radiusY;
            if (nx * nx + ny * ny > 1f)
                continue;

            target.TakeDamage(damage);
            return true;
        }

        return false;
    }

    private void DamagePlayerCircle(Vector2 center, float radius, int damage)
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int mask = playerLayer >= 0 ? 1 << playerLayer : Physics2D.AllLayers;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, mask);
        for (int i = 0; i < hits.Length; i++)
        {
            PlayerHealth target = hits[i] != null ? hits[i].GetComponentInParent<PlayerHealth>() : null;
            if (target == null) continue;
            target.TakeDamage(damage);
            return;
        }
    }

    private bool TryDamagePlayerBox(Vector2 center, Vector2 size, float angle, int damage)
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int mask = playerLayer >= 0 ? 1 << playerLayer : Physics2D.AllLayers;
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, angle, mask);
        for (int i = 0; i < hits.Length; i++)
        {
            PlayerHealth target = hits[i] != null ? hits[i].GetComponentInParent<PlayerHealth>() : null;
            if (target == null) continue;
            target.TakeDamage(damage);
            return true;
        }
        return false;
    }

    private void DamagePlayerBox(Vector2 center, Vector2 size, float angle, int damage)
    {
        TryDamagePlayerBox(center, size, angle, damage);
    }

    private bool TryDamagePlayerBeam(Vector2 origin, Vector2 dir, float length, float width, int damage)
    {
        Vector2 safeDir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.down;
        Vector2 center = origin + safeDir * length * 0.5f;
        float angle = Mathf.Atan2(safeDir.y, safeDir.x) * Mathf.Rad2Deg;
        return TryDamagePlayerBox(center, new Vector2(length, width), angle, damage);
    }

    private void DamagePlayerBeam(Vector2 origin, Vector2 dir, float length, float width, int damage)
    {
        TryDamagePlayerBeam(origin, dir, length, width, damage);
    }

    /// <summary>
    /// Keeps a visible static beam dangerous for its whole displayed lifetime, but caps it
    /// to a single hit per beam instance. Entering after the firing frame can still hurt.
    /// </summary>
    private IEnumerator SustainStaticBeamDamage(Vector2 origin, Vector2 dir, float length, float width, int damage, float duration)
    {
        float elapsed = 0f;
        bool hit = false;
        float safeDuration = Mathf.Max(0.02f, duration);
        while (elapsed < safeDuration && !dead)
        {
            elapsed += Time.deltaTime;
            if (!hit)
                hit = TryDamagePlayerBeam(origin, dir, length, width, damage);
            yield return null;
        }
    }

    private IEnumerator WaitForPartsSettle(JHLPartMotion a, JHLPartMotion b, float maxDuration, float tolerance)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.03f, maxDuration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            bool aReady = a == null || (a.DistanceToTarget <= tolerance && a.Velocity.magnitude <= 0.34f);
            bool bReady = b == null || (b.DistanceToTarget <= tolerance && b.Velocity.magnitude <= 0.34f);
            if (aReady && bReady) yield break;
            yield return null;
        }
    }

    private IEnumerator ReturnPartsToHomeAnimated(float duration)
    {
        RecalculateScreenHomes(phase);
        faceMotion.SetMotionProfile(4.0f, 1.08f, 30f);
        leftMotion.SetMotionProfile(4.2f, 1.08f, 36f);
        rightMotion.SetMotionProfile(4.2f, 1.08f, 36f);
        SetHomeTargets(JHLPartMotionState.Recovery);
        faceMotion.SetScaleMultiplier(Vector3.one);
        leftMotion.SetScaleMultiplier(Vector3.one);
        rightMotion.SetScaleMultiplier(Vector3.one);
        faceMotion.SetRotation(0f, JHLPartMotionState.Recovery);
        leftMotion.SetRotation(0f, JHLPartMotionState.Recovery);
        rightMotion.SetRotation(0f, JHLPartMotionState.Recovery);
        yield return new WaitForSeconds(Mathf.Max(0.05f, duration));
    }

    private void RecalculateScreenHomes(int targetPhase)
    {
        GetCameraFrame(out Vector3 center, out float halfW, out float halfH);
        float faceY = 0.72f;
        float handX = targetPhase <= 1 ? 1.03f : targetPhase == 2 ? 0.89f : 0.75f;
        float handY = targetPhase <= 1 ? -0.06f : targetPhase == 2 ? -0.01f : 0.03f;
        faceHome = center + new Vector3(0f, halfH * faceY, 0f);
        leftHome = center + new Vector3(-halfW * handX, halfH * handY, 0f);
        rightHome = center + new Vector3(halfW * handX, halfH * handY, 0f);
    }

    private void SetHomeTargets(JHLPartMotionState motionState)
    {
        if (faceMotion != null) faceMotion.SetTarget(faceHome, motionState);
        if (leftMotion != null) leftMotion.SetTarget(leftHome, motionState);
        if (rightMotion != null) rightMotion.SetTarget(rightHome, motionState);
    }

    private void SnapPartsOffscreen()
    {
        GetCameraFrame(out Vector3 center, out float halfW, out float halfH);
        faceMotion.Snap(center + new Vector3(0f, halfH + faceSize.y * 0.72f, 0f), 0f);
        leftMotion.Snap(center + new Vector3(-halfW - handSize.x * 0.75f, 0f, 0f), -10f);
        rightMotion.Snap(center + new Vector3(halfW + handSize.x * 0.75f, 0f, 0f), 10f);
    }

    private void SnapPartsToHomes()
    {
        if (faceMotion != null) faceMotion.Snap(faceHome, 0f);
        if (leftMotion != null) leftMotion.Snap(leftHome, 0f);
        if (rightMotion != null) rightMotion.Snap(rightHome, 0f);
        faceMotion.SetScaleMultiplier(Vector3.one, true);
        leftMotion.SetScaleMultiplier(Vector3.one, true);
        rightMotion.SetScaleMultiplier(Vector3.one, true);
        Physics2D.SyncTransforms();
    }

    private Vector3 HomeFor(JHLPartMotion motion)
    {
        if (motion == faceMotion) return faceHome;
        if (motion == leftMotion) return leftHome;
        return rightHome;
    }

    private void GetCameraFrame(out Vector3 center, out float halfWidth, out float halfHeight)
    {
        if (RoomLayoutV24.TryGetBounds(ownerRoom, out Bounds fixedFrame))
        { center = fixedFrame.center; halfWidth = fixedFrame.extents.x; halfHeight = fixedFrame.extents.y; return; }
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            center = cam.transform.position;
            center.z = 0f;
            halfHeight = cam.orthographicSize;
            halfWidth = halfHeight * cam.aspect;
            return;
        }

        Bounds b = GetCombatBounds();
        center = b.center;
        center.z = 0f;
        halfWidth = Mathf.Max(4f, b.extents.x);
        halfHeight = Mathf.Max(2.5f, b.extents.y);
    }

    /// <summary>
    /// Full boss-room combat bounds. EnemySpawnArea is a normal-enemy spawning region and
    /// is noticeably smaller than the actual room, so boss space-control patterns must use
    /// the four real entry spawn points instead.
    /// </summary>
    private Bounds GetCombatBounds()
    {
        if (RoomLayoutV24.TryGetBounds(ownerRoom, out Bounds layoutBounds)) return layoutBounds;
        if (ownerRoom != null)
        {
            Transform left = ownerRoom.GetSpawnPoint(GateDirection.Left);
            Transform right = ownerRoom.GetSpawnPoint(GateDirection.Right);
            Transform top = ownerRoom.GetSpawnPoint(GateDirection.Top);
            Transform bottom = ownerRoom.GetSpawnPoint(GateDirection.Bottom);
            if (left != null && right != null && top != null && bottom != null)
            {
                const float edgePadding = 0.34f;
                float minX = Mathf.Min(left.position.x, right.position.x) - edgePadding;
                float maxX = Mathf.Max(left.position.x, right.position.x) + edgePadding;
                float minY = Mathf.Min(bottom.position.y, top.position.y) - edgePadding;
                float maxY = Mathf.Max(bottom.position.y, top.position.y) + edgePadding;
                return new Bounds(
                    new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f),
                    new Vector3(Mathf.Max(1f, maxX - minX), Mathf.Max(1f, maxY - minY), 1f));
            }
        }

        if (arenaBounds != null) return arenaBounds.bounds;
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float h = cam.orthographicSize * 2f;
            return new Bounds(cam.transform.position, new Vector3(h * cam.aspect, h, 1f));
        }
        return new Bounds(transform.position, new Vector3(14f, 8f, 1f));
    }

    private Vector2 ArenaEdgeFromPoint(Vector2 start, Vector2 direction, float margin)
    {
        Bounds b = GetCombatBounds();
        Vector2 d = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
        float best = float.PositiveInfinity;
        if (Mathf.Abs(d.x) > 0.0001f)
        {
            float tx = d.x > 0f ? (b.max.x - margin - start.x) / d.x : (b.min.x + margin - start.x) / d.x;
            if (tx > 0f) best = Mathf.Min(best, tx);
        }
        if (Mathf.Abs(d.y) > 0.0001f)
        {
            float ty = d.y > 0f ? (b.max.y - margin - start.y) / d.y : (b.min.y + margin - start.y) / d.y;
            if (ty > 0f) best = Mathf.Min(best, ty);
        }
        if (float.IsInfinity(best)) best = Mathf.Max(4f, b.extents.magnitude);
        return ClampToArena(start + d * Mathf.Max(0f, best), margin);
    }

    /// <summary>Stops the zero-damage roar push just before a real world wall/obstacle.</summary>
    private Vector2 ResolveForcePushTarget(Vector2 start, Vector2 desiredTarget, float clearance)
    {
        Vector2 delta = desiredTarget - start;
        float distance = delta.magnitude;
        if (distance <= 0.01f) return desiredTarget;

        int mask = 0;
        string[] blockingLayers = { "Wall", "Obstacle", "RoomBoundary" };
        for (int i = 0; i < blockingLayers.Length; i++)
        {
            int layer = LayerMask.NameToLayer(blockingLayers[i]);
            if (layer >= 0) mask |= 1 << layer;
        }
        if (mask == 0) return desiredTarget;

        float radius = 0.24f;
        Collider2D playerCollider = player != null ? player.GetComponentInChildren<Collider2D>() : null;
        if (playerCollider != null)
            radius = Mathf.Clamp(Mathf.Min(playerCollider.bounds.extents.x, playerCollider.bounds.extents.y) * 0.72f, 0.16f, 0.42f);

        RaycastHit2D[] hits = Physics2D.CircleCastAll(start, radius, delta / distance, distance, mask);
        float nearest = distance;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null) continue;
            if (player != null && (hitCollider.transform == player || hitCollider.transform.IsChildOf(player))) continue;
            if (hitCollider.GetComponentInParent<JHLBossController>() == this) continue;
            nearest = Mathf.Min(nearest, hits[i].distance);
        }

        float safeDistance = Mathf.Max(0f, nearest - Mathf.Max(0.05f, clearance));
        return start + delta.normalized * safeDistance;
    }

    private bool IsPlayerInFaceMeleeZone()
    {
        if (player == null || face == null) return false;
        Vector2 delta = (Vector2)player.position - (Vector2)face.position;
        float maxX = Mathf.Max(2.4f, faceSize.x * 0.56f);
        float maxDistance = Mathf.Max(3.0f, faceSize.y * 0.82f + 1.0f);
        return Mathf.Abs(delta.x) <= maxX && delta.magnitude <= maxDistance;
    }

    private Vector2 ClampToArena(Vector2 point, float margin)
    {
        Bounds b = GetCombatBounds();
        float safeMarginX = Mathf.Min(Mathf.Max(0f, margin), Mathf.Max(0f, b.extents.x - 0.05f));
        float safeMarginY = Mathf.Min(Mathf.Max(0f, margin), Mathf.Max(0f, b.extents.y - 0.05f));
        return new Vector2(
            Mathf.Clamp(point.x, b.min.x + safeMarginX, b.max.x - safeMarginX),
            Mathf.Clamp(point.y, b.min.y + safeMarginY, b.max.y - safeMarginY));
    }

    private static Vector2 AngleToVector(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void SetCombatColliders(bool enabledState)
    {
        // Hands are never weak points. Their physical wall colliders are controlled separately
        // and are enabled only by patterns whose actual mechanic is "the hands are walls".
        if (faceHurtbox != null) faceHurtbox.enabled = enabledState;
        if (leftHurtbox != null) leftHurtbox.enabled = false;
        if (rightHurtbox != null) rightHurtbox.enabled = false;
        if (!enabledState) SetHandsSolid(false);
    }

    private void SetHandsSolid(bool enabledState)
    {
        bool wasSolid = (leftHandBarrier != null && leftHandBarrier.IsBarrierEnabled) ||
                        (rightHandBarrier != null && rightHandBarrier.IsBarrierEnabled);
        if (leftHandBarrier != null) leftHandBarrier.SetBarrierEnabled(enabledState);
        if (rightHandBarrier != null) rightHandBarrier.SetBarrierEnabled(enabledState);
        if (enabledState && !wasSolid)
        {
            Physics2D.SyncTransforms();
            if (bossAudio != null) bossAudio.PlayHandWallLock();
        }
    }

    private void SetVisualAlpha(float alpha)
    {
        float a = Mathf.Clamp01(alpha);
        for (int i = 0; i < structureRenderers.Length; i++)
        {
            SpriteRenderer sr = structureRenderers[i];
            if (sr == null) continue;
            Color c = Color.white;
            c.a = a;
            sr.color = c;
        }
    }

    private void SetTrailForMotion(JHLPartMotion motion, bool enabledState)
    {
        if (combatEffects == null) return;
        if (motion == leftMotion) combatEffects.SetTrail(leftTrail, enabledState);
        else if (motion == rightMotion) combatEffects.SetTrail(rightTrail, enabledState);
    }

    private void SetTrails(bool left, bool right)
    {
        if (combatEffects == null) return;
        combatEffects.SetTrail(leftTrail, left);
        combatEffects.SetTrail(rightTrail, right);
    }

    private void SpawnImpact(Vector2 position, float radius)
    {
        if (combatEffects == null) return;
        combatEffects.SpawnImpact(position, radius, new Color(1f, 1f, 1f, 0.90f), ownerRoom != null ? ownerRoom.transform : null);
    }

    private JHLPartMotion ClosestMotion(Vector2 point)
    {
        // Hands are non-damageable hazards in V4, therefore legitimate boss damage can only come from the face.
        return faceMotion;
    }

    private string PhaseLabel()
    {
        if (phase <= 1) return string.Empty;
        if (phase == 2) return "CONTROL OVERRIDE";
        return "ADMIN OVERRIDE";
    }

    private void ReleaseInputLock()
    {
        if (inputLock == null) return;
        inputLock.Dispose();
        inputLock = null;
    }

    private static IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;
        float safe = Mathf.Max(0f, duration);
        while (elapsed < safe)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
