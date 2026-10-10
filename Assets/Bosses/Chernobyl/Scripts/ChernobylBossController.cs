using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public sealed partial class ChernobylBossController : MonoBehaviour, IEnemyDeathOverride
{
    private enum BossState { Dormant, Intro, Combat, Transition, Meltdown, Dead }
    private enum PatternKind
    {
        TargetBlast, CrossBlast, SparseGrid, ChainGrid, CoreShockwave, MeltdownGrid,
        CheckerA, CheckerB,
        SweepL2R, SweepR2L, SweepTopDown, SweepBottomUp,
        CompressHorizontal, ExpandHorizontal, CompressVertical, ExpandVertical,
        ChainInvert, SafeShift,

        // V11 spatial-control families
        OuterCollapse, CenterEvacuation, TwinTarget, GapSweep, SplitField,
        DelayedTiles, DelayedCross, MovingSafeLane, CornerQuarantine, RadiationRing,
        CorridorCrush, GridChase,
        RotatingLines, RotatingSafeSector, SpiralGrid, SafeZoneRelay, DelayedCascade,
        ReactorPulse, SectorCollapse,
        BaitVolleyV17, CoreRadiationV17, PipeCascadeV17, DirectionalVentV17,
        CrossVentV17, FinalOverloadV17
    }

    private struct GridCell
    {
        public Vector3 Center;
        public Vector2 Size;
        public GridCell(Vector3 center, Vector2 size) { Center = center; Size = size; }
    }

    private const int BossMaxHealth = 2700;
    private const float PhaseTwoRatio = 0.65f;
    private const float MeltdownRatio = 0.30f;

    private MapManager mapManager;
    private RoomController ownerRoom;
    private BoxCollider2D arenaBounds;
    private EnemyHealth health;
    private BoxCollider2D hurtbox;
    private BoxCollider2D solidCollider;
    private Transform visualRoot;
    private SpriteRenderer coreRenderer;
    private Transform coreRing;
    private SpriteRenderer[] structureRenderers;
    private PlayerHealth playerHealth;
    private Transform player;
    private ChernobylBossHUD hud;
    private ChernobylBossMusic music;
    private ExecutorCutsceneIsolation cutsceneIsolation;
    private IDisposable inputLock;
    private Coroutine combatRoutine;
    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private BossState state = BossState.Dormant;
    private int phase = 1;
    private bool combatStarted;
    private bool dead;
    private bool transitionRequested;
    private bool meltdownRequested;
    private bool playerDefeatHandled;
    private bool suppressTransitions;
    private float hitPulse;
    private float corePulseTime;
    private float skipAllowedAt;
    private Vector3 visualBasePosition;
    private AudioSource sfxSource;
    private AudioClip activationSfx;
    private AudioClip overloadSfx;
    private AudioClip explosionSfx;
    private AudioClip shutdownSfx;
    // V11 optional event hooks. Dedicated files can be dropped into Resources/Bosses/Chernobyl/SFX
    // without changing controller code. Safe fallbacks preserve current audio when a clip is absent.
    private AudioClip hitSfxV11;
    private AudioClip heavyHitSfxV11;
    private AudioClip gridWarningSfxV11;
    private AudioClip gridExplosionSfxV11;
    private AudioClip shockwaveChargeSfxV11;
    private AudioClip shockwaveFireSfxV11;
    private AudioClip pageTransitionSfxV11;
    private AudioClip antiBurstChargeSfxV11;
    private AudioClip antiBurstFireSfxV11;
    private readonly Dictionary<AudioClip, AudioClip[]> sfxVariantsV12 = new Dictionary<AudioClip, AudioClip[]>();

    public EnemyHealth Health => health;
    public RoomController OwnerRoom => ownerRoom;
    public bool IsDead => dead;
    public int Phase => phase;

    public void Initialize(MapManager manager, RoomController room, BoxCollider2D arena, EnemyHealth enemyHealth,
        BoxCollider2D bossHurtbox, BoxCollider2D bossSolidCollider, Transform bossVisualRoot,
        SpriteRenderer core, Transform rotatingRing, SpriteRenderer[] structure)
    {
        mapManager = manager;
        ownerRoom = room;
        arenaBounds = arena;
        health = enemyHealth;
        hurtbox = bossHurtbox;
        solidCollider = bossSolidCollider;
        visualRoot = bossVisualRoot;
        coreRenderer = core;
        coreRing = rotatingRing;
        structureRenderers = structure ?? Array.Empty<SpriteRenderer>();
        visualBasePosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;

        playerHealth = FindAnyObjectByType<PlayerHealth>();
        player = playerHealth != null ? playerHealth.transform : null;
        hud = ChernobylBossHUD.Create();
        // Keep the screen-space boss HUD owned by the boss room. If the player enters the
        // exit portal before the death coroutine reaches its final Destroy call, destroying
        // the old room will still clean the HUD instead of leaking it into the next stage.
        if (hud != null && ownerRoom != null)
            hud.transform.SetParent(ownerRoom.transform, false);
        music = ChernobylBossMusic.Create(transform);
        cutsceneIsolation = new ExecutorCutsceneIsolation();
        BuildSfx();
        InitializeOverhaulV10();
        InitializeThermalV19();
        // V17: ordinary damage remains valuable; no hidden opening multiplier.
        openingV15 = null;

        if (health != null)
        {
            health.Damaged += OnDamaged;
            hud.ReportHealth(health.CurrentHealth, health.MaxHealth, true);
        }
        SetCombatColliders(false);
        ApplyPhaseVisuals();
    }

    private void BuildSfx()
    {
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false; sfxSource.spatialBlend = 0f;
        sfxSource.ignoreListenerPause = false; sfxSource.volume = 0.72f;
        sfxVariantsV12.Clear();
        LoadTeamAudioV20();
    }

    private AudioClip[] LoadVariantsV12(string root, string baseName)
    {
        List<AudioClip> clips = new List<AudioClip>(3);
        for (int i = 1; i <= 3; i++)
        {
            AudioClip clip = Resources.Load<AudioClip>(root + baseName + "_" + i.ToString("00"));
            if (clip != null) clips.Add(clip);
        }
        return clips.Count > 0 ? clips.ToArray() : null;
    }

    private void AssignVariantsV12(ref AudioClip target, AudioClip[] variants)
    {
        if (variants == null || variants.Length == 0) return;
        target = variants[0];
        sfxVariantsV12[target] = variants;
    }

    private void Update()
    {
        if (dead) return;
        if(playerHealth!=null && playerHealth.IsDead)
        {
            if(hud!=null) hud.HideAllImmediate();
            if(audioV21!=null) audioV21.StopAll();
            if(combatStarted) { combatStarted=false; StopAllCoroutines(); CleanupSpawnedObjects();
                ClearAllGridWarningsV11(); ReleaseAntiBurstInputLockV11();
                if(inputLock!=null) { inputLock.Dispose(); inputLock=null; }
                if(cameraFxV10!=null) cameraFxV10.SetCutsceneZoomV22(0);
                SetThermalV19(0f,0f,false); }
            return;
        }
        UpdateCoreLightV10();
        UpdateDebugControlsV11();
        corePulseTime += Time.deltaTime;
        hitPulse = Mathf.MoveTowards(hitPulse, 0f, Time.deltaTime * 5.5f);
        float pulseSpeed = phase == 1 ? 2.7f : phase == 2 ? 4.3f : 7.2f;
        float pulseAmount = phase == 1 ? 0.055f : phase == 2 ? 0.075f : 0.11f;
        float pulse = 1f + Mathf.Sin(corePulseTime * pulseSpeed) * pulseAmount + hitPulse * 0.07f;
        if (coreRenderer != null)
            coreRenderer.transform.localScale = Vector3.one * (coreRenderer.gameObject.name == "CoreV16" ? 1f : 0.42f) * pulse;
        if (coreRing != null)
            coreRing.Rotate(0f, 0f, (thermalVulnerableV19 || phasePresentationV22 ? 0f : 22f + thermalHeatV19 * 90f) * Time.deltaTime);

        if (!phasePresentationV22 && phase == 3 && visualRoot != null && state == BossState.Combat)
            visualRoot.localPosition = visualBasePosition + (Vector3)(Random.insideUnitCircle * 0.006f);
        else if (!phasePresentationV22 && visualRoot != null && state == BossState.Combat)
            visualRoot.localPosition = visualBasePosition;

        if (!playerDefeatHandled && playerHealth != null && playerHealth.IsDead)
        {
            playerDefeatHandled = true;
            if (music != null) music.SetDefeatMix(0.88f, 0.35f);
        }
    }

    public void BeginIntro()
    {
        if (dead || combatStarted || state != BossState.Dormant) return;
        StartCoroutine(IntroRoutine());
    }

    public void DebugBeginCombatAtHealth(float normalizedHealth)
    {
        if (dead) return;
        ReleaseAntiBurstInputLockV11();
        StopAllCoroutines();
        if (inputLock != null) { inputLock.Dispose(); inputLock = null; }
        if (cutsceneIsolation != null) cutsceneIsolation.Restore();
        CleanupSpawnedObjects();
        ClearAllGridWarningsV11();
        ResetDirectorV11();

        float ratio = Mathf.Clamp(normalizedHealth, 0.02f, 1f);
        suppressTransitions = true;
        if (health != null)
        {
            int target = Mathf.Max(1, Mathf.RoundToInt(health.MaxHealth * ratio));
            int damage = Mathf.Max(0, health.CurrentHealth - target);
            if (damage > 0) health.TakeDamage(damage, Vector2.down, EnemyHitKind.Environment);
        }
        suppressTransitions = false;
        phase = ratio <= MeltdownRatio ? 3 : ratio <= PhaseTwoRatio ? 2 : 1;
        transitionRequested = false;
        meltdownRequested = false;
        combatStarted = true;
        state = BossState.Combat;
        ApplyPhaseVisuals();
        SetCombatColliders(true);
        if (hud != null)
        {
            hud.HideAllImmediate();
            hud.ReportHealth(health != null ? health.CurrentHealth : BossMaxHealth, health != null ? health.MaxHealth : BossMaxHealth, true);
            hud.ShowBossBar(true);
        }
        if (music != null)
        {
            music.PrepareSilentIntro();
            if (phase >= 2) music.PlayPhaseTwo(0.15f);
            else music.PlayPhaseOne(0.15f);
        }
        if (cameraFxV10 != null) cameraFxV10.BeginBoss(phase);
        combatRoutine = StartCoroutine(CombatLoop());
    }

    private IEnumerator IntroRoutine()
    {
        state = BossState.Intro;
        if (cameraFxV10 != null)
        {
            cameraFxV10.BeginBoss(1);
            cameraFxV10.PulseTransition(1);
        }
        inputLock = GameInputState.Acquire("ChernobylIntro");
        if (music != null) music.PrepareSilentIntro();
        if (hud != null)
        {
            hud.HideAllImmediate();
            hud.SetSkipVisible(true);
            hud.ShowIntro("반응로 이상 감지");
        }
        if (cutsceneIsolation != null && hud != null)
            cutsceneIsolation.Begin(hud.Canvas, transform);

        SetCombatColliders(false);
        SetVisualAlpha(0.18f);
        skipAllowedAt = Time.unscaledTime + 0.75f;
        float elapsed = 0f;
        bool activationPlayed = false;
        while (elapsed < 2.9f && !dead)
        {
            if (ShouldSkipIntro()) break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 2.9f);
            float flicker = t < 0.45f ? (Mathf.PingPong(elapsed * 7f, 1f) > 0.48f ? 1f : 0.32f) : 1f;
            SetVisualAlpha(Mathf.Lerp(0.18f, 1f, t) * flicker);
            if (!activationPlayed && elapsed >= 1.05f)
            {
                activationPlayed = true;
                PlaySfx(activationSfx, 0.78f, 0.96f);
                if (hud != null) hud.ShowIntro("비정상 노심 확인");
                ChernobylBossEffects.SpawnCoreSparks(transform.position + Vector3.up * 0.1f,
                    new Color(0.48f, 1f, 0.30f, 1f), 12, 2.8f);
            }
            if (elapsed >= 1.95f && hud != null)
                hud.ShowIntro("체르노빌");
            yield return null;
        }

        SetVisualAlpha(1f);
        if (hud != null)
        {
            hud.ShowIntro(string.Empty);
            hud.SetSkipVisible(false);
        }
        if (cutsceneIsolation != null) cutsceneIsolation.Restore();
        if (inputLock != null) { inputLock.Dispose(); inputLock = null; }

        combatStarted = true;
        state = BossState.Combat;
        SetCombatColliders(true);
        if (hud != null)
            yield return hud.RevealBossBar(health != null ? health.CurrentHealth : BossMaxHealth,
                health != null ? health.MaxHealth : BossMaxHealth, 0.48f);
        if (music != null) music.PlayPhaseOne(0.65f);
        if (cameraFxV10 != null) cameraFxV10.BeginBoss(phase);
        combatRoutine = StartCoroutine(CombatLoop());
    }

    private bool ShouldSkipIntro()
    {
        return Time.unscaledTime >= skipAllowedAt && Input.anyKeyDown && !Input.GetKeyDown(KeyCode.Escape);
    }

    private IEnumerator CombatLoop() { yield return ThermalLoopV19(); }

    private IEnumerator RunPattern(PatternKind pattern)
    {
        switch (pattern)
        {
            case PatternKind.TargetBlast: yield return TargetBlastRoutine(); break;
            case PatternKind.CrossBlast: yield return CrossBlastRoutine(); break;
            case PatternKind.SparseGrid: yield return SparseGridRoutine(); break;
            case PatternKind.ChainGrid: yield return ChainGridRoutine(); break;
            case PatternKind.CoreShockwave: yield return CoreShockwaveRoutine(); break;
            case PatternKind.MeltdownGrid: yield return MeltdownGridRoutine(); break;
        }
    }

    private IEnumerator TargetBlastRoutine()
    {
        Vector3 target = ClampArenaPoint(GetPlayerPosition(), 0.55f);
        float radius = phase == 1 ? 1.05f : phase == 2 ? 1.12f : 1.22f;
        float warning = phase == 1 ? 1.05f : phase == 2 ? 0.92f : 0.82f;
        Color color = CurrentHazardColor();
        GameObject telegraph = ChernobylBossEffects.CreateCircleTelegraph(target, radius, warning, color);
        RegisterSpawnedObject(telegraph);
        yield return WaitCombatSeconds(warning);
        if (dead) yield break;
        ForgetAndDestroy(telegraph);
        ChernobylBossEffects.SpawnCircleBlast(target, radius, color);
        DamagePlayerCircle(target, radius, 1);
        PlaySfx(explosionSfx, phase == 1 ? 0.60f : 0.68f, phase == 3 ? 0.90f : 0.96f);
        Shake(0.08f + phase * 0.012f);
        if (cameraFxV10 != null) cameraFxV10.PulseExplosion(0.52f + phase * 0.10f);
    }

    private IEnumerator CrossBlastRoutine()
    {
        Bounds bounds = GetArenaBounds();
        Vector3 target = ClampArenaPoint(GetPlayerPosition(), 0.35f);
        float thickness = phase == 1 ? 0.82f : phase == 2 ? 0.94f : 1.05f;
        float warning = phase == 1 ? 1.08f : phase == 2 ? 0.82f : 0.66f;
        Vector3 vCenter = new Vector3(target.x, bounds.center.y, 0f);
        Vector3 hCenter = new Vector3(bounds.center.x, target.y, 0f);
        Vector2 vSize = new Vector2(thickness, Mathf.Max(0.5f, bounds.size.y - 0.35f));
        Vector2 hSize = new Vector2(Mathf.Max(0.5f, bounds.size.x - 0.35f), thickness);
        Color color = CurrentHazardColor();
        GameObject v = ChernobylBossEffects.CreateRectTelegraph(vCenter, vSize, warning, color);
        GameObject h = ChernobylBossEffects.CreateRectTelegraph(hCenter, hSize, warning, color);
        RegisterSpawnedObject(v); RegisterSpawnedObject(h);
        yield return WaitCombatSeconds(warning);
        if (dead) yield break;
        bool hit = PlayerInsideRect(vCenter, vSize) || PlayerInsideRect(hCenter, hSize);
        ForgetAndDestroy(v); ForgetAndDestroy(h);
        ChernobylBossEffects.SpawnRectBlast(vCenter, vSize, color);
        ChernobylBossEffects.SpawnRectBlast(hCenter, hSize, color);
        if (hit) DamagePlayerOnce(1);
        PlaySfx(explosionSfx, 0.66f, 0.92f);
        Shake(0.095f);
        if (cameraFxV10 != null) cameraFxV10.PulseExplosion(0.60f);

        if (phase == 3 && !dead)
        {
            yield return WaitCombatSeconds(0.24f);
            Vector3 shifted = ClampArenaPoint(GetPlayerPosition(), 0.35f);
            vCenter.x = shifted.x;
            hCenter.y = shifted.y;
            v = ChernobylBossEffects.CreateRectTelegraph(vCenter, vSize, 0.48f, color);
            h = ChernobylBossEffects.CreateRectTelegraph(hCenter, hSize, 0.48f, color);
            RegisterSpawnedObject(v); RegisterSpawnedObject(h);
            yield return WaitCombatSeconds(0.48f);
            if (dead) yield break;
            hit = PlayerInsideRect(vCenter, vSize) || PlayerInsideRect(hCenter, hSize);
            ForgetAndDestroy(v); ForgetAndDestroy(h);
            ChernobylBossEffects.SpawnRectBlast(vCenter, vSize, color);
            ChernobylBossEffects.SpawnRectBlast(hCenter, hSize, color);
            if (hit) DamagePlayerOnce(1);
            PlaySfx(explosionSfx, 0.63f, 0.95f);
            if (cameraFxV10 != null) cameraFxV10.PulseExplosion(0.56f);
        }
    }

    private IEnumerator SparseGridRoutine()
    {
        List<GridCell> cells = BuildFixedGridV10();
        Shuffle(cells);
        int count = Mathf.Clamp(Mathf.RoundToInt(cells.Count * (phase == 1 ? 0.34f : 0.40f)), 3, Mathf.Max(3, cells.Count - 4));
        List<GridCell> hazards = cells.GetRange(0, Mathf.Min(count, cells.Count));
        float warning = phase == 1 ? 1.12f : phase == 2 ? 0.90f : 0.74f;
        yield return TelegraphAndDetonateCells(hazards, warning, CurrentHazardColor());
    }

    private IEnumerator ChainGridRoutine()
    {
        List<GridCell> cells = BuildFixedGridV10();
        Color color = CurrentHazardColor();
        int waves = phase >= 3 ? 4 : 3;
        int perWave = Mathf.Clamp(Mathf.RoundToInt(cells.Count * (phase >= 3 ? 0.16f : 0.18f)), 3, 7);
        float warning = phase >= 3 ? 0.66f : 0.82f;
        for (int wave = 0; wave < waves && !dead; wave++)
        {
            Shuffle(cells);
            List<GridCell> hazards = new List<GridCell>();
            for (int i = 0; i < Mathf.Min(perWave, cells.Count); i++) hazards.Add(cells[i]);
            yield return TelegraphAndDetonateCells(hazards, warning, color);
            if (!dead) yield return WaitCombatSeconds(phase >= 3 ? 0.16f : 0.20f);
        }
    }

    private IEnumerator MeltdownGridRoutine()
    {
        List<GridCell> cells = BuildFixedGridV10();
        int cols, rows;
        GetGridDimensionsV11(out cols, out rows);
        List<int[]> safeBlocks = BuildSafeBlocksV11(cols, rows, 2, 2);
        if (safeBlocks.Count == 0) yield break;
        float warning = phase >= 3 ? 0.76f : 0.92f;
        int safeIndex = ChooseReachableSafeSetV11(cells, safeBlocks, -1, GetPlayerPosition(), BasicReachDistanceV11(warning));
        yield return DetonateAllExceptSafeV11(cells, safeBlocks[safeIndex], warning);
    }

    private IEnumerator TelegraphAndDetonateCells(List<GridCell> hazards, float warning, Color color)
    {
        if (hazards == null || hazards.Count == 0) yield break;
        PlaySfx(gridWarningSfxV11, hazards.Count >= 10 ? 0.18f : 0.14f, phase >= 3 ? 1.08f : 1.00f);
        List<GameObject> marks = new List<GameObject>();
        bool simpleMassTelegraph = hazards.Count >= 10;
        for (int i = 0; i < hazards.Count; i++)
        {
            // Large checker/meltdown patterns previously created four child border objects
            // per cell. In mass patterns one pulsing fill per cell keeps the warning clear
            // while cutting transient GameObject creation dramatically.
            GameObject mark = ChernobylBossEffects.CreateRectTelegraph(
                hazards[i].Center, hazards[i].Size, warning, color, 11, simpleMassTelegraph);
            marks.Add(mark);
            RegisterSpawnedObject(mark);
        }
        yield return WaitCombatSeconds(warning);
        if (dead) yield break;
        bool hit = false;
        // V11 performance fix: mass-grid attacks used to emit debris on every cell,
        // producing hundreds of temporary GameObjects in a single frame. Keep every
        // blast flash, but only a bounded subset emits pooled debris.
        int fragmentStride = hazards.Count >= 16 ? 5 : hazards.Count >= 10 ? 3 : 2;
        for (int i = 0; i < hazards.Count; i++)
        {
            if (PlayerInsideRect(hazards[i].Center, hazards[i].Size)) hit = true;
            bool emitFragments = hazards.Count <= 6 || (i % fragmentStride) == 0;
            ChernobylBossEffects.SpawnRectBlast(hazards[i].Center, hazards[i].Size, color, emitFragments);
        }
        for (int i = 0; i < marks.Count; i++) ForgetAndDestroy(marks[i]);
        if (hit) DamagePlayerOnce(1);
        PlaySfx(gridExplosionSfxV11, hazards.Count >= 10 ? 0.72f : 0.62f, phase >= 3 ? 0.90f : 0.96f);
        Shake(hazards.Count >= 10 ? 0.13f : 0.09f);
        if (cameraFxV10 != null) cameraFxV10.PulseExplosion(hazards.Count >= 10 ? 0.78f : 0.46f);
    }

    private IEnumerator CoreShockwaveRoutine()
    {
        Color color = CurrentHazardColor();
        float warning = phase >= 3 ? 0.68f : phase == 2 ? 0.82f : 0.94f;
        GameObject mark = ChernobylBossEffects.CreateCircleTelegraph(transform.position, 0.82f, warning, color, 13);
        RegisterSpawnedObject(mark);
        ChernobylBossEffects.SpawnCoreSparks(transform.position + Vector3.up * 0.1f, color, phase >= 3 ? 12 : 8, 2.3f);
        PlaySfx(shockwaveChargeSfxV11, 0.42f, phase >= 3 ? 1.04f : 0.98f);
        yield return WaitCombatSeconds(warning);
        if (dead) yield break;
        ForgetAndDestroy(mark);
        Bounds bounds = GetArenaBounds();
        float endRadius = Mathf.Max(1.6f, Mathf.Min(bounds.extents.y - 0.18f, 2.55f));
        ChernobylShockwave.Create(transform.position, 0.56f, endRadius, phase >= 3 ? 0.22f : 0.18f,
            phase >= 3 ? 0.58f : 0.72f, 1, color);
        PlaySfx(shockwaveFireSfxV11, 0.66f, 0.88f);
        Shake(0.11f);
        if (cameraFxV10 != null) cameraFxV10.PulseExplosion(0.72f);
        yield return WaitCombatSeconds(phase >= 3 ? 0.62f : 0.76f);
    }

    private IEnumerator PhaseTwoRoutine()
    {
        if (dead || phase >= 2) yield break;
        transitionRequested = false;
        ClearAllGridWarningsV11();
        state = BossState.Transition;
        inputLock = GameInputState.Acquire("ChernobylPhase2");
        phase = 2;
        ApplyPhaseVisuals();
        NotifyGridReconfiguredV11();
        if (cameraFxV10 != null) cameraFxV10.SetPhase(2);
        if (hud != null) hud.ShowPhase(string.Empty);
        PlaySfx(pageTransitionSfxV11, 0.82f, 0.95f);
        if (music != null) music.PlayPhaseTwo(0.85f);
        ChernobylBossEffects.SpawnCoreSparks(transform.position + Vector3.up * 0.1f, CurrentHazardColor(), 18, 3.6f);
        if (hud != null) StartCoroutine(hud.Flash(new Color(0.48f, 1f, 0.20f, 1f), 0.22f, 0.70f));
        float elapsed = 0f;
        while (elapsed < 1.15f && !dead)
        {
            elapsed += Time.unscaledDeltaTime;
            if (visualRoot != null) visualRoot.localPosition = visualBasePosition + (Vector3)(Random.insideUnitCircle * 0.018f);
            yield return null;
        }
        if (visualRoot != null) visualRoot.localPosition = visualBasePosition;
        if (hud != null) hud.ShowPhase(string.Empty);
        if (inputLock != null) { inputLock.Dispose(); inputLock = null; }
        state = BossState.Combat;
    }

    private IEnumerator MeltdownRoutine()
    {
        if (dead || phase >= 3) yield break;
        meltdownRequested = false;
        ClearAllGridWarningsV11();
        state = BossState.Meltdown;
        inputLock = GameInputState.Acquire("ChernobylMeltdown");
        phase = 3;
        ApplyPhaseVisuals();
        NotifyGridReconfiguredV11();
        if (cameraFxV10 != null) cameraFxV10.SetPhase(3);
        if (hud != null) hud.ShowPhase(string.Empty);
        PlaySfx(pageTransitionSfxV11, 0.92f, 0.88f);
        ChernobylBossEffects.SpawnCoreSparks(transform.position + Vector3.up * 0.1f, CurrentHazardColor(), 26, 4.2f);
        if (hud != null) StartCoroutine(hud.Flash(new Color(0.64f, 1f, 0.18f, 1f), 0.28f, 0.74f));
        yield return WaitRealtime(0.82f);
        if (hud != null) hud.ShowPhase(string.Empty);
        if (inputLock != null) { inputLock.Dispose(); inputLock = null; }
        state = BossState.Combat;
    }

    private void OnDamaged(EnemyHealth source, int damage, Vector2 direction)
    {
        if (source != health || dead) return;
        hitPulse = 0.28f;
        TriggerHitFeedbackV11(damage, direction);

        if (hud != null) hud.ReportHealth(health.CurrentHealth, health.MaxHealth, false);
        if (suppressTransitions) return;
        float ratio = health.NormalizedHealth;
        if (ratio <= MeltdownRatio && phase < 3) meltdownRequested = true;
        else if (ratio <= PhaseTwoRatio && phase == 1) transitionRequested = true;
    }

    public bool HandleRequestedDeath(EnemyHealth requestedHealth, Vector2 hitDirection, EnemyHitKind hitKind)
    {
        if (dead || requestedHealth != health) return dead;
        dead = true;
        state = BossState.Dead;
        if (openingV15 != null) openingV15.Close();
        ClearGuardHazardsV15();
        ReleaseAntiBurstInputLockV11();
        StopAllCoroutines();
        StartCoroutine(DeathRoutine(hitDirection));
        return true;
    }

    private IEnumerator DeathRoutine(Vector2 hitDirection)
    {
        inputLock = GameInputState.Acquire("ChernobylDeath");
        SetCombatColliders(false);
        if (guardDirectorV13 != null) guardDirectorV13.ShutdownAllGuards();
        CleanupSpawnedObjects();
        ClearAllGridWarningsV11();
        if (music != null) music.CrossFadeBackToGameplay(1.7f);
        if (hud != null)
        {
            hud.ReportHealth(0, health != null ? health.MaxHealth : BossMaxHealth, true);
            hud.HideBossBarImmediate();
            hud.ShowPhase(string.Empty);
        }
        if(audioV21!=null) audioV21.StopAll();
        PlaySfx(shutdownSfx, 0.70f, 1f);

        float elapsed = 0f;
        while (elapsed < 0.65f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.65f);
            if (visualRoot != null) visualRoot.localPosition = visualBasePosition + (Vector3)(Random.insideUnitCircle * Mathf.Lerp(0.012f, 0.045f, t));
            if (coreRenderer != null)
            {
                Color c = Color.Lerp(CurrentHazardColor(), new Color(0.18f, 0.25f, 0.18f, 1f), t);
                coreRenderer.color = c;
            }
            yield return null;
        }
        if (visualRoot != null) visualRoot.localPosition = visualBasePosition;
        ChernobylBossEffects.SpawnDeathBurst(transform.position + Vector3.up * 0.1f);
        Shake(0.16f);
        if (cameraFxV10 != null)
        {
            cameraFxV10.PulseExplosion(1f);
            cameraFxV10.EndBoss();
        }
        SetStructureColor(new Color(0.38f, 0.43f, 0.40f, 1f));
        if (coreRenderer != null) coreRenderer.color = new Color(0.08f, 0.12f, 0.08f, 1f);

        if (playerHealth != null && !playerHealth.IsDead)
            playerHealth.RestoreToFull();
        SpawnRewards();
        yield return WaitRealtime(0.9f);
        if (mapManager != null) mapManager.NotifyChernobylDefeated(ownerRoom);
        if (hud != null)
        {
            hud.ShowPhase(string.Empty);
            yield return hud.FadeOut(0.45f);
        }
        if (inputLock != null) { inputLock.Dispose(); inputLock = null; }
        if (hud != null) Destroy(hud.gameObject);
        if (health != null) health.CompleteDeferredDeath(hitDirection);
    }

    private void SpawnRewards()
    {
        Vector3 origin = transform.position + Vector3.up * 0.45f;
        ByteDropper dropper = GetComponent<ByteDropper>();
        if (dropper == null) dropper = gameObject.AddComponent<ByteDropper>();
        dropper.ConfigureDrops(6, 6, 4);
        dropper.DropBytes(origin);
        for (int i = 0; i < 3; i++)
            AmmoDropper.SpawnPickupAt(origin + (Vector3)(Random.insideUnitCircle * 0.20f), 8, ownerRoom);
    }

    private void ApplyPhaseVisuals()
    {
        SetStructureColor(phase == 1 ? new Color(0.90f, 0.94f, 0.92f, 1f) :
            phase == 2 ? new Color(0.94f, 0.97f, 0.90f, 1f) : new Color(0.98f, 0.98f, 0.88f, 1f));
        if (coreRenderer != null) coreRenderer.color = CurrentHazardColor();
    }

    private void SetStructureColor(Color target)
    {
        for (int i = 0; i < structureRenderers.Length; i++)
        {
            SpriteRenderer r = structureRenderers[i];
            if (r == null || r.gameObject.name == "InnerCavity" || r.gameObject.name == "CoreBack") continue;
            if (r.gameObject.name == "BodyV16" || r.gameObject.name == "RingV16")
            {
                r.color = new Color(1f, 1f, 1f, r.color.a);
                continue;
            }
            float brightness = r.gameObject.name.Contains("Band") ? 0.94f : 1f;
            r.color = new Color(target.r * brightness, target.g * brightness, target.b * brightness, r.color.a);
        }
    }

    private void SetVisualAlpha(float alpha)
    {
        float a = Mathf.Clamp01(alpha);
        for (int i = 0; i < structureRenderers.Length; i++)
        {
            if (structureRenderers[i] == null) continue;
            Color c = structureRenderers[i].color;
            c.a = a;
            structureRenderers[i].color = c;
        }
        if (coreRenderer != null)
        {
            Color c = coreRenderer.color; c.a = a; coreRenderer.color = c;
        }
    }

    private Color CurrentHazardColor()
    {
        if (phase >= 3) return new Color(0.72f, 1f, 0.18f, 1f);
        if (phase == 2) return new Color(0.54f, 1f, 0.22f, 1f);
        return new Color(0.26f, 0.94f, 0.36f, 1f);
    }

    private List<GridCell> BuildGrid(int cols, int rows)
    {
        Bounds b = GetArenaBounds();
        float marginX = 0.28f;
        float marginY = 0.26f;
        float width = Mathf.Max(1f, b.size.x - marginX * 2f);
        float height = Mathf.Max(1f, b.size.y - marginY * 2f);
        float cellW = width / Mathf.Max(1, cols);
        float cellH = height / Mathf.Max(1, rows);
        Vector2 visualSize = new Vector2(cellW * 0.90f, cellH * 0.86f);
        List<GridCell> result = new List<GridCell>(cols * rows);
        float startX = b.min.x + marginX + cellW * 0.5f;
        float startY = b.min.y + marginY + cellH * 0.5f;
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
                result.Add(new GridCell(new Vector3(startX + x * cellW, startY + y * cellH, 0f), visualSize));
        return result;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    private Bounds GetArenaBounds()
    {
        if (RoomLayoutV24.TryGetBounds(ownerRoom, out Bounds layoutBounds)) return layoutBounds;
        // EnemySpawnArea는 일반 몹 생성용 영역이라 방 전체보다 작고 비대칭이다.
        // 특히 RoomBase의 좌/우/상/하 입장 스폰 지점이 모두 그 바깥에 있어
        // V10에서는 맵 끝에 의도치 않은 완전 안전지대가 생겼다.
        // 체르노빌의 영역 공격은 네 방향 실제 플레이어 스폰 지점을 기준으로
        // 방 전체 전투 범위를 구성한다.
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
                Vector3 center = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f);
                Vector3 size = new Vector3(Mathf.Max(1f, maxX - minX), Mathf.Max(1f, maxY - minY), 1f);
                return new Bounds(center, size);
            }
        }

        if (arenaBounds != null) return arenaBounds.bounds;
        return new Bounds(transform.position, new Vector3(12f, 5f, 1f));
    }

    private Vector3 ClampArenaPoint(Vector3 point, float margin)
    {
        Bounds b = GetArenaBounds();
        point.x = Mathf.Clamp(point.x, b.min.x + margin, b.max.x - margin);
        point.y = Mathf.Clamp(point.y, b.min.y + margin, b.max.y - margin);
        point.z = 0f;
        return point;
    }

    private Vector3 GetPlayerPosition()
    {
        if (player == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>();
            player = playerHealth != null ? playerHealth.transform : null;
        }
        return player != null ? player.position : transform.position + Vector3.left * 2f;
    }

    private void DamagePlayerCircle(Vector3 center, float radius, int damage)
    {
        if (playerHealth == null || playerHealth.IsDead) return;
        Collider2D[] colliders = playerHealth.GetComponentsInChildren<Collider2D>(true);
        float closest = Vector2.Distance(center, playerHealth.transform.position);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D c = colliders[i];
            if (c == null || !c.enabled || c.isTrigger) continue;
            closest = Mathf.Min(closest, Vector2.Distance(center, c.ClosestPoint(center)));
        }
        if (closest <= radius) playerHealth.TakeDamage(damage);
    }

    private bool PlayerInsideRect(Vector3 center, Vector2 size)
    {
        if (playerHealth == null || playerHealth.IsDead) return false;
        Bounds hazard = new Bounds(center, new Vector3(size.x, size.y, 2f));
        Collider2D[] colliders = playerHealth.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D c = colliders[i];
            if (c == null || !c.enabled || c.isTrigger) continue;
            if (hazard.Intersects(c.bounds)) return true;
        }
        return hazard.Contains(playerHealth.transform.position);
    }

    private void DamagePlayerOnce(int damage)
    {
        if (playerHealth != null && !playerHealth.IsDead) playerHealth.TakeDamage(damage);
    }

    private void SetCombatColliders(bool enabledState)
    {
        if (hurtbox != null) hurtbox.enabled = enabledState;
        if (solidCollider != null) solidCollider.enabled = enabledState;
    }

    public void RegisterSpawnedObject(GameObject target)
    {
        if (target != null && !spawnedObjects.Contains(target)) spawnedObjects.Add(target);
    }

    private void ForgetAndDestroy(GameObject target)
    {
        if (target == null) return;
        spawnedObjects.Remove(target);
        Destroy(target);
    }

    private void CleanupSpawnedObjects()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
            if (spawnedObjects[i] != null) Destroy(spawnedObjects[i]);
        spawnedObjects.Clear();
    }

    private void PlaySfx(AudioClip clip, float volume, float pitch)
    {
        if (sfxSource == null || clip == null) return;
        if (sfxVariantsV12.TryGetValue(clip, out AudioClip[] variants) && variants != null && variants.Length > 0)
            clip = variants[Random.Range(0, variants.Length)];
        if(audioV21!=null) audioV21.Play(clip,volume,pitch);
    }

    private void Shake(float strength)
    {
        if (GameFeelManager.Instance != null)
            GameFeelManager.Instance.Shake(0.14f, Mathf.Clamp(strength, 0.03f, 0.18f));
    }

    private IEnumerator WaitCombatSeconds(float duration)
    {
        float elapsed = 0f;
        float safe = Mathf.Max(0f, duration);
        while (elapsed < safe && !dead)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitRealtime(float duration)
    {
        float elapsed = 0f;
        float safe = Mathf.Max(0f, duration);
        while (elapsed < safe)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void OnDestroy()
    {
        if (guardDirectorV13 != null) guardDirectorV13.ShutdownAllGuards();
        if (health != null) health.Damaged -= OnDamaged;
        ReleaseAntiBurstInputLockV11();
        if (inputLock != null) inputLock.Dispose();
        if (cutsceneIsolation != null) cutsceneIsolation.Restore();
        ClearAllGridWarningsV11();
        if (cameraFxV10 != null) cameraFxV10.EndBoss();
        if (music != null && !dead) music.RestoreGameplayImmediate();
        if (hud != null)
        {
            hud.HideAllImmediate();
            Destroy(hud.gameObject);
            hud = null;
        }
    }
}
