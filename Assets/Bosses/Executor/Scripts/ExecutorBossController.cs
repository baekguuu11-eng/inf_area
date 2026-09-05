using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public sealed class ExecutorBossController : MonoBehaviour, IEnemyDeathOverride
{
    private enum ExecutorState
    {
        Dormant,
        Intro,
        Idle,
        Attacking,
        Burrowing,
        Underground,
        Emerging,
        PhaseTransition,
        FinalStand,
        Dead
    }

    private enum PatternKind
    {
        Aimed,
        Spread,
        Artillery,
        CrossBarrage,
        ChainArtillery,
        FinalStandArtillery
    }

    private struct DamageStamp
    {
        public float Time;
        public int Damage;
    }

    private sealed class PendingStrike
    {
        public ExecutorTelegraph Telegraph;
        public Vector3 Position;
        public float DetonateAt;
        public bool Detonated;
    }

    private MapManager mapManager;
    private RoomController ownerRoom;
    private BoxCollider2D arenaBounds;
    private EnemyHealth health;
    private BoxCollider2D hurtbox;
    private BoxCollider2D solidCollider;
    private Transform visualRoot;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer leftEye;
    private SpriteRenderer rightEye;
    private SpriteRenderer muzzleGlow;
    private SpriteRenderer shadowRenderer;
    private SpriteRenderer groundSocketBack;
    private SpriteRenderer groundSocketFront;
    private SpriteRenderer groundSocketCracks;
    private Transform muzzlePoint;
    private PlayerHealth playerHealth;
    private Transform player;
    private ExecutorBossHUD hud;
    private ExecutorBossMusic music;
    private ExecutorBossAudio bossAudio;
    private ExecutorCutsceneIsolation cutsceneIsolation;
    private ExecutorCameraDirector cameraDirector;
    private IDisposable introInputLock;
    private IDisposable phaseInputLock;
    private IDisposable deathInputLock;
    private Coroutine combatRoutine;
    private ExecutorState state = ExecutorState.Dormant;
    private readonly List<DamageStamp> recentDamage = new List<DamageStamp>();
    private readonly List<GameObject> spawnedObjects = new List<GameObject>();

    private int phase = 1;
    private bool combatStarted;
    private bool dead;
    private bool phaseTransitionRequested;
    private bool finalStandRequested;
    private bool finalStandActive;
    private bool burrowRequested;
    private bool firstAttackPending = true;
    private int attackOutputCount;
    private const int BossMaxHealth = 2400;
    private const float FinalStandHealthRatio = 0.15f;
    private const float FinalStandArtilleryCooldown = 7.0f;
    private float lastBurrowTime;
    private float nextFinalStandArtilleryAllowedTime;
    private int artilleryBurstSequence;
    private float nextBurrowAllowedTime;
    private int rangedPatternsSinceBurrow = 1;
    private PatternKind lastPattern = PatternKind.Aimed;
    private int repeatedPatternCount;
    private Vector3 visualBaseLocalPosition;
    private Vector3 visualBaseScale;
    private Vector3 bodyBaseScale;
    private Color bodyBaseColor = Color.white;
    private bool playerDefeatHandled;
    private bool phaseTwoVisualsActive;
    private float phaseTwoSmokeTimer;
    private Vector3 leftEyeBaseLocalPosition;
    private Vector3 rightEyeBaseLocalPosition;
    private Coroutine hitReactionRoutine;
    private float nextHeavyHitFeedbackTime;
    private float nextVisualHitReactionTime;
    private Vector3 hitReactionAnchorPosition;
    private PatternKind debugSelectedPattern = PatternKind.Aimed;
    private bool debugForceSelectedPattern;

    public EnemyHealth Health => health;
    public RoomController OwnerRoom => ownerRoom;
    public bool IsDead => dead;
    public int DebugPhase => finalStandActive ? 3 : phase;
    public string DebugStateName => state.ToString();
    public string DebugLastPatternName => lastPattern.ToString();
    public string DebugSelectedPatternName => debugSelectedPattern.ToString();

    public void DebugCyclePattern(int delta)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Array values = Enum.GetValues(typeof(PatternKind));
        int count = values.Length;
        int current = (int)debugSelectedPattern;
        current = (current + delta) % count;
        if (current < 0) current += count;
        debugSelectedPattern = (PatternKind)current;
#endif
    }

    public void DebugForcePattern()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (combatStarted && !dead) debugForceSelectedPattern = true;
#endif
    }
    public int ProjectileVisualStage => finalStandActive ? 3 : Mathf.Clamp(phase, 1, 2);
    public bool CanReceivePersistentDamage => !dead && combatStarted &&
        state != ExecutorState.Dormant && state != ExecutorState.Intro &&
        state != ExecutorState.Burrowing && state != ExecutorState.Underground &&
        state != ExecutorState.Emerging;

    public void Initialize(MapManager manager, RoomController room, BoxCollider2D arena,
        EnemyHealth enemyHealth, BoxCollider2D bossHurtbox, BoxCollider2D bossSolidCollider, Transform bossVisualRoot,
        SpriteRenderer bossBodyRenderer, SpriteRenderer eyeLeft, SpriteRenderer eyeRight,
        SpriteRenderer muzzleRenderer, Transform firePoint, SpriteRenderer groundShadowRenderer,
        SpriteRenderer socketBackRenderer, SpriteRenderer socketFrontRenderer, SpriteRenderer socketCrackRenderer)
    {
        mapManager = manager;
        ownerRoom = room;
        arenaBounds = arena;
        health = enemyHealth;
        hurtbox = bossHurtbox;
        solidCollider = bossSolidCollider;
        visualRoot = bossVisualRoot;
        bodyRenderer = bossBodyRenderer;
        leftEye = eyeLeft;
        rightEye = eyeRight;
        muzzleGlow = muzzleRenderer;
        shadowRenderer = groundShadowRenderer;
        groundSocketBack = socketBackRenderer;
        groundSocketFront = socketFrontRenderer;
        groundSocketCracks = socketCrackRenderer;
        muzzlePoint = firePoint;

        visualBaseLocalPosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        visualBaseScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
        bodyBaseScale = bodyRenderer != null ? bodyRenderer.transform.localScale : Vector3.one;
        bodyBaseColor = Color.white;
        if (bodyRenderer != null)
        {
            bodyRenderer.sprite = ExecutorRuntimeSprites.LoadBody();
            bodyRenderer.color = bodyBaseColor;
        }
        leftEyeBaseLocalPosition = leftEye != null ? leftEye.transform.localPosition : Vector3.zero;
        rightEyeBaseLocalPosition = rightEye != null ? rightEye.transform.localPosition : Vector3.zero;

        playerHealth = FindAnyObjectByType<PlayerHealth>();
        player = playerHealth != null ? playerHealth.transform : null;
        if (health != null)
        {
            health.SetMaxHealth(BossMaxHealth, true);
            health.Damaged += HandleDamaged;
        }

        hud = ExecutorBossHUD.Create();
        music = ExecutorBossMusic.Create(transform);
        bossAudio = ExecutorBossAudio.Create(transform);
        cutsceneIsolation = new ExecutorCutsceneIsolation();
        cameraDirector = GetComponent<ExecutorCameraDirector>();
        if (cameraDirector == null) cameraDirector = gameObject.AddComponent<ExecutorCameraDirector>();
        cameraDirector.ConfigureRoomBounds(ownerRoom);
        SetHurtboxEnabled(false);
        SetSolidColliderEnabled(false);
        SetMachineLights(0f);
        SetGroundSocketVisible(false, 0f);
        SetVisualUnderground(true);
    }

    public void BeginIntro()
    {
        if (dead || combatStarted || state == ExecutorState.Intro)
            return;
        StartCoroutine(IntroRoutine());
    }


#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void DebugBeginCombatAtHealth(float normalizedHealth)
    {
        if (dead || health == null)
            return;

        StopAllCoroutines();
        CleanupSpawnedCombatObjects();
        RemoveGroundWarningMarks();
        if (bossAudio != null) bossAudio.StopAllLoops();
        if (cutsceneIsolation != null) cutsceneIsolation.Restore();

        if (introInputLock != null)
        {
            introInputLock.Dispose();
            introInputLock = null;
        }
        if (phaseInputLock != null)
        {
            phaseInputLock.Dispose();
            phaseInputLock = null;
        }

        phase = 1;
        combatStarted = false;
        dead = false;
        phaseTransitionRequested = false;
        finalStandRequested = false;
        finalStandActive = false;
        burrowRequested = false;
        phaseTwoVisualsActive = false;
        playerDefeatHandled = false;
        firstAttackPending = true;
        rangedPatternsSinceBurrow = 1;
        recentDamage.Clear();

        if (visualRoot != null)
        {
            visualRoot.localPosition = visualBaseLocalPosition;
            visualRoot.localScale = visualBaseScale;
            visualRoot.localRotation = Quaternion.identity;
        }
        ApplyDamageStageVisual(1);
        SetVisualUnderground(false);
        SetMachineLights(1f);
        SetHurtboxEnabled(true);
        SetSolidColliderEnabled(true);

        if (hud != null)
        {
            hud.HideAllImmediate();
            hud.SetSkipVisible(false);
            hud.SetCinematicBarsImmediate(false);
            hud.ShowBossBar(health.CurrentHealth, health.MaxHealth);
        }
        if (cameraDirector != null)
            cameraDirector.SnapToCombat();
        if (music != null)
            music.PlayPhaseOne(0.12f);

        FinishIntroAndStartCombat();

        float clamped = Mathf.Clamp(normalizedHealth, 0.01f, 1f);
        int targetHealth = Mathf.Clamp(Mathf.RoundToInt(health.MaxHealth * clamped), 1, health.MaxHealth);
        int damage = Mathf.Max(0, health.CurrentHealth - targetHealth);
        if (damage > 0)
        {
            Vector2 hitPoint = (Vector2)transform.position + Vector2.up;
            health.TakeDamage(new DamageContext(damage, Vector2.down, hitPoint, EnemyHitKind.Environment));
        }
    }
#endif

    public void RegisterSpawnedObject(GameObject target)
    {
        if (target != null)
            spawnedObjects.Add(target);
    }

    private IEnumerator IntroRoutine()
    {
        state = ExecutorState.Intro;
        introInputLock = GameInputState.Acquire("ExecutorIntro");
        SetHurtboxEnabled(false);
        SetSolidColliderEnabled(false);
        SetGroundSocketVisible(false, 0f);
        SetVisualUnderground(true);

        hud.HideAllImmediate();
        hud.SetSkipVisible(false);
        music.PrepareSilentIntro();
        cutsceneIsolation.Begin(hud.CanvasComponent, transform);
        cameraDirector.Capture();
        StartCoroutine(hud.AnimateCinematicBars(true, 0.55f));

        float introStart = Time.unscaledTime;
        bool skipEnabled = false;
        Func<bool> skipCheck = () =>
        {
            if (!TryReadSkip(ref skipEnabled, introStart)) return false;
            SkipIntro();
            return true;
        };

        float baseSize = Mathf.Max(0.1f, cameraDirector.CombatOrthoSize);
        Vector3 playerFocus = GetPlayerPosition() + new Vector3(0f, 0.18f, 0f);
        Vector3 floorFocus = transform.position + new Vector3(0f, 0.10f, 0f);
        Vector3 closeFloorFocus = transform.position + new Vector3(0f, 0.18f, 0f);

        // 0.0~1.0: 음악과 HUD를 비운 정적. 포탈 연출은 사용하지 않는다.
        yield return cameraDirector.MoveTo(playerFocus, baseSize * 0.96f, 0.72f, skipCheck);
        if (state != ExecutorState.Intro) yield break;
        bossAudio.StartRumble(0.08f);
        yield return WaitIntroSeconds(0.32f, skipCheck);
        if (state != ExecutorState.Intro) yield break;

        // 1.0~2.7: 카메라가 플레이어를 떠나 지면을 천천히 찾아간다.
        yield return cameraDirector.MoveTo(floorFocus, baseSize * 0.76f, 1.55f, skipCheck);
        if (state != ExecutorState.Intro) yield break;
        SetGroundSocketVisible(true, 0.05f);
        ExecutorCombatEffects.SpawnGroundCrack(this, transform.position, 0.82f, 7.0f,
            new Color(0.22f, 0.025f, 0.025f, 0.35f));

        // 2.7~4.8: 바닥판과 작은 파편이 서서히 떨리며 지하 진동이 커진다.
        float elapsed = 0f;
        float nextDust = 0.22f;
        float nextRock = 0.58f;
        bool creakPlayed = false;
        while (elapsed < 2.10f)
        {
            if (skipCheck()) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 2.10f);
            bossAudio.SetRumbleVolume(Mathf.Lerp(0.08f, 0.27f, t * t));
            SetGroundSocketIntensity(Mathf.Lerp(0.06f, 0.52f, t));
            if (!creakPlayed && t > 0.36f)
            {
                creakPlayed = true;
                bossAudio.PlayGroundCreak(0.38f);
            }
            if (elapsed >= nextDust)
            {
                nextDust += Mathf.Lerp(0.45f, 0.22f, t);
                ExecutorCombatEffects.SpawnDustBurst(this, transform.position, t > 0.70f ? 3 : 1,
                    Mathf.Lerp(0.35f, 0.90f, t));
            }
            if (elapsed >= nextRock)
            {
                nextRock += Mathf.Lerp(0.62f, 0.34f, t);
                ExecutorCombatEffects.SpawnRockBurst(this, transform.position, t > 0.75f ? 2 : 1,
                    Mathf.Lerp(0.45f, 1.05f, t), 0.48f, 0.05f, 0.09f);
            }
            if (GameFeelManager.Instance != null)
                GameFeelManager.Instance.DirectionalShake(0.055f, Mathf.Lerp(0.004f, 0.022f, t), Vector2.down);
            yield return null;
        }

        // 4.8~6.4: 바닥으로 더 가까이 들어가며 압력, 균열, 파편을 확실히 보여 준다.
        yield return cameraDirector.MoveTo(closeFloorFocus, baseSize * 0.60f, 0.88f, skipCheck);
        if (state != ExecutorState.Intro) yield break;
        bossAudio.PlayGroundCreak(0.62f);
        ExecutorCombatEffects.SpawnGroundCrack(this, transform.position, 1.30f, 4.2f,
            new Color(0.58f, 0.035f, 0.025f, 0.84f));
        elapsed = 0f;
        nextDust = 0f;
        nextRock = 0.12f;
        while (elapsed < 1.55f)
        {
            if (skipCheck()) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 1.55f);
            bossAudio.SetRumbleVolume(Mathf.Lerp(0.28f, 0.48f, t));
            SetGroundSocketIntensity(Mathf.Lerp(0.54f, 1.20f, t));
            if (elapsed >= nextDust)
            {
                nextDust += Mathf.Lerp(0.26f, 0.10f, t);
                ExecutorCombatEffects.SpawnDustBurst(this, transform.position, t > 0.64f ? 5 : 3,
                    Mathf.Lerp(0.85f, 1.90f, t));
            }
            if (elapsed >= nextRock)
            {
                nextRock += Mathf.Lerp(0.34f, 0.15f, t);
                ExecutorCombatEffects.SpawnRockBurst(this, transform.position, t > 0.65f ? 3 : 1,
                    Mathf.Lerp(0.9f, 1.8f, t), 0.52f, 0.06f, 0.14f);
            }
            if (GameFeelManager.Instance != null)
                GameFeelManager.Instance.DirectionalShake(0.06f, Mathf.Lerp(0.018f, 0.055f, t), Vector2.down);
            yield return null;
        }

        // 6.4~8.2: 상단 일부가 천천히 드러나는 1차 상승.
        Vector3 hidden = visualBaseLocalPosition + Vector3.down * 3.15f;
        Vector3 partial = visualBaseLocalPosition + Vector3.down * 1.18f;
        Vector3 overshoot = visualBaseLocalPosition + Vector3.up * 0.18f;
        if (visualRoot != null)
        {
            visualRoot.localPosition = hidden;
            visualRoot.localScale = new Vector3(1.08f, 0.88f, 1f);
        }
        SetShadowVisible(false);
        bossAudio.PlayBurrow(0.46f);
        elapsed = 0f;
        nextDust = 0f;
        while (elapsed < 1.30f)
        {
            if (skipCheck()) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 1.30f);
            float eased = t * t * (3f - 2f * t);
            if (visualRoot != null)
            {
                visualRoot.localPosition = Vector3.Lerp(hidden, partial, eased);
                visualRoot.localScale = Vector3.Lerp(new Vector3(1.08f, 0.88f, 1f), new Vector3(1.02f, 0.97f, 1f), eased);
            }
            if (elapsed >= nextDust)
            {
                nextDust += 0.13f;
                ExecutorCombatEffects.SpawnDustBurst(this, transform.position, 3, Mathf.Lerp(1.05f, 1.65f, t));
            }
            if (GameFeelManager.Instance != null)
                GameFeelManager.Instance.DirectionalShake(0.05f, Mathf.Lerp(0.025f, 0.065f, t), Vector2.up);
            yield return null;
        }

        // 8.2~8.9: 중간에서 멈추고 먼지가 안쪽으로 빨려 들어가는 압력 재축적.
        bossAudio.SetRumbleVolume(0.54f);
        ExecutorCombatEffects.SpawnInwardDust(this, transform.position, 18, 3.3f);
        // V11: 팀 피드백 - 사용자 제공 돌출 표식을 실제 돌출 약 0.5초 전부터 보여 준다.
        ExecutorCombatEffects.SpawnEmergenceImpactDecal(this, transform.position, 1.22f, 1.08f);
        yield return WaitIntroSeconds(0.58f, skipCheck);
        if (state != ExecutorState.Intro) yield break;
        bossAudio.StopRumble();
        yield return WaitIntroSeconds(0.12f, skipCheck);
        if (state != ExecutorState.Intro) yield break;

        // 8.9~9.8: 최종 돌출. 기존 돌출 경고는 이 순간 제거하고,
        // 사용자 제공 충격 마커를 실제 돌출 순간에만 짧게 표시한다.
        RemoveGroundWarningMarks();
        bossAudio.PlayRiseImpact();
        ExecutorCombatEffects.SpawnDustBurst(this, transform.position, 40, 4.6f);
        ExecutorCombatEffects.SpawnRockBurst(this, transform.position, 24, 5.5f, 0.90f, 0.10f, 0.34f);
        ExecutorCombatEffects.SpawnMetalSparks(this, transform.position + Vector3.up * 0.18f, 10);
        cameraDirector.Punch(Vector2.up, 0.24f, 0.23f);
        if (GameFeelManager.Instance != null)
        {
            GameFeelManager.Instance.DoHitStop(0.042f);
            GameFeelManager.Instance.Shake(0.27f, 0.18f);
        }
        elapsed = 0f;
        while (elapsed < 0.34f)
        {
            if (skipCheck()) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.34f);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            if (visualRoot != null)
            {
                visualRoot.localPosition = Vector3.Lerp(partial, overshoot, eased);
                visualRoot.localScale = Vector3.Lerp(new Vector3(1.02f, 0.97f, 1f), new Vector3(0.97f, 1.08f, 1f), eased);
            }
            if (t > 0.28f) SetShadowVisible(true);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < 0.24f)
        {
            if (skipCheck()) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.24f);
            if (visualRoot != null)
            {
                visualRoot.localPosition = Vector3.Lerp(overshoot, visualBaseLocalPosition, 1f - Mathf.Pow(1f - t, 2f));
                visualRoot.localScale = Vector3.Lerp(new Vector3(0.97f, 1.08f, 1f), visualBaseScale, t);
            }
            yield return null;
        }
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualBaseLocalPosition;
            visualRoot.localScale = visualBaseScale;
        }
        SetShadowVisible(true);
        cameraDirector.Punch(Vector2.down, 0.13f, 0.09f);
        ExecutorCombatEffects.SpawnDustBurst(this, transform.position, 12, 1.65f);

        // 9.8~11.6: 눈과 발사구 클로즈업, 시스템 문구와 보스 이름을 글자 단위로 출력한다.
        Vector3 faceFocus = transform.position + new Vector3(0f, 1.56f, 0f);
        yield return cameraDirector.MoveTo(faceFocus, baseSize * 0.53f, 0.72f, skipCheck);
        if (state != ExecutorState.Intro) yield break;
        bossAudio.PlayMachineActivate();
        yield return FlickerMachineLights(0.70f, skipCheck);
        if (state != ExecutorState.Intro) yield break;

        yield return hud.TypeIntroMessage("비인가 개체 확인", 0.055f,
            () => bossAudio.PlayTypeClick(false), skipCheck);
        if (state != ExecutorState.Intro) yield break;
        yield return WaitIntroSeconds(0.18f, skipCheck);
        yield return hud.TypeIntroMessage("집행 절차 개시", 0.060f,
            () => bossAudio.PlayTypeClick(false), skipCheck);
        if (state != ExecutorState.Intro) yield break;
        yield return WaitIntroSeconds(0.24f, skipCheck);
        hud.ShowIntroMessage(string.Empty);

        yield return hud.TypeCinematicTitle("집행자", 0.15f, 0.52f,
            (index, character) => bossAudio.PlayBossNameHit(index), skipCheck);
        if (state != ExecutorState.Intro) yield break;

        // 일반 HUD/오디오는 전투 직전에 복구하고 보스 음악과 체력바를 함께 시작한다.
        cutsceneIsolation.Restore();
        music.PlayPhaseOne(0.48f);
        yield return hud.RevealBossBar(health.CurrentHealth, health.MaxHealth, 0.48f);
        StartCoroutine(hud.AnimateCinematicBars(false, 0.44f));
        yield return cameraDirector.ReturnToCombat(0.82f, skipCheck);
        if (state != ExecutorState.Intro) yield break;

        hud.SetSkipVisible(false);
        cameraDirector.RestorePixelPerfect();
        FinishIntroAndStartCombat();
    }

    private IEnumerator WaitIntroSeconds(float duration, Func<bool> skipCheck)
    {
        float elapsed = 0f;
        float safe = Mathf.Max(0f, duration);
        while (elapsed < safe)
        {
            if (skipCheck != null && skipCheck()) yield break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private bool TryReadSkip(ref bool skipEnabled, float introStart)
    {
        if (!skipEnabled && Time.unscaledTime - introStart >= 1.0f)
        {
            skipEnabled = true;
            hud.SetSkipVisible(true);
        }
        return skipEnabled && Input.anyKeyDown && !Input.GetKeyDown(KeyCode.Escape);
    }

    private void SkipIntro()
    {
        if (state != ExecutorState.Intro) return;
        CleanupSpawnedCombatObjects();
        if (bossAudio != null) bossAudio.StopRumble();
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualBaseLocalPosition;
            visualRoot.localScale = visualBaseScale;
            visualRoot.localRotation = Quaternion.identity;
        }
        SetShadowVisible(true);
        SetMachineLights(1f);
        hud.ShowIntroMessage(string.Empty);
        hud.SetSkipVisible(false);
        hud.SetCinematicBarsImmediate(false);
        cutsceneIsolation.Restore();
        hud.ShowBossBar(health.CurrentHealth, health.MaxHealth);
        music.PlayPhaseOne(0.16f);
        cameraDirector.SnapToCombat();
        FinishIntroAndStartCombat();
    }

    private void FinishIntroAndStartCombat()
    {
        if (bossAudio != null) bossAudio.StopRumble();
        if (cutsceneIsolation != null) cutsceneIsolation.Restore();
        if (introInputLock != null)
        {
            introInputLock.Dispose();
            introInputLock = null;
        }
        SetHurtboxEnabled(true);
        SetSolidColliderEnabled(true);
        SetShadowVisible(true);
        SetGroundSocketVisible(true, 1f);
        cameraDirector.RestorePixelPerfect();
        state = ExecutorState.Idle;
        combatStarted = true;
        lastBurrowTime = Time.time;
        nextBurrowAllowedTime = Time.time + 4f;
        combatRoutine = StartCoroutine(CombatLoop());
    }

    private IEnumerator CombatLoop()
    {
        yield return WaitCombatSeconds(0.45f);
        while (!dead)
        {
            if (phaseTransitionRequested)
            {
                yield return PhaseTransitionRoutine();
                continue;
            }

            if (finalStandRequested && !finalStandActive)
            {
                yield return FinalStandRoutine();
                continue;
            }

            if (ShouldBurrow())
            {
                yield return BurrowRoutine();
                continue;
            }

            PatternKind pattern;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugForceSelectedPattern)
            {
                pattern = debugSelectedPattern;
                debugForceSelectedPattern = false;
            }
            else
#endif
            {
                pattern = firstAttackPending ? PatternKind.Aimed : ChoosePattern();
            }
            firstAttackPending = false;
            yield return RunPattern(pattern);
            if (dead) yield break;
            rangedPatternsSinceBurrow++;
            yield return WaitCombatSeconds(finalStandActive ? 0.30f : (phase >= 2 ? 0.26f : 0.14f));
        }
    }

    private IEnumerator RunPattern(PatternKind pattern)
    {
        attackOutputCount = 0;
        switch (pattern)
        {
            case PatternKind.Spread:
                yield return SpreadRoutine();
                break;
            case PatternKind.Artillery:
                yield return ArtilleryRoutine();
                break;
            case PatternKind.CrossBarrage:
                yield return CrossBarrageRoutine();
                break;
            case PatternKind.ChainArtillery:
                yield return ChainArtilleryRoutine();
                break;
            case PatternKind.FinalStandArtillery:
                yield return RandomFinalStandArtilleryRoutine();
                break;
            default:
                yield return AimedBurstRoutine();
                break;
        }

        // 연출은 재생됐는데 실제 공격 오브젝트가 하나도 생성되지 않은 경우를 막는 최종 안전망이다.
        if (!dead && attackOutputCount <= 0 && state != ExecutorState.Burrowing &&
            state != ExecutorState.Underground && state != ExecutorState.Emerging)
        {
            state = ExecutorState.Attacking;
            if (bossAudio != null) bossAudio.PlayAimedProjectile();
            SpawnProjectile(GetPlayerDirectionFromMuzzle(), phase == 1 ? 7f : 8.2f, 1);
            yield return RecoilRoutine(0.12f);
            SetGlowStrength(0.15f);
            yield return WaitCombatSeconds(0.24f);
            state = ExecutorState.Idle;
        }
    }

    private PatternKind ChoosePattern()
    {
        PatternKind selected = PatternKind.Aimed;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float roll = UnityEngine.Random.value;
            if (phase == 1)
            {
                selected = roll < 0.45f ? PatternKind.Aimed
                    : roll < 0.80f ? PatternKind.Spread
                    : PatternKind.Artillery;
            }
            else if (finalStandActive)
            {
                bool barrageReady = Time.time >= nextFinalStandArtilleryAllowedTime &&
                    lastPattern != PatternKind.FinalStandArtillery;

                selected = roll < 0.20f ? PatternKind.Aimed
                    : roll < 0.40f ? PatternKind.Spread
                    : roll < 0.60f ? PatternKind.CrossBarrage
                    : roll < 0.70f ? PatternKind.ChainArtillery
                    : roll < 0.90f ? PatternKind.Artillery
                    : barrageReady ? PatternKind.FinalStandArtillery
                    : PatternKind.Aimed;
            }
            else
            {
                selected = roll < 0.30f ? PatternKind.Aimed
                    : roll < 0.55f ? PatternKind.Spread
                    : roll < 0.80f ? PatternKind.CrossBarrage
                    : PatternKind.ChainArtillery;
            }

            bool blockedRepeat = selected == lastPattern && repeatedPatternCount >= 2;
            bool blockedSpreadRepeat = selected == PatternKind.Spread && selected == lastPattern;
            bool blockedFinalStandRepeat = selected == PatternKind.FinalStandArtillery &&
                (lastPattern == PatternKind.FinalStandArtillery ||
                 Time.time < nextFinalStandArtilleryAllowedTime);
            if (!blockedRepeat && !blockedSpreadRepeat && !blockedFinalStandRepeat)
                break;
        }

        if (selected == lastPattern)
            repeatedPatternCount++;
        else
        {
            lastPattern = selected;
            repeatedPatternCount = 1;
        }
        return selected;
    }

    private IEnumerator AimedBurstRoutine()
    {
        state = ExecutorState.Attacking;
        int shots = phase == 1 ? 2 : 3;
        float charge = phase == 1 ? 0.50f : 0.44f;
        float interval = phase == 1 ? 0.25f : 0.20f;
        float speed = phase == 1 ? 7f : 7.7f;
        float recovery = phase == 1 ? 0.55f : 0.52f;

        yield return ChargeRoutine(charge);
        for (int i = 0; i < shots && !dead; i++)
        {
            Vector2 direction = GetPlayerDirectionFromMuzzle();
            if (bossAudio != null) bossAudio.PlayAimedProjectile();
            SpawnProjectile(direction, speed, 1);
            yield return RecoilRoutine(interval);
        }
        SetGlowStrength(0.15f);
        yield return WaitCombatSeconds(recovery);
        state = ExecutorState.Idle;
    }

    private IEnumerator SpreadRoutine()
    {
        state = ExecutorState.Attacking;
        int count = phase == 1 ? 5 : 7;
        // V12: slightly wider multi-shot gaps. Damage and shot count stay the same;
        // only dodge lanes become a little more readable.
        float totalAngle = phase == 1 ? 54f : 78f;
        float charge = phase == 1 ? 0.60f : 0.56f;
        float speed = phase == 1 ? 6.2f : 6.6f;
        float recovery = phase == 1 ? 0.65f : 0.64f;

        yield return ChargeRoutine(charge);
        Vector2 center = GetPlayerDirectionFromMuzzle();
        float start = -totalAngle * 0.5f;
        float step = count > 1 ? totalAngle / (count - 1) : 0f;
        if (bossAudio != null) bossAudio.PlaySpreadProjectile();
        for (int i = 0; i < count; i++)
            SpawnProjectile(Rotate(center, start + step * i), speed, 1, true);
        yield return RecoilRoutine(0.12f);
        SetGlowStrength(0.15f);
        yield return WaitCombatSeconds(recovery);
        state = ExecutorState.Idle;
    }

    private IEnumerator ArtilleryRoutine()
    {
        state = ExecutorState.Attacking;
        yield return ChargeRoutine(0.60f);
        yield return ExecutorArtilleryShell.LaunchUp(this, GetMuzzleWorldPosition(), phase == 2, bossAudio);
        yield return RecoilRoutine(0.13f);

        Vector3 target = ClampArtilleryTarget(GetPlayerPosition());
        float precisionRadius = GetPrecisionArtilleryRadius();
        ExecutorTelegraph telegraph = ExecutorTelegraph.Create(this, target, precisionRadius, phase >= 2 ? 1.02f : 0.92f,
            new Color(1f, 0.12f, 0.02f, 0.17f), new Color(1f, 0.45f, 0.12f, 0.9f), "EXE_ArtilleryWarning");
        yield return WaitCombatSeconds(phase >= 2 ? 0.72f : 0.62f);
        if (dead) yield break;
        yield return ExecutorArtilleryShell.Fall(this, target, phase == 2, bossAudio, () =>
        {
            if (telegraph != null) Destroy(telegraph.gameObject);
            DetonateArtillery(target, precisionRadius, 1, true);
        });
        SetGlowStrength(0.15f);
        yield return WaitCombatSeconds(phase >= 2 ? 0.82f : 0.70f);
        state = ExecutorState.Idle;
    }

    private IEnumerator CrossBarrageRoutine()
    {
        state = ExecutorState.Attacking;
        yield return ChargeRoutine(0.56f);
        Vector2 center = GetPlayerDirectionFromMuzzle();
        if (bossAudio != null) bossAudio.PlaySpreadProjectile();
        SpawnProjectile(Rotate(center, -19f), 6.6f, 1, true);
        SpawnProjectile(center, 6.6f, 1, true);
        SpawnProjectile(Rotate(center, 19f), 6.6f, 1, true);
        yield return RecoilRoutine(0.35f);

        for (int i = 0; i < 2 && !dead; i++)
        {
            if (bossAudio != null) bossAudio.PlayAimedProjectile();
            SpawnProjectile(GetPlayerDirectionFromMuzzle(), 7.7f, 1);
            yield return RecoilRoutine(0.20f);
        }
        SetGlowStrength(0.15f);
        yield return WaitCombatSeconds(0.78f);
        state = ExecutorState.Idle;
    }

    private IEnumerator ChainArtilleryRoutine()
    {
        state = ExecutorState.Attacking;
        yield return ChargeRoutine(0.52f);
        List<PendingStrike> strikes = new List<PendingStrike>();
        Vector3 previous = Vector3.positiveInfinity;

        // 세 발 모두 실제로 상단 발사구에서 위로 쏘아 올린다.
        for (int i = 0; i < 3 && !dead; i++)
        {
            float prediction = i == 0 ? 0f : i == 1 ? 0.25f : 0.40f;
            Vector3 target = ClampArtilleryTarget(GetPredictedPlayerPosition(prediction));
            if (previous.x < float.PositiveInfinity && Vector2.Distance(previous, target) < 0.7f)
            {
                Vector2 perpendicular = Rotate((target - previous).normalized, 90f);
                if (perpendicular.sqrMagnitude < 0.001f) perpendicular = Vector2.right;
                target = ClampArtilleryTarget(target + (Vector3)(perpendicular * 0.72f));
            }
            previous = target;

            StartCoroutine(ExecutorArtilleryShell.LaunchUp(this, GetMuzzleWorldPosition(), true, bossAudio));
            yield return RecoilRoutine(0.11f);

            PendingStrike strike = new PendingStrike
            {
                Position = target,
                DetonateAt = Time.time + 0.88f,
                Telegraph = ExecutorTelegraph.Create(this, target, 1.0f, 0.88f,
                    new Color(1f, 0.08f, 0.02f, 0.18f), new Color(1f, 0.45f, 0.12f, 0.92f),
                    "EXE_ChainArtilleryWarning_" + i)
            };
            strikes.Add(strike);
            if (i < 2) yield return WaitCombatSeconds(0.24f);
        }

        for (int i = 0; i < strikes.Count && !dead; i++)
        {
            PendingStrike strike = strikes[i];
            float fallLead = 0.27f;
            float wait = strike.DetonateAt - Time.time - fallLead;
            if (wait > 0f) yield return WaitCombatSeconds(wait);
            if (dead) yield break;
            yield return ExecutorArtilleryShell.Fall(this, strike.Position, true, bossAudio, () =>
            {
                if (strike.Telegraph != null) Destroy(strike.Telegraph.gameObject);
                DetonateArtillery(strike.Position, 1.0f, 1, true);
                strike.Detonated = true;
            });
        }

        SetGlowStrength(0.15f);
        yield return WaitCombatSeconds(1.00f);
        state = ExecutorState.Idle;
    }

    private IEnumerator FinalStandRoutine()
    {
        finalStandRequested = false;
        finalStandActive = true;
        state = ExecutorState.FinalStand;
        ApplyDamageStageVisual(3);
        recentDamage.Clear();
        burrowRequested = false;
        rangedPatternsSinceBurrow = 0;
        nextBurrowAllowedTime = Time.time + 6.5f;

        if (hud != null)
        {
            hud.SetFinalStandStyle();
            StartCoroutine(hud.ShowFinalStandTransition(0.82f));
        }
        if (bossAudio != null) bossAudio.PlayPhaseOverload();
        SetGlowStrength(1.25f);
        SetGroundSocketIntensity(1.55f);
        ExecutorCombatEffects.SpawnMetalSparks(this, transform.position + Vector3.up * 1.35f, 12);
        ExecutorCombatEffects.SpawnDustBurst(this, transform.position, 10, 1.45f);
        if (cameraDirector != null) cameraDirector.Punch(Vector2.up, 0.17f, 0.14f);
        if (GameFeelManager.Instance != null)
        {
            GameFeelManager.Instance.DoHitStop(0.028f);
            GameFeelManager.Instance.Shake(0.18f, 0.11f);
        }

        float warningTime = 0f;
        while (warningTime < 0.82f && !dead)
        {
            warningTime += Time.deltaTime;
            float pulse = Mathf.PingPong(warningTime * 6.5f, 1f);
            SetGlowStrength(Mathf.Lerp(0.65f, 1.35f, pulse));
            SetGroundSocketIntensity(Mathf.Lerp(0.85f, 1.65f, pulse));
            yield return null;
        }
        if (dead) yield break;

        yield return FinalStandArtilleryBarrageRoutine();
        nextFinalStandArtilleryAllowedTime = Time.time + FinalStandArtilleryCooldown;
        if (dead) yield break;
        yield return RadialBossBarrageRoutine();
        if (dead) yield break;

        SetGlowStrength(0.48f);
        SetGroundSocketIntensity(1.15f);
        firstAttackPending = false;
        state = ExecutorState.Idle;
        yield return WaitCombatSeconds(0.06f);
    }

    private IEnumerator RandomFinalStandArtilleryRoutine()
    {
        state = ExecutorState.Attacking;
        yield return FinalStandArtilleryBarrageRoutine();
        if (dead) yield break;

        nextFinalStandArtilleryAllowedTime = Time.time + FinalStandArtilleryCooldown;
        SetGlowStrength(0.42f);
        SetGroundSocketIntensity(1.15f);
        state = ExecutorState.Idle;
        yield return WaitCombatSeconds(0.16f);
    }

    private IEnumerator FinalStandArtilleryBarrageRoutine()
    {
        state = ExecutorState.Attacking;

        // 8발의 위치를 처음에 예약하지 않는다.
        // 각 발을 쏘기 직전에 플레이어 위치/이동을 다시 읽어서 경고 지점이 실제 플레이어를 추적한다.
        for (int i = 0; i < 8 && !dead; i++)
        {
            StartCoroutine(ExecutorArtilleryShell.LaunchUp(this, GetMuzzleWorldPosition(), true, bossAudio));
            attackOutputCount++;
            yield return RecoilRoutine(0.095f);

            // 포탄이 위로 발사된 직후의 최신 플레이어 좌표를 읽는다.
            // 일부 탄만 짧게 이동을 예측해 계속 달리는 플레이어도 추적한다.
            float prediction = 0f;
            if (i == 2) prediction = 0.12f;
            else if (i == 4) prediction = 0.16f;
            else if (i == 6) prediction = 0.22f;
            else if (i == 7) prediction = 0.14f;

            Vector3 target = prediction > 0f
                ? ClampArtilleryTarget(GetPredictedPlayerPosition(prediction))
                : ClampArtilleryTarget(GetPlayerPosition());

            const float warningDuration = 1.05f;
            PendingStrike strike = new PendingStrike
            {
                Position = target,
                DetonateAt = Time.time + warningDuration,
                Telegraph = ExecutorTelegraph.Create(this, target, 0.98f, warningDuration,
                    new Color(1f, 0.04f, 0.01f, 0.20f), new Color(1f, 0.54f, 0.12f, 0.96f),
                    "EXE_FinalStandWarning_" + i)
            };
            // 착탄은 독립 처리한다. 여덟 번째 발사 직후에는 착탄을 기다리지 않고 다음 패턴으로 복귀한다.
            StartCoroutine(ResolveFinalStandStrikeRoutine(strike));
            yield return WaitCombatSeconds(0.12f);
        }

        yield return WaitCombatSeconds(0.06f);
    }

    private IEnumerator ResolveFinalStandStrikeRoutine(PendingStrike strike)
    {
        float wait = strike.DetonateAt - Time.time - 0.27f;
        if (wait > 0f) yield return WaitCombatSeconds(wait);
        if (dead) yield break;

        yield return ExecutorArtilleryShell.Fall(this, strike.Position, true, bossAudio, () =>
        {
            if (strike.Telegraph != null) Destroy(strike.Telegraph.gameObject);
            // 최후의 저항 8연발도 모든 착탄에서 전방위 투사체가 나온다.
            DetonateArtillery(strike.Position, 0.98f, 1, true);
            strike.Detonated = true;
        });
    }

    private IEnumerator RadialBossBarrageRoutine()
    {
        state = ExecutorState.Attacking;
        yield return ChargeRoutine(0.24f);
        if (dead) yield break;

        if (bossAudio != null) bossAudio.PlaySpreadProjectile();
        Vector3 origin = transform.position + Vector3.up * 0.78f;
        SpawnRadialProjectiles(origin, 12, 5.6f, 1, 15f);
        if (cameraDirector != null) cameraDirector.Punch(Vector2.down, 0.11f, 0.085f);
        if (GameFeelManager.Instance != null) GameFeelManager.Instance.Shake(0.12f, 0.075f);
        yield return RecoilRoutine(0.16f);
        SetGlowStrength(0.42f);
        yield return WaitCombatSeconds(0.16f);
    }

    private float GetPrecisionArtilleryRadius()
    {
        // 일반 정밀 포격만 페이즈에 따라 대구경화한다.
        // 3연속/8연발 포격의 기존 반경은 그대로 유지해 회피 공간을 보존한다.
        if (finalStandActive)
            return 1.61f; // 1페이즈 대비 약 1.40배
        if (phase >= 2)
            return 1.38f; // 1페이즈 대비 약 1.20배
        return 1.15f;
    }

    private bool ShouldBurrow()
    {
        if (dead || state != ExecutorState.Idle || Time.time < nextBurrowAllowedTime || rangedPatternsSinceBurrow < 1)
            return false;
        float forcedDelay = phase == 1 ? 13f : 10f;
        return burrowRequested || Time.time - lastBurrowTime >= forcedDelay;
    }

    private IEnumerator BurrowRoutine()
    {
        state = ExecutorState.Burrowing;
        burrowRequested = false;
        recentDamage.Clear();
        rangedPatternsSinceBurrow = 0;
        CancelHitReactionForBurrow();
        SetGlowStrength(0.08f);
        SetGroundSocketVisible(true, 1f);
        if (bossAudio != null) bossAudio.PlayBurrow();

        // 1) 고정 소켓 해제: 본체가 눌리고 바닥 림/균열이 먼저 반응한다.
        float anticipation = 0f;
        while (anticipation < 0.22f && !dead)
        {
            anticipation += Time.deltaTime;
            float t = Mathf.Clamp01(anticipation / 0.22f);
            float shake = Mathf.Lerp(0.012f, 0.058f, t);
            SetGroundSocketIntensity(Mathf.Lerp(0.45f, 1.25f, Mathf.PingPong(anticipation * 6f, 1f)));
            if (visualRoot != null)
            {
                visualRoot.localPosition = visualBaseLocalPosition + new Vector3(UnityEngine.Random.Range(-shake, shake), -0.065f * t, 0f);
                visualRoot.localScale = Vector3.Lerp(visualBaseScale, new Vector3(1.07f, 0.89f, 1f), t);
            }
            if (GameFeelManager.Instance != null)
                GameFeelManager.Instance.DirectionalShake(0.05f, 0.018f + 0.030f * t, Vector2.down);
            yield return null;
        }

        ExecutorCombatEffects.SpawnGroundCrack(this, transform.position, 1.20f, 1.25f,
            new Color(0.42f, 0.07f, 0.05f, 0.88f));
        ExecutorCombatEffects.SpawnInwardDust(this, transform.position, 14, 3.2f);
        ExecutorCombatEffects.SpawnRockBurst(this, transform.position, 6, 1.8f, 0.48f, 0.08f, 0.16f);
        cameraDirector.Punch(Vector2.down, 0.12f, 0.085f);
        yield return WaitCombatSeconds(0.065f);

        SetHurtboxEnabled(false);
        SetSolidColliderEnabled(false);
        Vector3 start = visualBaseLocalPosition;
        Vector3 hidden = visualBaseLocalPosition + Vector3.down * 3.10f;
        float elapsed = 0f;
        while (elapsed < 0.31f && !dead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 0.31f);
            float eased = t * t * t;
            if (visualRoot != null)
            {
                visualRoot.localPosition = Vector3.Lerp(start, hidden, eased);
                visualRoot.localScale = Vector3.Lerp(new Vector3(1.07f, 0.89f, 1f), new Vector3(0.93f, 1.08f, 1f), eased);
            }
            if (t > 0.28f && Mathf.Repeat(elapsed, 0.065f) < Time.deltaTime)
                ExecutorCombatEffects.SpawnDustBurst(this, transform.position, 3, 1.45f);
            yield return null;
        }
        if (dead) yield break;
        SetShadowVisible(false);
        SetGroundSocketVisible(false, 0f);
        if (visualRoot != null)
        {
            visualRoot.localPosition = hidden;
            visualRoot.localScale = visualBaseScale;
        }
        ExecutorCombatEffects.SpawnGroundCrack(this, transform.position, 0.92f, 0.62f,
            new Color(0.22f, 0.08f, 0.08f, 0.55f));

        // 2) 지중 이동: 융기, 회백색 먼지, 잔균열이 목표 위치까지 따라간다.
        state = ExecutorState.Underground;
        if (bossAudio != null) bossAudio.StartUndergroundMovement(phase == 1 ? 0.30f : 0.37f);
        Vector3 undergroundStart = transform.position;
        Vector3 target = phase == 1 ? GetPlayerPosition() : GetPredictedPlayerPosition(0.35f);
        target = ClampBossRootPosition(target);
        float undergroundDuration = phase == 1 ? 0.48f : 0.37f;
        float undergroundElapsed = 0f;
        float nextTrail = 0f;
        Vector2 travelDirection = (target - undergroundStart).normalized;
        while (undergroundElapsed < undergroundDuration && !dead)
        {
            undergroundElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(undergroundElapsed / undergroundDuration);
            float eased = t * t * (3f - 2f * t);
            transform.position = Vector3.Lerp(undergroundStart, target, eased);
            if (undergroundElapsed >= nextTrail)
            {
                nextTrail += 0.07f;
                ExecutorCombatEffects.SpawnUndergroundTrail(this, transform.position, travelDirection);
                if (GameFeelManager.Instance != null)
                    GameFeelManager.Instance.DirectionalShake(0.045f, 0.014f, travelDirection);
            }
            yield return null;
        }
        transform.position = new Vector3(target.x, target.y, transform.position.z);
        Physics2D.SyncTransforms();

        if (bossAudio != null) bossAudio.StopUndergroundMovement();

        // 3) 불규칙한 균열과 지면 소켓 자체가 돌출 경고가 된다. 포격용 조준경과 완전히 분리한다.
        float warningDuration = phase == 1 ? 0.90f : 0.65f;
        GameObject emergeWarningMark = ExecutorCombatEffects.SpawnGroundCrack(this, transform.position, 1.22f, warningDuration + 0.72f,
            phase == 1
                ? new Color(0.54f, 0.05f, 0.04f, 0.88f)
                : new Color(0.90f, 0.16f, 0.03f, 0.94f));
        float warningElapsed = 0f;
        bool earlyImpactMarkSpawned = false;
        float nextWarningBurst = 0f;
        float nextWarningRock = 0.12f;
        while (warningElapsed < warningDuration && !dead)
        {
            warningElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(warningElapsed / warningDuration);
            if (!earlyImpactMarkSpawned && warningElapsed >= Mathf.Max(0.12f, warningDuration - 0.50f))
            {
                earlyImpactMarkSpawned = true;
                ExecutorCombatEffects.SpawnEmergenceImpactDecal(this, transform.position, 1.05f, phase == 1 ? 1.00f : 1.10f);
            }
            if (warningElapsed >= nextWarningBurst)
            {
                nextWarningBurst += Mathf.Lerp(0.17f, 0.065f, t);
                ExecutorCombatEffects.SpawnDustBurst(this, transform.position, t > 0.72f ? 6 : 3, Mathf.Lerp(0.90f, 2.25f, t));
            }
            if (warningElapsed >= nextWarningRock)
            {
                nextWarningRock += Mathf.Lerp(0.20f, 0.095f, t);
                ExecutorCombatEffects.SpawnRockBurst(this, transform.position, t > 0.70f ? 3 : 1,
                    1.15f + t * 1.35f, 0.38f, 0.065f, 0.14f);
            }
            if (GameFeelManager.Instance != null && t > 0.34f)
                GameFeelManager.Instance.DirectionalShake(0.045f, Mathf.Lerp(0.014f, 0.055f, t), Vector2.down);
            yield return null;
        }
        if (dead) yield break;

        // 4) 돌출 직전 짧은 정적과 흡입으로 다음 충격을 강조한다.
        ExecutorCombatEffects.SpawnInwardDust(this, transform.position, phase == 1 ? 12 : 16, phase == 1 ? 3.3f : 3.8f);
        yield return WaitCombatSeconds(0.065f);
        if (dead) yield break;

        // 5) 폭발적 돌출: 경고 표시는 돌출과 동시에 사라지고 사용자 제공 마커가 잠깐 남는다.
        if (emergeWarningMark != null) Destroy(emergeWarningMark);
        RemoveGroundWarningMarks();
        state = ExecutorState.Emerging;
        if (bossAudio != null) bossAudio.PlayRiseImpact(phase == 1 ? 0.88f : 0.96f);
        ExecutorCombatEffects.SpawnDustBurst(this, transform.position, phase == 1 ? 32 : 40, phase == 1 ? 4.2f : 4.8f);
        ExecutorCombatEffects.SpawnRockBurst(this, transform.position, phase == 1 ? 20 : 26,
            phase == 1 ? 5.0f : 5.7f, 0.88f, 0.10f, phase == 1 ? 0.30f : 0.34f);
        ExecutorCombatEffects.SpawnMetalSparks(this, transform.position + Vector3.up * 0.15f, phase == 1 ? 5 : 9);
        cameraDirector.Punch(Vector2.up, 0.21f, phase == 1 ? 0.20f : 0.24f);
        if (GameFeelManager.Instance != null)
        {
            GameFeelManager.Instance.DoHitStop(phase == 1 ? 0.038f : 0.045f);
            GameFeelManager.Instance.Shake(0.24f, phase == 1 ? 0.16f : 0.20f);
        }

        Vector3 overshoot = visualBaseLocalPosition + Vector3.up * 0.17f;
        elapsed = 0f;
        while (elapsed < 0.145f && !dead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 0.145f);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            if (visualRoot != null)
            {
                visualRoot.localPosition = Vector3.Lerp(hidden, overshoot, eased);
                visualRoot.localScale = Vector3.Lerp(new Vector3(0.92f, 1.10f, 1f), new Vector3(0.96f, 1.08f, 1f), eased);
            }
            if (t > 0.30f) SetShadowVisible(true);
            yield return null;
        }
        if (dead) yield break;

        DamagePlayerInRadius(transform.position, 1.10f, 2);
        SetHurtboxEnabled(true);
        SetSolidColliderEnabled(true);

        float settle = 0f;
        while (settle < 0.15f && !dead)
        {
            settle += Time.deltaTime;
            float t = Mathf.Clamp01(settle / 0.15f);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            if (visualRoot != null)
            {
                visualRoot.localPosition = Vector3.Lerp(overshoot, visualBaseLocalPosition, eased);
                visualRoot.localScale = Vector3.Lerp(new Vector3(0.96f, 1.08f, 1f), new Vector3(1.05f, 0.95f, 1f), eased);
            }
            yield return null;
        }
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualBaseLocalPosition;
            visualRoot.localScale = visualBaseScale;
        }
        ExecutorCombatEffects.SpawnDustBurst(this, transform.position, 12, 1.55f);
        cameraDirector.Punch(Vector2.down, 0.11f, 0.085f);

        if (phase == 2)
        {
            yield return WaitCombatSeconds(0.14f);
            ExecutorShockwaveEffect.Create(this, transform.position, 0.7f, 4.2f, 0.45f, 0.55f, 2);
            yield return WaitCombatSeconds(0.55f);
            yield return WaitCombatSeconds(0.75f);
        }
        else
        {
            yield return WaitCombatSeconds(1.0f);
        }

        lastBurrowTime = Time.time;
        nextBurrowAllowedTime = Time.time + (phase == 1 ? 8f : 6.5f);
        state = ExecutorState.Idle;
    }

    private IEnumerator PhaseTransitionRoutine()
    {
        phaseTransitionRequested = false;
        phase = 2;
        state = ExecutorState.PhaseTransition;
        recentDamage.Clear();
        burrowRequested = false;
        lastBurrowTime = Time.time;
        nextBurrowAllowedTime = Time.time + 4.5f;
        phaseInputLock = GameInputState.Acquire("ExecutorPhaseTransition");

        CleanupSpawnedCombatObjects();
        RemoveGroundWarningMarks();
        SetHurtboxEnabled(true);
        SetSolidColliderEnabled(true);

        // V6: 2페이즈 컷신은 카메라 중심/줌을 직접 움직이지 않는다.
        // 기존 전투 카메라 위치를 그대로 유지해 RoomBase 바깥 공허가 비치는 가능성을 원천 차단한다.
        StartCoroutine(hud.AnimateCinematicBars(true, 0.20f));
        StartCoroutine(hud.ShowPhaseTransition(1.55f));
        if (bossAudio != null) bossAudio.PlayPhaseOverload();

        float elapsed = 0f;
        float nextSpark = 0f;
        float nextSmoke = 0f;
        bool spriteSwapped = false;
        while (elapsed < 1.05f && !dead)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 1.05f);
            if (visualRoot != null)
            {
                float magnitude = Mathf.Lerp(0.012f, 0.070f, Mathf.Sin(t * Mathf.PI));
                visualRoot.localPosition = visualBaseLocalPosition +
                    (Vector3)(UnityEngine.Random.insideUnitCircle * magnitude);
            }

            if (!spriteSwapped && t >= 0.44f)
            {
                spriteSwapped = true;
                ApplyDamageStageVisual(2);
                ExecutorCombatEffects.SpawnMetalSparks(this, transform.position + Vector3.up * 1.30f, 12);
                ExecutorCombatEffects.SpawnDustBurst(this, transform.position, 10, 1.40f);
                cameraDirector.Punch(Vector2.up, 0.12f, 0.12f);
                if (GameFeelManager.Instance != null)
                {
                    GameFeelManager.Instance.DoHitStop(0.032f);
                    GameFeelManager.Instance.Shake(0.16f, 0.10f);
                }
            }

            SetGlowStrength(Mathf.Lerp(0.28f, 1.18f, Mathf.PingPong(elapsed * 5.6f, 1f)));
            if (elapsed >= nextSpark)
            {
                nextSpark += 0.12f;
                ExecutorCombatEffects.SpawnMetalSparks(this,
                    transform.position + new Vector3(Random.Range(-0.42f, 0.42f), Random.Range(0.72f, 2.02f), 0f), 2);
            }
            if (elapsed >= nextSmoke)
            {
                nextSmoke += 0.20f;
                ExecutorCombatEffects.SpawnDustCloud(this,
                    transform.position + new Vector3(Random.Range(-0.30f, 0.30f), Random.Range(1.15f, 2.0f), 0f),
                    Random.Range(0.22f, 0.36f), 0.46f, new Color(0.42f, 0.43f, 0.45f, 0.44f));
            }
            yield return null;
        }

        if (visualRoot != null) visualRoot.localPosition = visualBaseLocalPosition;
        if (!spriteSwapped) ApplyDamageStageVisual(2);
        ApplyPhaseTwoVisuals();
        hud.SetPhaseTwoStyle();
        music.PlayPhaseTwo(0.48f);
        cameraDirector.Punch(Vector2.up, 0.16f, 0.14f);
        ExecutorCombatEffects.SpawnMetalSparks(this, transform.position + Vector3.up * 1.35f, 8);

        yield return new WaitForSecondsRealtime(0.30f);
        yield return hud.AnimateCinematicBars(false, 0.24f);

        if (phaseInputLock != null)
        {
            phaseInputLock.Dispose();
            phaseInputLock = null;
        }

        SetGlowStrength(0.42f);
        firstAttackPending = true;
        if (health != null && health.NormalizedHealth <= FinalStandHealthRatio && !finalStandActive)
            finalStandRequested = true;
        state = ExecutorState.Idle;
    }

    private void Update()
    {
        if (dead)
            return;

        if (combatStarted && playerHealth != null && !playerHealth.IsDead)
        {
            UpdateEyeTracking();
            if (phaseTwoVisualsActive)
            {
                phaseTwoSmokeTimer -= Time.deltaTime;
                if (phaseTwoSmokeTimer <= 0f)
                {
                    phaseTwoSmokeTimer = Random.Range(0.42f, 0.68f);
                    ExecutorCombatEffects.SpawnDustCloud(this, transform.position + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(1.25f, 2.05f), 0f),
                        Random.Range(0.18f, 0.30f), 0.55f, new Color(0.38f, 0.39f, 0.42f, 0.34f));
                }
            }
        }

        if (playerDefeatHandled || !combatStarted || playerHealth == null || !playerHealth.IsDead)
            return;

        playerDefeatHandled = true;
        StopAllCoroutines();
        StartCoroutine(PlayerDefeatedRoutine());
    }

    private IEnumerator PlayerDefeatedRoutine()
    {
        if (cutsceneIsolation != null) cutsceneIsolation.Restore();
        if (bossAudio != null) bossAudio.StopAllLoops();
        state = ExecutorState.Dormant;
        combatStarted = false;
        SetHurtboxEnabled(false);
        SetSolidColliderEnabled(false);
        CleanupSpawnedCombatObjects();
        if (phaseInputLock != null)
        {
            phaseInputLock.Dispose();
            phaseInputLock = null;
        }
        if (music != null) music.SetDefeatMix(0.88f, 0.35f);
        if (cameraDirector != null) cameraDirector.SnapToCombat();
        if (hud != null)
        {
            hud.ShowIntroMessage(string.Empty);
            hud.SetSkipVisible(false);
            yield return hud.FadeOut(0.24f);
            hud.HideAllImmediate();
        }
    }

    private void HandleDamaged(EnemyHealth source, int appliedDamage, Vector2 direction)
    {
        if (dead || health == null)
            return;

        hud.ReportHealth(health.CurrentHealth, health.MaxHealth, true);
        recentDamage.Add(new DamageStamp { Time = Time.time, Damage = appliedDamage });
        PruneRecentDamage();

        if (phase == 1 && health.NormalizedHealth <= 0.55f)
            phaseTransitionRequested = true;

        if (!finalStandActive && !finalStandRequested && health.NormalizedHealth <= FinalStandHealthRatio)
            finalStandRequested = true;

        int total = 0;
        for (int i = 0; i < recentDamage.Count; i++) total += recentDamage[i].Damage;
        float threshold = health.MaxHealth * (phase == 1 ? 0.09f : 0.07f);
        if (total >= threshold && Time.time >= nextBurrowAllowedTime)
            burrowRequested = true;

        DamageContext context = health.LastDamageContext;
        bool suppressVisualFeedback = state == ExecutorState.Dormant || state == ExecutorState.Intro ||
            state == ExecutorState.Burrowing || state == ExecutorState.Underground ||
            state == ExecutorState.Emerging;

        if (!suppressVisualFeedback)
        {
            Vector2 hitPoint = context.HitPoint != Vector2.zero
                ? context.HitPoint
                : (Vector2)transform.position + Vector2.up;
            if (bodyRenderer != null && Time.unscaledTime >= nextVisualHitReactionTime)
            {
                // 샷건 펠릿이나 기관총 연사가 같은 프레임에 연속 적중해도
                // 현재 찌그러진 스케일을 다음 반응의 기준으로 삼지 않는다.
                bool heavyVisual = context.HitKind == EnemyHitKind.Melee || appliedDamage >= 25;
                nextVisualHitReactionTime = Time.unscaledTime + (heavyVisual ? 0.055f : 0.040f);

                if (hitReactionRoutine != null)
                {
                    StopCoroutine(hitReactionRoutine);
                    hitReactionRoutine = null;
                    if (visualRoot != null) visualRoot.localPosition = hitReactionAnchorPosition;
                }

                bodyRenderer.color = bodyBaseColor;
                bodyRenderer.transform.localScale = bodyBaseScale;
                hitReactionAnchorPosition = visualRoot != null
                    ? visualRoot.localPosition
                    : visualBaseLocalPosition;
                hitReactionRoutine = StartCoroutine(
                    HitReactionRoutine(direction, context.HitKind, appliedDamage, hitReactionAnchorPosition));
            }
            ExecutorCombatEffects.SpawnMetalSparks(this, hitPoint,
                appliedDamage >= 40 ? 7 : context.HitKind == EnemyHitKind.Melee ? 5 : 3);

            if (Time.unscaledTime >= nextHeavyHitFeedbackTime)
            {
                bool heavy = context.HitKind == EnemyHitKind.Melee || appliedDamage >= 25;
                nextHeavyHitFeedbackTime = Time.unscaledTime + (heavy ? 0.045f : 0.075f);
                CameraFeedbackController feedback = CameraFeedbackController.Instance;
                if (feedback != null)
                {
                    feedback.Shake(heavy ? 0.075f : 0.040f, heavy ? 0.070f : 0.025f, direction);
                    if (heavy) feedback.HitStop(appliedDamage >= 40 ? 0.026f : 0.016f);
                }
            }
        }
    }

    private void PruneRecentDamage()
    {
        float minimum = Time.time - 3f;
        for (int i = recentDamage.Count - 1; i >= 0; i--)
        {
            if (recentDamage[i].Time < minimum)
                recentDamage.RemoveAt(i);
        }
    }

    private void CancelHitReactionForBurrow()
    {
        if (hitReactionRoutine != null)
        {
            StopCoroutine(hitReactionRoutine);
            hitReactionRoutine = null;
        }
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualBaseLocalPosition;
            visualRoot.localScale = visualBaseScale;
        }
        if (bodyRenderer != null)
        {
            bodyRenderer.color = bodyBaseColor;
            bodyRenderer.transform.localScale = bodyBaseScale;
        }
    }

    private IEnumerator HitReactionRoutine(
        Vector2 direction, EnemyHitKind hitKind, int damage, Vector3 anchorPosition)
    {
        if (bodyRenderer == null)
        {
            hitReactionRoutine = null;
            yield break;
        }

        bool heavy = hitKind == EnemyHitKind.Melee || damage >= 25;
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.up;
        Vector3 kick = -(Vector3)safeDirection * (heavy ? 0.052f : 0.020f);

        // 항상 최초 원본 스케일을 기준으로 계산한다.
        // 연속 적중 때 0.98 * 0.98 * 0.98...로 누적되어 짜부되는 현상을 차단한다.
        Vector3 compressedScale = new Vector3(
            bodyBaseScale.x * (heavy ? 1.025f : 1.010f),
            bodyBaseScale.y * (heavy ? 0.970f : 0.988f),
            bodyBaseScale.z);

        hitReactionAnchorPosition = anchorPosition;
        bodyRenderer.color = Color.white;
        bodyRenderer.transform.localScale = compressedScale;
        if (visualRoot != null)
            visualRoot.localPosition = anchorPosition + kick;

        yield return new WaitForSecondsRealtime(0.020f);

        if (!dead && bodyRenderer != null)
            bodyRenderer.color = new Color(1f, 0.34f, 0.27f, bodyBaseColor.a);

        yield return new WaitForSecondsRealtime(heavy ? 0.030f : 0.018f);

        float elapsed = 0f;
        float duration = heavy ? 0.060f : 0.038f;
        Color flashColor = new Color(1f, 0.34f, 0.27f, bodyBaseColor.a);

        while (elapsed < duration && !dead)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);

            if (visualRoot != null)
                visualRoot.localPosition = Vector3.Lerp(anchorPosition + kick, anchorPosition, eased);

            if (bodyRenderer != null)
            {
                bodyRenderer.transform.localScale = Vector3.Lerp(compressedScale, bodyBaseScale, eased);
                bodyRenderer.color = Color.Lerp(flashColor, bodyBaseColor, eased);
            }
            yield return null;
        }

        if (!dead)
        {
            if (visualRoot != null)
                visualRoot.localPosition = anchorPosition;

            if (bodyRenderer != null)
            {
                bodyRenderer.transform.localScale = bodyBaseScale;
                bodyRenderer.color = bodyBaseColor;
            }
        }

        hitReactionRoutine = null;
    }

    public bool HandleRequestedDeath(EnemyHealth requestedHealth, Vector2 hitDirection, EnemyHitKind hitKind)
    {
        if (dead || requestedHealth != health)
            return dead;

        dead = true;
        state = ExecutorState.Dead;
        StopAllCoroutines();
        StartCoroutine(DeathRoutine(hitDirection));
        return true;
    }

    private IEnumerator DeathRoutine(Vector2 hitDirection)
    {
        deathInputLock = GameInputState.Acquire("ExecutorDeath");
        if (bossAudio != null)
        {
            bossAudio.StopAllLoops();
            bossAudio.PlayDeathMalfunction();
        }
        SetHurtboxEnabled(false);
        SetSolidColliderEnabled(false);
        cameraDirector.SnapToCombat();
        CleanupSpawnedCombatObjects();
        if (music != null) music.CrossFadeBackToGameplay(1.75f);
        hud.ReportHealth(0, health != null ? health.MaxHealth : BossMaxHealth, true);
        hud.SetSkipVisible(false);
        hud.ShowIntroMessage(string.Empty);

        float elapsed = 0f;
        float nextDeathSpark = 0f;
        while (elapsed < 0.42f)
        {
            elapsed += Time.unscaledDeltaTime;
            if (visualRoot != null)
                visualRoot.localPosition = visualBaseLocalPosition + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.045f);
            SetMachineLights(Mathf.PingPong(elapsed * 13f, 1f));
            if (elapsed >= nextDeathSpark)
            {
                nextDeathSpark += 0.075f;
                ExecutorCombatEffects.SpawnMetalSparks(this, transform.position + new Vector3(Random.Range(-0.42f, 0.42f), Random.Range(0.55f, 2.05f), 0f), 3);
            }
            yield return null;
        }

        if (visualRoot != null) visualRoot.localPosition = visualBaseLocalPosition;
        Sprite deathSprite = ExecutorRuntimeSprites.LoadDeath();
        if (deathSprite != null && bodyRenderer != null)
            bodyRenderer.sprite = deathSprite;
        else
        {
            if (bodyRenderer != null) bodyRenderer.color = new Color(0.43f, 0.43f, 0.46f, 1f);
            if (leftEye != null)
            {
                leftEye.color = new Color(0.08f, 0.10f, 0.12f, 0.35f);
                leftEye.transform.localPosition += new Vector3(-0.03f, -0.05f, 0f);
            }
            if (rightEye != null)
            {
                rightEye.color = new Color(0.42f, 0.78f, 1f, 0.65f);
                rightEye.transform.localPosition += new Vector3(0.08f, -0.10f, 0f);
                rightEye.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
            }
        }
        if (muzzleGlow != null) muzzleGlow.enabled = false;
        if (bossAudio != null) bossAudio.PlayFinalShutdown();
        ExecutorCombatEffects.SpawnDustBurst(null, transform.position + Vector3.up * 0.4f, 10, 1.4f);
        if (GameFeelManager.Instance != null) GameFeelManager.Instance.Shake(0.22f, 0.08f);
        CameraFeedbackController deathFeedback = CameraFeedbackController.Instance;
        if (deathFeedback != null) deathFeedback.Impact(CameraImpactLevelV11.Boss, hitDirection, true);

        float settle = 0f;
        Quaternion startRotation = visualRoot != null ? visualRoot.localRotation : Quaternion.identity;
        Vector3 startPosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        while (settle < 0.85f)
        {
            settle += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(settle / 0.85f);
            if (visualRoot != null)
            {
                visualRoot.localRotation = Quaternion.Lerp(startRotation, Quaternion.Euler(0f, 0f, -8f), t);
                visualRoot.localPosition = Vector3.Lerp(startPosition, startPosition + Vector3.down * 0.18f, t);
            }
            yield return null;
        }

        // 보스 격파 보상: 다음 구간 진입 전에 현재 최대 체력까지 완전히 회복한다.
        if (playerHealth != null && !playerHealth.IsDead)
            playerHealth.RestoreToFull();

        SpawnRewards();
        // 일반 몹과 같은 BytePickup / AmmoPickup이 흩어질 시간을 보장한 뒤 출구 포탈을 연다.
        yield return new WaitForSecondsRealtime(1.20f);
        if (mapManager != null)
            mapManager.NotifyExecutorDefeated(ownerRoom);
        if (hud != null)
            yield return hud.FadeOut(0.45f);

        if (deathInputLock != null)
        {
            deathInputLock.Dispose();
            deathInputLock = null;
        }
        if (hud != null) Destroy(hud.gameObject);
        if (health != null)
            health.CompleteDeferredDeath(hitDirection);
    }

    private void SpawnRewards()
    {
        Vector3 origin = transform.position + Vector3.up * 0.8f;

        // 집행자 전용 RewardOrb를 사용하지 않고 일반 적과 동일한 BytePickup 시스템을 사용한다.
        // 총 보상량 24 Byte는 유지하되 일반 드롭과 같은 외형/충돌/흡수 규칙을 따른다.
        ByteDropper byteDropper = GetComponent<ByteDropper>();
        if (byteDropper == null)
            byteDropper = gameObject.AddComponent<ByteDropper>();
        byteDropper.ConfigureDrops(6, 6, 4);
        byteDropper.DropBytes(origin);

        // 탄약도 일반 적이 쓰는 AmmoPickup과 완전히 동일한 생성 경로를 사용한다.
        // 8 에너지 x 3개 = 기존과 같은 총 24 에너지.
        for (int i = 0; i < 3; i++)
        {
            Vector3 spawn = origin + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.18f);
            AmmoDropper.SpawnPickupAt(spawn, 8, ownerRoom);
        }
    }

    private IEnumerator ChargeRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && !dead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetGlowStrength(Mathf.Lerp(0.2f, 1f, t));
            yield return null;
        }
    }

    private IEnumerator RecoilRoutine(float duration)
    {
        if (visualRoot == null)
        {
            yield return WaitCombatSeconds(duration);
            yield break;
        }

        Vector3 recoil = visualBaseLocalPosition + Vector3.down * 0.07f;
        visualRoot.localPosition = recoil;
        float elapsed = 0f;
        float safe = Mathf.Max(0.03f, duration);
        while (elapsed < safe && !dead)
        {
            elapsed += Time.deltaTime;
            visualRoot.localPosition = Vector3.Lerp(recoil, visualBaseLocalPosition, Mathf.Clamp01(elapsed / safe));
            yield return null;
        }
        visualRoot.localPosition = visualBaseLocalPosition;
    }

    private IEnumerator FlickerMachineLights(float duration, Func<bool> skipCheck)
    {
        float elapsed = 0f;
        while (elapsed < duration && !dead)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float flicker = t < 0.4f ? (Mathf.PingPong(elapsed * 9f, 1f) > 0.5f ? 0.15f : 0f) : 1f;
            SetMachineLights(flicker);
            if (skipCheck != null && skipCheck())
            {
                yield break;
            }
            yield return null;
        }
        SetMachineLights(1f);
    }

    private bool SpawnProjectile(Vector2 direction, float speed, int damage, bool spread = false)
    {
        return SpawnProjectileFrom(GetMuzzleWorldPosition(), direction, speed, damage, spread, 0.52f);
    }

    private bool SpawnProjectileFrom(Vector3 origin, Vector2 direction, float speed, int damage, bool spread, float safeOffset)
    {
        Vector2 normalized = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
        ExecutorProjectileStyle style;
        if (phase == 2)
            style = spread ? ExecutorProjectileStyle.SpreadPhaseTwo : ExecutorProjectileStyle.AimedPhaseTwo;
        else
            style = spread ? ExecutorProjectileStyle.SpreadPhaseOne : ExecutorProjectileStyle.AimedPhaseOne;

        Vector3 safePosition = origin + (Vector3)(normalized * Mathf.Max(0.18f, safeOffset));
        ExecutorProjectile projectile = ExecutorProjectile.Create(this, safePosition, normalized, speed, damage, style);
        if (projectile == null)
        {
            safePosition += (Vector3)(normalized * 0.28f);
            projectile = ExecutorProjectile.Create(this, safePosition, normalized, speed, damage, style);
        }
        if (projectile == null)
            return false;

        attackOutputCount++;
        return true;
    }

    private void SpawnRadialProjectiles(Vector3 origin, int count, float speed, int damage, float angleOffset)
    {
        int safeCount = Mathf.Max(1, count);
        float step = 360f / safeCount;
        for (int i = 0; i < safeCount; i++)
        {
            float angle = angleOffset + step * i;
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            SpawnProjectileFrom(origin, direction, speed, damage, true, 0.38f);
        }
    }

    private void DetonateArtillery(Vector3 position, float radius, int damage, bool radialBurst = false)
    {
        attackOutputCount++;
        float effectScale = Mathf.Clamp(radius / 1.15f, 0.82f, 1.40f);
        bool stronger = phase >= 2 || finalStandActive;

        ExecutorExplosionAnimator.Create(this, position, radius, stronger);
        ExecutorCombatEffects.SpawnExplosion(this, position, radius);

        int dustCount = Mathf.RoundToInt((stronger ? 16f : 12f) * effectScale);
        int rockCount = Mathf.RoundToInt((stronger ? 12f : 8f) * effectScale);
        float dustStrength = (stronger ? 2.7f : 2.25f) * Mathf.Lerp(0.92f, 1.18f, Mathf.InverseLerp(0.98f, 1.61f, radius));
        float rockStrength = (stronger ? 3.4f : 2.8f) * Mathf.Lerp(0.92f, 1.16f, Mathf.InverseLerp(0.98f, 1.61f, radius));

        ExecutorCombatEffects.SpawnDustBurst(this, position, Mathf.Max(6, dustCount), dustStrength);
        ExecutorCombatEffects.SpawnRockBurst(this, position, Mathf.Max(5, rockCount),
            rockStrength, 0.62f, 0.08f, (stronger ? 0.24f : 0.19f) * Mathf.Lerp(0.92f, 1.12f, effectScale - 0.82f));
        if (bossAudio != null) bossAudio.PlayExplosion(Mathf.Clamp((stronger ? 0.90f : 0.82f) * Mathf.Lerp(0.96f, 1.10f, effectScale - 0.82f), 0.72f, 1f));
        DamagePlayerInRadius(position, radius, damage);
        if (GameFeelManager.Instance != null)
            GameFeelManager.Instance.Shake(0.15f, Mathf.Clamp((stronger ? 0.10f : 0.08f) * effectScale, 0.065f, 0.14f));
        if (radialBurst && !dead) StartCoroutine(DelayedImpactRadialRoutine(position));
    }

    private IEnumerator DelayedImpactRadialRoutine(Vector3 position)
    {
        yield return WaitCombatSeconds(0.14f);
        if (dead || phase < 2) yield break;

        int count = finalStandActive ? 8 : 4;
        float angleOffset;
        if (finalStandActive)
            angleOffset = (artilleryBurstSequence++ & 1) == 0 ? 0f : 22.5f;
        else
            angleOffset = (artilleryBurstSequence++ & 1) == 0 ? 0f : 45f;

        if (bossAudio != null) bossAudio.PlaySpreadProjectile();
        SpawnRadialProjectiles(position, count, finalStandActive ? 5.2f : 4.9f, 1, angleOffset);
        ExecutorCombatEffects.SpawnMetalSparks(this, position, finalStandActive ? 7 : 5);
    }

    private void DamagePlayerInRadius(Vector3 center, float radius, int damage)
    {
        if (playerHealth == null || playerHealth.IsDead) return;
        Collider2D[] playerColliders = playerHealth.GetComponentsInChildren<Collider2D>(true);
        float closest = Vector2.Distance(center, playerHealth.transform.position);
        for (int i = 0; i < playerColliders.Length; i++)
        {
            if (playerColliders[i] == null || !playerColliders[i].enabled) continue;
            float distance = Vector2.Distance(center, playerColliders[i].ClosestPoint(center));
            if (distance < closest) closest = distance;
        }
        if (closest <= radius)
            playerHealth.TakeDamage(damage);
    }

    private Vector2 GetPlayerDirectionFromMuzzle()
    {
        Vector2 direction = GetPlayerPosition() - GetMuzzleWorldPosition();
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
    }

    private Vector3 GetPlayerPosition()
    {
        if (player == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>();
            player = playerHealth != null ? playerHealth.transform : null;
        }
        return player != null ? player.position : transform.position + Vector3.down * 2f;
    }

    private Vector3 GetPredictedPlayerPosition(float predictionTime)
    {
        Vector3 current = GetPlayerPosition();
        if (player == null) return current;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        Vector2 velocity = body != null ? body.linearVelocity : Vector2.zero;
        Vector2 offset = velocity * Mathf.Max(0f, predictionTime);
        if (offset.magnitude > 1.5f) offset = offset.normalized * 1.5f;
        return current + (Vector3)offset;
    }

    private Vector3 ClampBossRootPosition(Vector3 position)
    {
        if (arenaBounds == null) return position;
        Bounds bounds = arenaBounds.bounds;
        float minX = bounds.min.x + 0.95f;
        float maxX = bounds.max.x - 0.95f;
        // 탑다운 전투이므로 세로로 긴 스프라이트의 시각 높이가 아니라 지면 점유 반경으로 보정한다.
        float minY = bounds.min.y + 0.75f;
        float maxY = bounds.max.y - 0.75f;
        if (maxY < minY) maxY = minY;
        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);
        position.z = transform.position.z;
        return position;
    }

    private Vector3 ClampArtilleryTarget(Vector3 position)
    {
        if (arenaBounds == null) return position;
        Bounds bounds = arenaBounds.bounds;
        float margin = 1.15f;
        position.x = Mathf.Clamp(position.x, bounds.min.x + margin, bounds.max.x - margin);
        position.y = Mathf.Clamp(position.y, bounds.min.y + margin, bounds.max.y - margin);
        position.z = 0f;
        return position;
    }

    private void UpdateEyeTracking()
    {
        if (player == null || leftEye == null || rightEye == null)
            return;
        Vector2 direction = ((Vector2)GetPlayerPosition() - (Vector2)(transform.position + Vector3.up * 1.55f)).normalized;
        Vector3 offset = new Vector3(direction.x * 0.045f, direction.y * 0.022f, 0f);
        leftEye.transform.localPosition = Vector3.Lerp(leftEye.transform.localPosition, leftEyeBaseLocalPosition + offset, Time.deltaTime * 9f);
        rightEye.transform.localPosition = Vector3.Lerp(rightEye.transform.localPosition, rightEyeBaseLocalPosition + offset, Time.deltaTime * 9f);
    }

    private void ApplyPhaseTwoVisuals()
    {
        phaseTwoVisualsActive = true;
        phaseTwoSmokeTimer = 0.12f;
        bodyBaseColor = Color.white;
        if (bodyRenderer != null)
            bodyRenderer.color = bodyBaseColor;
        SetMachineLights(1f);
    }

    private void ApplyDamageStageVisual(int stage)
    {
        if (bodyRenderer == null)
            return;

        Sprite target = stage >= 3
            ? ExecutorRuntimeSprites.LoadBodyFinal()
            : stage == 2
                ? ExecutorRuntimeSprites.LoadBodyPhase2()
                : ExecutorRuntimeSprites.LoadBody();

        if (target != null)
            bodyRenderer.sprite = target;

        bodyBaseColor = Color.white;
        bodyRenderer.color = bodyBaseColor;
        bodyRenderer.transform.localScale = bodyBaseScale;
    }

    private void RemoveGroundWarningMarks()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            GameObject target = spawnedObjects[i];
            if (target == null)
            {
                spawnedObjects.RemoveAt(i);
                continue;
            }

            if (target.name.StartsWith("EXE_GroundCrack", StringComparison.Ordinal))
            {
                SpriteRenderer markRenderer = target.GetComponent<SpriteRenderer>();
                if (markRenderer != null) markRenderer.enabled = false;
                Destroy(target);
                spawnedObjects.RemoveAt(i);
            }
        }
    }

    private void SetGroundSocketVisible(bool visible, float intensity)
    {
        if (groundSocketBack != null) groundSocketBack.enabled = visible;
        if (groundSocketFront != null) groundSocketFront.enabled = visible;
        if (groundSocketCracks != null) groundSocketCracks.enabled = visible;
        if (visible) SetGroundSocketIntensity(intensity);
    }

    private void SetGroundSocketIntensity(float intensity)
    {
        intensity = Mathf.Max(0f, intensity);
        Color crackColor = phaseTwoVisualsActive
            ? new Color(1f, 0.20f, 0.035f, Mathf.Clamp01(0.45f + intensity * 0.42f))
            : new Color(0.50f, 0.07f, 0.055f, Mathf.Clamp01(0.34f + intensity * 0.42f));
        if (groundSocketCracks != null)
        {
            groundSocketCracks.color = crackColor;
            groundSocketCracks.transform.localScale = Vector3.one * Mathf.Lerp(1.24f, 1.38f, Mathf.Clamp01(intensity));
        }
        if (groundSocketBack != null)
        {
            Color back = phaseTwoVisualsActive
                ? new Color(0.56f, 0.17f, 0.10f, Mathf.Clamp01(0.72f + intensity * 0.20f))
                : new Color(0.72f, 0.72f, 0.74f, Mathf.Clamp01(0.76f + intensity * 0.18f));
            groundSocketBack.color = back;
            groundSocketBack.transform.localScale = new Vector3(
                Mathf.Lerp(1.07f, 1.15f, Mathf.Clamp01(intensity)),
                Mathf.Lerp(0.96f, 1.05f, Mathf.Clamp01(intensity)), 1f);
        }
        if (groundSocketFront != null)
        {
            Color front = phaseTwoVisualsActive
                ? new Color(0.84f, 0.32f, 0.18f, 1f)
                : new Color(0.72f, 0.72f, 0.74f, 1f);
            groundSocketFront.color = front;
        }
    }

    private void SetVisualUnderground(bool underground)
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = underground ? visualBaseLocalPosition + Vector3.down * 2.95f : visualBaseLocalPosition;
            visualRoot.localScale = visualBaseScale;
        }
        SetShadowVisible(!underground);
        if (underground) SetGroundSocketVisible(false, 0f);
    }

    private void SetHurtboxEnabled(bool enabled)
    {
        if (hurtbox != null) hurtbox.enabled = enabled;
    }

    private void SetSolidColliderEnabled(bool enabled)
    {
        if (solidCollider != null) solidCollider.enabled = enabled;
    }

    private void SetShadowVisible(bool visible)
    {
        if (shadowRenderer == null) return;
        shadowRenderer.enabled = visible;
        Color color = shadowRenderer.color;
        color.a = visible ? 0.68f : 0f;
        shadowRenderer.color = color;
    }

    private void SetMachineLights(float strength)
    {
        strength = Mathf.Clamp01(strength);
        Color eyeColor = phaseTwoVisualsActive
            ? new Color(1f, 0.30f, 0.08f, strength)
            : new Color(0.28f, 0.82f, 1f, strength);
        if (leftEye != null)
        {
            leftEye.enabled = strength > 0.01f;
            leftEye.color = eyeColor;
        }
        if (rightEye != null)
        {
            rightEye.enabled = strength > 0.01f;
            rightEye.color = eyeColor;
        }
        SetGlowStrength(strength * 0.55f);
    }

    private void SetGlowStrength(float strength)
    {
        if (muzzleGlow == null) return;
        strength = Mathf.Max(0f, strength);
        muzzleGlow.enabled = strength > 0.01f;
        Color glowColor = phaseTwoVisualsActive
            ? new Color(1f, 0.28f, 0.04f, Mathf.Clamp01(strength * 0.78f))
            : new Color(0.45f, 0.9f, 1f, Mathf.Clamp01(strength * 0.72f));
        muzzleGlow.color = glowColor;
        muzzleGlow.transform.localScale = Vector3.one * Mathf.Lerp(0.18f, phaseTwoVisualsActive ? 0.54f : 0.48f, Mathf.Clamp01(strength));
    }

    private Vector3 GetMuzzleWorldPosition()
    {
        return muzzlePoint != null ? muzzlePoint.position : transform.position + Vector3.up * 2.2f;
    }

    private IEnumerator WaitCombatSeconds(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && !dead)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void CleanupSpawnedCombatObjects()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] != null)
                Destroy(spawnedObjects[i]);
        }
        spawnedObjects.Clear();
    }

    private static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
    }

    private void OnDestroy()
    {
        if (cutsceneIsolation != null) cutsceneIsolation.Restore();
        if (bossAudio != null) bossAudio.StopAllLoops();
        if (health != null) health.Damaged -= HandleDamaged;
        if (introInputLock != null) introInputLock.Dispose();
        if (phaseInputLock != null) phaseInputLock.Dispose();
        if (deathInputLock != null) deathInputLock.Dispose();
        CleanupSpawnedCombatObjects();
        if (cameraDirector != null) cameraDirector.SnapToCombat();
        if (hud != null) Destroy(hud.gameObject);
    }
}
