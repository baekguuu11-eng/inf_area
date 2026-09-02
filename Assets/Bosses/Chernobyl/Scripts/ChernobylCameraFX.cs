using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Chernobyl 전용 카메라/URP 후처리 레이어.
/// 기존 CombatPostProcessV61보다 높은 우선순위의 런타임 Volume을 사용해
/// 보스전 동안만 녹색 계열 Bloom/Vignette/Color/FilmGrain과 순간 Chroma/Lens 왜곡을 추가한다.
/// 카메라 이동은 기존 CameraFeedbackController와 충돌하지 않도록 Orthographic Size만 미세 펄스한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ChernobylCameraFX : MonoBehaviour
{
    private static ChernobylCameraFX instance;

    private Camera targetCamera;
    private Volume volume;
    private Bloom bloom;
    private Vignette vignette;
    private ChromaticAberration chroma;
    private ColorAdjustments colorAdjustments;
    private LensDistortion lensDistortion;
    private FilmGrain filmGrain;

    private bool bossActive;
    private int phase = 1;
    private float baseOrthoSize;
    private float zoomPulse;
    private float bloomPulse;
    private float chromaPulse;
    private float exposurePulse;
    private float lensPulse;
    private float vignettePulse;
    private float pulseDecay = 5.8f;
    private float breathingTime;

    public static ChernobylCameraFX CreateOrGet()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null) return null;

        ChernobylCameraFX fx = cam.GetComponent<ChernobylCameraFX>();
        if (fx == null) fx = cam.gameObject.AddComponent<ChernobylCameraFX>();
        instance = fx;
        return fx;
    }

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera != null)
        {
            baseOrthoSize = targetCamera.orthographicSize;
            UniversalAdditionalCameraData data = targetCamera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = targetCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
        }
        SetupVolume();
    }

    private void SetupVolume()
    {
        Transform child = transform.Find("CHN_BossVolume");
        if (child == null)
        {
            GameObject go = new GameObject("CHN_BossVolume");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);
            child = go.transform;
        }

        volume = child.GetComponent<Volume>();
        if (volume == null) volume = child.gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 925f; // 기존 CombatPostProcessV61(900)보다 높게, 체르노빌에서만 weight 사용.
        volume.weight = 0f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.HideAndDontSave;
        volume.profile = profile;

        bloom = profile.Add<Bloom>(true);
        bloom.active = true;
        bloom.threshold.Override(0.96f);
        bloom.intensity.Override(0.34f);
        bloom.scatter.Override(0.56f);
        bloom.highQualityFiltering.Override(false);

        vignette = profile.Add<Vignette>(true);
        vignette.active = true;
        vignette.color.Override(new Color(0.015f, 0.075f, 0.025f, 1f));
        vignette.intensity.Override(0.10f);
        vignette.smoothness.Override(0.48f);

        chroma = profile.Add<ChromaticAberration>(true);
        chroma.active = true;
        chroma.intensity.Override(0f);

        colorAdjustments = profile.Add<ColorAdjustments>(true);
        colorAdjustments.active = true;
        colorAdjustments.postExposure.Override(0f);
        colorAdjustments.contrast.Override(10f);
        colorAdjustments.saturation.Override(-2f);
        colorAdjustments.colorFilter.Override(new Color(0.94f, 1f, 0.92f, 1f));

        lensDistortion = profile.Add<LensDistortion>(true);
        lensDistortion.active = true;
        lensDistortion.intensity.Override(0f);
        lensDistortion.scale.Override(1f);

        filmGrain = profile.Add<FilmGrain>(true);
        filmGrain.active = true;
        filmGrain.intensity.Override(0.025f);
        filmGrain.response.Override(0.72f);
    }

    public void BeginBoss(int currentPhase)
    {
        bossActive = true;
        phase = Mathf.Clamp(currentPhase, 1, 3);
        if (targetCamera != null) baseOrthoSize = targetCamera.orthographicSize;
        if (volume != null) volume.weight = Mathf.Max(volume.weight, 0.01f);
    }

    public void SetPhase(int currentPhase)
    {
        phase = Mathf.Clamp(currentPhase, 1, 3);
        bossActive = true;
        PulseTransition(phase);
    }

    public void EndBoss()
    {
        bossActive = false;
        zoomPulse = bloomPulse = chromaPulse = exposurePulse = lensPulse = vignettePulse = 0f;
    }

    public void PulseExplosion(float strength)
    {
        float s = Mathf.Clamp01(strength);
        bloomPulse = Mathf.Max(bloomPulse, 0.38f * s);
        chromaPulse = Mathf.Max(chromaPulse, 0.10f * s);
        exposurePulse = Mathf.Max(exposurePulse, 0.14f * s);
        lensPulse = Mathf.Max(lensPulse, 0.055f * s);
        vignettePulse = Mathf.Max(vignettePulse, 0.045f * s);
        zoomPulse = Mathf.Max(zoomPulse, 0.025f * s);
    }

    public void PulseSweep(float strength)
    {
        float s = Mathf.Clamp01(strength);
        bloomPulse = Mathf.Max(bloomPulse, 0.20f * s);
        chromaPulse = Mathf.Max(chromaPulse, 0.045f * s);
        lensPulse = Mathf.Max(lensPulse, 0.022f * s);
        zoomPulse = Mathf.Max(zoomPulse, 0.012f * s);
    }

    public void PulseCounter(float strength)
    {
        float s = Mathf.Clamp01(strength);
        bloomPulse = Mathf.Max(bloomPulse, 0.46f * s);
        chromaPulse = Mathf.Max(chromaPulse, 0.14f * s);
        exposurePulse = Mathf.Max(exposurePulse, 0.18f * s);
        lensPulse = Mathf.Max(lensPulse, 0.075f * s);
        vignettePulse = Mathf.Max(vignettePulse, 0.065f * s);
        zoomPulse = Mathf.Max(zoomPulse, 0.032f * s);
    }

    public void PulseTransition(int targetPhase)
    {
        phase = Mathf.Clamp(targetPhase, 1, 3);
        bloomPulse = Mathf.Max(bloomPulse, targetPhase >= 3 ? 0.72f : 0.52f);
        chromaPulse = Mathf.Max(chromaPulse, targetPhase >= 3 ? 0.20f : 0.13f);
        exposurePulse = Mathf.Max(exposurePulse, targetPhase >= 3 ? 0.24f : 0.17f);
        lensPulse = Mathf.Max(lensPulse, targetPhase >= 3 ? 0.10f : 0.065f);
        vignettePulse = Mathf.Max(vignettePulse, targetPhase >= 3 ? 0.085f : 0.055f);
        zoomPulse = Mathf.Max(zoomPulse, targetPhase >= 3 ? 0.040f : 0.028f);
    }

    private void Update()
    {
        if (volume == null || bloom == null || vignette == null || colorAdjustments == null || chroma == null)
            return;

        float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        float targetWeight = bossActive ? 1f : 0f;
        volume.weight = Mathf.MoveTowards(volume.weight, targetWeight, dt * (bossActive ? 2.8f : 1.8f));

        breathingTime += dt;
        float meltdownBreath = bossActive && phase >= 3 ? (Mathf.Sin(breathingTime * 4.6f) * 0.5f + 0.5f) : 0f;

        float baseBloom = phase == 1 ? 0.34f : phase == 2 ? 0.48f : 0.64f;
        float baseVignette = phase == 1 ? 0.10f : phase == 2 ? 0.135f : 0.18f;
        float baseContrast = phase == 1 ? 10f : phase == 2 ? 14f : 18f;
        float baseSaturation = phase == 1 ? -2f : phase == 2 ? 0f : 3f;
        float baseGrain = phase == 1 ? 0.025f : phase == 2 ? 0.040f : 0.060f;

        bloom.intensity.value = baseBloom + bloomPulse + meltdownBreath * 0.08f;
        vignette.intensity.value = baseVignette + vignettePulse + meltdownBreath * 0.018f;
        vignette.color.value = phase >= 3 ? new Color(0.055f, 0.12f, 0.018f, 1f) : new Color(0.015f, 0.075f, 0.025f, 1f);
        chroma.intensity.value = Mathf.Clamp01(chromaPulse + meltdownBreath * (phase >= 3 ? 0.018f : 0f));
        colorAdjustments.postExposure.value = exposurePulse;
        colorAdjustments.contrast.value = baseContrast;
        colorAdjustments.saturation.value = baseSaturation;
        colorAdjustments.colorFilter.value = phase == 1
            ? new Color(0.94f, 1f, 0.92f, 1f)
            : phase == 2 ? new Color(0.98f, 1f, 0.87f, 1f) : new Color(1f, 0.98f, 0.78f, 1f);
        if (lensDistortion != null)
            lensDistortion.intensity.value = -Mathf.Clamp(lensPulse + meltdownBreath * 0.012f, 0f, 0.16f);
        if (filmGrain != null)
            filmGrain.intensity.value = baseGrain + meltdownBreath * 0.012f;

        bloomPulse = Mathf.MoveTowards(bloomPulse, 0f, dt * pulseDecay);
        chromaPulse = Mathf.MoveTowards(chromaPulse, 0f, dt * pulseDecay * 0.75f);
        exposurePulse = Mathf.MoveTowards(exposurePulse, 0f, dt * pulseDecay * 0.85f);
        lensPulse = Mathf.MoveTowards(lensPulse, 0f, dt * pulseDecay * 0.90f);
        vignettePulse = Mathf.MoveTowards(vignettePulse, 0f, dt * pulseDecay * 0.75f);
        zoomPulse = Mathf.MoveTowards(zoomPulse, 0f, dt * 0.16f);

        // 픽셀 아트가 무너지지 않는 범위에서 1~4% 정도의 짧은 줌 펀치만 사용한다.
        if (targetCamera != null && targetCamera.orthographic && bossActive)
        {
            float desired = baseOrthoSize * (1f + zoomPulse + meltdownBreath * (phase >= 3 ? 0.0035f : 0f));
            targetCamera.orthographicSize = Mathf.Lerp(targetCamera.orthographicSize, desired, 1f - Mathf.Exp(-dt * 16f));
        }
        else if (targetCamera != null && targetCamera.orthographic && !bossActive && baseOrthoSize > 0.01f)
        {
            targetCamera.orthographicSize = Mathf.Lerp(targetCamera.orthographicSize, baseOrthoSize, 1f - Mathf.Exp(-dt * 10f));
        }
    }
}
