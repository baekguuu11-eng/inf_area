using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// v6.1 기본 색보정과 강한 공격 순간의 짧은 후처리 펄스를 관리한다.
/// 평상시 화면은 선명하게 유지하고, 샷건/해머/레이저 같은 강한 순간에만 강조한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CombatPostProcessV61 : MonoBehaviour
{
    private static CombatPostProcessV61 instance;
    private static bool sceneHooked;

    private Bloom bloom;
    private Vignette vignette;
    private ChromaticAberration chromaticAberration;
    private ColorAdjustments colorAdjustments;

    private float baseBloom = 0.26f;
    private float pulseBloom;
    private float pulseChroma;
    private float pulseExposure;
    private float pulseDuration;
    private float pulseElapsed;
    private PlayerHealth playerHealth;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        instance = null;
        sceneHooked = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!sceneHooked)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            sceneHooked = true;
        }
        TryAttach(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryAttach(scene);
    }

    private static void TryAttach(Scene scene)
    {
        if (!scene.IsValid() || scene.name == "MainMenu" || scene.name == "OpeningCutscene")
            return;

        Camera target = Camera.main;
        if (target == null)
            target = Object.FindFirstObjectByType<Camera>();
        if (target == null)
            return;

        CombatPostProcessV61 existing = target.GetComponent<CombatPostProcessV61>();
        if (existing == null)
            existing = target.gameObject.AddComponent<CombatPostProcessV61>();
        instance = existing;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
        SetupVolume();
    }

    private void SetupVolume()
    {
        Camera cameraComponent = GetComponent<Camera>();
        if (cameraComponent != null)
        {
            UniversalAdditionalCameraData cameraData = cameraComponent.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
                cameraData = cameraComponent.gameObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
        }

        Transform volumeTransform = transform.Find("V61_CombatVolume");
        if (volumeTransform == null)
        {
            GameObject volumeObject = new GameObject("V61_CombatVolume");
            volumeObject.hideFlags = HideFlags.DontSave;
            volumeObject.transform.SetParent(transform, false);
            volumeTransform = volumeObject.transform;
        }

        Volume volume = volumeTransform.GetComponent<Volume>();
        if (volume == null)
            volume = volumeTransform.gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 900f;
        volume.weight = 1f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.HideAndDontSave;
        volume.profile = profile;

        bloom = profile.Add<Bloom>(true);
        bloom.active = true;
        bloom.threshold.Override(1.08f);
        bloom.intensity.Override(baseBloom);
        bloom.scatter.Override(0.52f);
        bloom.highQualityFiltering.Override(false);

        colorAdjustments = profile.Add<ColorAdjustments>(true);
        colorAdjustments.active = true;
        colorAdjustments.postExposure.Override(0f);
        colorAdjustments.contrast.Override(9f);
        colorAdjustments.saturation.Override(-4f);
        colorAdjustments.colorFilter.Override(new Color(0.93f, 0.97f, 1f, 1f));

        vignette = profile.Add<Vignette>(true);
        vignette.active = true;
        vignette.color.Override(new Color(0.015f, 0.025f, 0.065f, 1f));
        vignette.intensity.Override(0.12f);
        vignette.smoothness.Override(0.42f);

        chromaticAberration = profile.Add<ChromaticAberration>(true);
        chromaticAberration.active = true;
        chromaticAberration.intensity.Override(0f);
    }

    private void Update()
    {
        if (bloom == null || colorAdjustments == null || chromaticAberration == null)
            return;

        UpdateLowHealthVignette();

        if (pulseDuration <= 0f || pulseElapsed >= pulseDuration)
        {
            bloom.intensity.value = Mathf.MoveTowards(bloom.intensity.value, baseBloom, Time.unscaledDeltaTime * 3.2f);
            chromaticAberration.intensity.value = Mathf.MoveTowards(chromaticAberration.intensity.value, 0f, Time.unscaledDeltaTime * 1.8f);
            colorAdjustments.postExposure.value = Mathf.MoveTowards(colorAdjustments.postExposure.value, 0f, Time.unscaledDeltaTime * 2.2f);
            return;
        }

        pulseElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(pulseElapsed / pulseDuration);
        float envelope = 1f - t;
        envelope *= envelope;
        bloom.intensity.value = baseBloom + pulseBloom * envelope;
        chromaticAberration.intensity.value = pulseChroma * envelope;
        colorAdjustments.postExposure.value = pulseExposure * envelope;
    }


    private void UpdateLowHealthVignette()
    {
        if (vignette == null)
            return;

        if (playerHealth == null)
            playerHealth = Object.FindFirstObjectByType<PlayerHealth>();

        float healthRatio = 1f;
        if (playerHealth != null && playerHealth.maxHealth > 0)
            healthRatio = Mathf.Clamp01(playerHealth.currentHealth / (float)playerHealth.maxHealth);

        float lowHealth = Mathf.InverseLerp(0.42f, 0.08f, healthRatio);
        float pulse = lowHealth > 0f ? (Mathf.Sin(Time.unscaledTime * 5.4f) * 0.5f + 0.5f) * lowHealth : 0f;
        float targetIntensity = 0.12f + lowHealth * 0.13f + pulse * 0.035f;
        vignette.intensity.value = Mathf.MoveTowards(vignette.intensity.value, targetIntensity, Time.unscaledDeltaTime * 0.8f);
        vignette.color.value = Color.Lerp(new Color(0.015f, 0.025f, 0.065f, 1f), new Color(0.32f, 0.015f, 0.025f, 1f), lowHealth);
    }

    public static void PulseFire(WeaponDefinition weapon)
    {
        // v6.2: 원거리 발사로 전체 화면의 노출/비네트/색수차를 바꾸지 않는다.
        // 발사 피드백은 총구, 탄환, 탄피 같은 국소 효과만 사용한다.
    }

    public static void PulseImpact(EnemyHitKind hitKind, bool lethal)
    {
        if (instance == null)
            return;

        if (lethal)
            instance.BeginPulse(0.16f, 0.025f, 0.08f, 0.070f);
        else if (hitKind == EnemyHitKind.Melee)
            instance.BeginPulse(0.10f, 0.012f, 0.05f, 0.045f);
    }


    public static void PulsePlayerDamage()
    {
        if (instance != null)
            instance.BeginPulse(0.18f, 0.065f, 0.08f, 0.12f);
    }


    public static void PulseShotgunClose()
    {
        if (instance != null)
            instance.BeginPulse(0.18f, 0.020f, 0.055f, 0.075f);
    }

    public static void PulseHeal()
    {
        if (instance != null)
            instance.BeginPulse(0.12f, 0.008f, 0.035f, 0.12f);
    }

    private void BeginPulse(float bloomBoost, float chroma, float exposure, float duration)
    {
        pulseBloom = Mathf.Max(pulseBloom, bloomBoost);
        pulseChroma = Mathf.Max(pulseChroma, chroma);
        pulseExposure = Mathf.Max(pulseExposure, exposure);
        pulseDuration = Mathf.Max(0.02f, duration);
        pulseElapsed = 0f;
    }
}
