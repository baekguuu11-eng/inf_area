using UnityEngine;

/// <summary>
/// Telegraph animation that never changes the actual attack geometry.
/// Fill/ring intensity rises with urgency; an optional Countdown child contracts toward impact.
/// </summary>
[DisallowMultipleComponent]
public sealed class JHLTelegraphVisual : MonoBehaviour
{
    private SpriteRenderer[] renderers;
    private Color[] baseColors;
    private Transform countdown;
    private Vector3 countdownBaseScale;
    private float duration;
    private float elapsed;
    private float pulseSpeed;
    private bool configured;

    public float Progress => duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

    public void Configure(float lifetime, float pulseRate = 10f, float unusedScaleAmount = 0f)
    {
        duration = Mathf.Max(0.05f, lifetime);
        pulseSpeed = Mathf.Max(1f, pulseRate);
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = renderers[i] != null ? renderers[i].color : Color.white;
        countdown = transform.Find("Countdown");
        if (countdown != null) countdownBaseScale = countdown.localScale;
        elapsed = 0f;
        configured = true;
    }

    private void Update()
    {
        if (!configured) return;

        elapsed += Time.deltaTime;
        float t = Progress;
        float urgency = t * t * (3f - 2f * t);
        float pulseRate = Mathf.Lerp(pulseSpeed * 0.45f, pulseSpeed * 1.65f, urgency);
        float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * pulseRate);
        float finalFlash = t > 0.84f ? (0.5f + 0.5f * Mathf.Sign(Mathf.Sin(elapsed * 42f))) : 1f;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null) continue;
            Color c = baseColors[i];
            bool isFill = sr.gameObject.name.Contains("Fill");
            bool isCountdown = sr.gameObject.name.Contains("Countdown");
            float alphaMultiplier;
            if (isFill) alphaMultiplier = Mathf.Lerp(0.62f, 1.18f, urgency);
            else if (isCountdown) alphaMultiplier = Mathf.Lerp(0.55f, 1.25f, urgency);
            else alphaMultiplier = Mathf.Lerp(0.78f, 1.28f, urgency);
            alphaMultiplier *= Mathf.Lerp(0.82f, 1.12f, pulse) * finalFlash;
            c.a = Mathf.Clamp01(baseColors[i].a * alphaMultiplier);
            sr.color = c;
        }

        if (countdown != null)
        {
            // Converging marker: 1.18 -> 0.18. Parent scale remains untouched,
            // so rendered warning and damage geometry stay exactly aligned.
            float size = Mathf.Lerp(1.18f, 0.18f, Mathf.Pow(t, 0.82f));
            countdown.localScale = Vector3.Scale(countdownBaseScale, new Vector3(size, size, 1f));
        }
    }
}
