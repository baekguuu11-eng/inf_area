using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class JHLCameraFX : MonoBehaviour
{
    private Camera targetCamera;
    private Volume volume;
    private Bloom bloom;
    private Vignette vignette;
    private ChromaticAberration chroma;
    private ColorAdjustments colorAdjustments;
    private LensDistortion lens;
    private FilmGrain grain;

    private bool activeBoss;
    private int phase = 1;
    private float baseOrthoSize;
    private float zoomVelocity;
    private float currentZoomOffset;
    private float framingTarget;
    private float pressure;
    private float charge;

    private float bloomPulse;
    private float chromaPulse;
    private float exposurePulse;
    private float lensPulse;
    private float vignettePulse;
    private float zoomImpulse;
    private float phasePulse;
    private Behaviour pixelPerfectCamera;

    public float Pressure => pressure;
    public float Framing => framingTarget;
    public float Charge => charge;

    public static JHLCameraFX CreateOrGet()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null) return null;
        JHLCameraFX fx = cam.GetComponent<JHLCameraFX>();
        if (fx == null) fx = cam.gameObject.AddComponent<JHLCameraFX>();
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

            Behaviour[] behaviours = targetCamera.GetComponents<Behaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour candidate = behaviours[i];
                if (candidate != null && candidate.GetType().Name == "PixelPerfectCamera")
                {
                    pixelPerfectCamera = candidate;
                    break;
                }
            }
        }
        SetupVolume();
    }

    private void SetupVolume()
    {
        Transform child = transform.Find("JHL_BossVolume");
        if (child == null)
        {
            GameObject go = new GameObject("JHL_BossVolume");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);
            child = go.transform;
        }

        volume = child.GetComponent<Volume>();
        if (volume == null) volume = child.gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 940f;
        volume.weight = 0f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.HideAndDontSave;
        volume.profile = profile;

        bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0f);
        bloom.scatter.Override(0.52f);
        bloom.highQualityFiltering.Override(false);

        vignette = profile.Add<Vignette>(true);
        vignette.color.Override(new Color(0.035f, 0.005f, 0.04f, 1f));
        vignette.intensity.Override(0f);
        vignette.smoothness.Override(0.48f);

        chroma = profile.Add<ChromaticAberration>(true);
        chroma.intensity.Override(0f);

        colorAdjustments = profile.Add<ColorAdjustments>(true);
        colorAdjustments.postExposure.Override(0f);
        colorAdjustments.contrast.Override(0f);
        colorAdjustments.saturation.Override(0f);
        colorAdjustments.colorFilter.Override(Color.white);

        lens = profile.Add<LensDistortion>(true);
        lens.intensity.Override(0f);
        lens.scale.Override(1f);

        grain = profile.Add<FilmGrain>(true);
        grain.intensity.Override(0f);
        grain.response.Override(0.72f);
    }

    public void BeginBoss(int currentPhase)
    {
        if (!activeBoss && targetCamera != null)
            baseOrthoSize = targetCamera.orthographicSize;
        activeBoss = true;
        phase = Mathf.Clamp(currentPhase, 1, 3);
    }

    public void SetPhase(int value)
    {
        phase = Mathf.Clamp(value, 1, 3);
        activeBoss = true;
        PulseTransition(phase);
    }

    public void EndBoss()
    {
        activeBoss = false;
        pressure = 0f;
        charge = 0f;
        framingTarget = 0f;
        bloomPulse = 0f;
        chromaPulse = 0f;
        exposurePulse = 0f;
        lensPulse = 0f;
        vignettePulse = 0f;
        zoomImpulse = 0f;
        phasePulse = 0f;
    }

    public void SetPressure(float normalized)
    {
        pressure = Mathf.Clamp01(normalized);
    }

    public void SetFraming(float normalizedZoomOut)
    {
        framingTarget = Mathf.Clamp(normalizedZoomOut, -0.035f, 0.10f);
    }

    public void SetCharge(float normalized)
    {
        charge = Mathf.Clamp01(normalized);
    }

    public void PulseLight(float strength = 1f)
    {
        float s = Mathf.Clamp01(strength);
        bloomPulse = Mathf.Max(bloomPulse, 0.16f * s);
        chromaPulse = Mathf.Max(chromaPulse, 0.010f * s);
        exposurePulse = Mathf.Max(exposurePulse, 0.025f * s);
        zoomImpulse = Mathf.Max(zoomImpulse, 0.003f * s);
    }

    public void PulseHeavy(float strength = 1f)
    {
        float s = Mathf.Clamp01(strength);
        bloomPulse = Mathf.Max(bloomPulse, 0.48f * s);
        chromaPulse = Mathf.Max(chromaPulse, 0.065f * s);
        exposurePulse = Mathf.Max(exposurePulse, 0.11f * s);
        lensPulse = Mathf.Max(lensPulse, 0.038f * s);
        vignettePulse = Mathf.Max(vignettePulse, 0.040f * s);
        zoomImpulse = Mathf.Max(zoomImpulse, 0.013f * s);
    }

    public void PulseTransition(int targetPhase)
    {
        phase = Mathf.Clamp(targetPhase, 1, 3);
        float power = targetPhase >= 3 ? 1f : 0.72f;
        bloomPulse = Mathf.Max(bloomPulse, 0.62f * power);
        chromaPulse = Mathf.Max(chromaPulse, 0.085f * power);
        exposurePulse = Mathf.Max(exposurePulse, 0.13f * power);
        lensPulse = Mathf.Max(lensPulse, 0.050f * power);
        vignettePulse = Mathf.Max(vignettePulse, 0.046f * power);
        zoomImpulse = Mathf.Max(zoomImpulse, 0.016f * power);
        phasePulse = Mathf.Max(phasePulse, power);
    }

    private void Update()
    {
        if (volume == null || bloom == null || vignette == null || chroma == null || colorAdjustments == null)
            return;

        float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        float eventEnergy = Mathf.Clamp01(
            bloomPulse * 0.70f +
            chromaPulse * 1.6f +
            pressure * 0.35f +
            charge * 0.30f +
            phasePulse * 0.45f);

        // V4: no permanent post-process wash. Effects appear only when something actually happens.
        float desiredWeight = activeBoss ? Mathf.Clamp01(eventEnergy * 0.82f) : 0f;
        volume.weight = Mathf.MoveTowards(volume.weight, desiredWeight, dt * (desiredWeight > volume.weight ? 5.5f : 3.0f));

        float baseBloom = 0f;
        float baseVignette = 0f;
        float baseContrast = 0f;
        float baseGrain = 0f;

        bloom.intensity.value = baseBloom + bloomPulse + pressure * 0.08f + charge * 0.14f;
        vignette.intensity.value = baseVignette + vignettePulse + pressure * 0.045f;
        chroma.intensity.value = Mathf.Clamp01(chromaPulse + pressure * 0.008f + phasePulse * 0.010f);
        colorAdjustments.postExposure.value = exposurePulse + charge * 0.020f;
        colorAdjustments.contrast.value = baseContrast + pressure * 0.8f;
        colorAdjustments.saturation.value = -pressure * 0.5f;
        colorAdjustments.colorFilter.value = Color.white;
        if (lens != null) lens.intensity.value = -Mathf.Clamp(lensPulse + pressure * 0.006f, 0f, 0.07f);
        if (grain != null) grain.intensity.value = baseGrain + pressure * 0.003f + phasePulse * 0.004f;

        bloomPulse = Mathf.MoveTowards(bloomPulse, 0f, dt * 4.7f);
        chromaPulse = Mathf.MoveTowards(chromaPulse, 0f, dt * 3.8f);
        exposurePulse = Mathf.MoveTowards(exposurePulse, 0f, dt * 4.8f);
        lensPulse = Mathf.MoveTowards(lensPulse, 0f, dt * 4.1f);
        vignettePulse = Mathf.MoveTowards(vignettePulse, 0f, dt * 3.5f);
        zoomImpulse = Mathf.MoveTowards(zoomImpulse, 0f, dt * 0.20f);
        phasePulse = Mathf.MoveTowards(phasePulse, 0f, dt * 1.8f);

        if (targetCamera != null && targetCamera.orthographic)
        {
            // PixelPerfectCamera owns orthographicSize once the intro director restores it.
            // JHL post FX/camera shake still work, but we no longer fight the pixel-perfect
            // component every frame and cause tiny zoom snaps/jitter.
            bool pixelPerfectOwnsZoom = pixelPerfectCamera != null && pixelPerfectCamera.enabled;
            if (!pixelPerfectOwnsZoom)
            {
                float targetOffset = activeBoss
                    ? framingTarget + zoomImpulse - pressure * 0.010f - charge * 0.005f
                    : 0f;
                currentZoomOffset = Mathf.SmoothDamp(currentZoomOffset, targetOffset, ref zoomVelocity, activeBoss ? 0.16f : 0.18f, 1.2f, dt);
                targetCamera.orthographicSize = baseOrthoSize * (1f + currentZoomOffset);

                if (!activeBoss && Mathf.Abs(currentZoomOffset) < 0.0005f)
                {
                    currentZoomOffset = 0f;
                    zoomVelocity = 0f;
                    targetCamera.orthographicSize = baseOrthoSize;
                }
            }
            else
            {
                currentZoomOffset = 0f;
                zoomVelocity = 0f;
            }
        }
    }
}
