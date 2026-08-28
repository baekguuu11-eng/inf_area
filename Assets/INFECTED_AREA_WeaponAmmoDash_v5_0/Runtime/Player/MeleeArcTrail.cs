using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class MeleeArcTrail : MonoBehaviour
{
    private TrailRenderer trail;
    private WeaponDefinition weapon;
    private float alpha = 1f;
    private Color accent = Color.white;
    private Vector3 lastSamplePosition;
    private bool hasLastSample;
    private static Material sharedMaterial;

    public bool IsEmitting => trail != null && trail.emitting;

    private void Awake()
    {
        EnsureTrail();
        CancelImmediate();
    }

    public void Begin(WeaponDefinition definition, float playerHeight, int sortingLayerId, int sortingOrder, Vector2 startPosition)
    {
        EnsureTrail();
        weapon = definition;
        alpha = 1f;
        accent = definition != null ? definition.accentColor : Color.white;

        float widthRatio = definition != null ? Mathf.Max(0.02f, definition.sweepThicknessPlayerRatio) : 0.1f;
        float widthMultiplier = 1f;
        if (definition != null)
        {
            if (definition.handStyle == WeaponHandStyle.HeavyTwoHand) widthMultiplier = 1.45f;
            else if (definition.handStyle == WeaponHandStyle.WhipGrip) widthMultiplier = 0.62f;
            else if (definition.weaponId == "firewall_sword") widthMultiplier = 1.2f;
        }

        trail.time = definition != null ? Mathf.Max(0.055f, definition.trailDuration * 1.35f) : 0.12f;
        trail.widthMultiplier = Mathf.Max(0.018f, playerHeight * widthRatio * widthMultiplier);
        trail.minVertexDistance = Mathf.Max(0.004f, trail.widthMultiplier * 0.12f);
        trail.sortingLayerID = sortingLayerId;
        trail.sortingOrder = sortingOrder;
        trail.Clear();
        transform.position = startPosition;
        lastSamplePosition = startPosition;
        hasLastSample = true;
        trail.AddPosition(startPosition);
        ApplyGradient();
        trail.emitting = true;
    }

    public void Sample(Vector2 worldTip)
    {
        if (trail == null || !trail.emitting) return;

        Vector3 target = worldTip;
        if (!hasLastSample)
        {
            lastSamplePosition = target;
            hasLastSample = true;
            trail.AddPosition(target);
        }
        else
        {
            float distance = Vector3.Distance(lastSamplePosition, target);
            int subdivisions = Mathf.Clamp(
                Mathf.CeilToInt(distance / Mathf.Max(0.003f, trail.minVertexDistance)),
                1,
                8);

            for (int i = 1; i <= subdivisions; i++)
                trail.AddPosition(Vector3.Lerp(lastSamplePosition, target, i / (float)subdivisions));

            lastSamplePosition = target;
        }

        transform.position = target;
    }

    public void SetAlpha(float value)
    {
        alpha = Mathf.Clamp01(value);
        ApplyGradient();
    }

    public void End()
    {
        if (trail != null)
            trail.emitting = false;
    }

    public void CancelImmediate()
    {
        EnsureTrail();
        trail.emitting = false;
        trail.Clear();
        hasLastSample = false;
    }

    private void EnsureTrail()
    {
        if (trail != null) return;

        trail = GetComponent<TrailRenderer>();
        if (trail == null)
            trail = gameObject.AddComponent<TrailRenderer>();

        Material material = V620SpriteMaterialUtility.GetTrailMaterial();
        if (material != null)
            trail.sharedMaterial = material;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.numCornerVertices = 0;
        trail.numCapVertices = 0;
        trail.generateLightingData = false;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.autodestruct = false;
        trail.emitting = false;
    }

    private void ApplyGradient()
    {
        if (trail == null) return;

        Color bright = Color.Lerp(accent, Color.white, 0.38f);
        bright.a = alpha;
        Color middle = accent;
        middle.a = alpha * 0.78f;
        Color faded = accent;
        faded.a = 0f;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(bright, 0f),
                new GradientColorKey(middle, 0.5f),
                new GradientColorKey(accent, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(alpha * 0.72f, 0.18f),
                new GradientAlphaKey(alpha, 1f)
            });
        trail.colorGradient = gradient;

        AnimationCurve width = new AnimationCurve(
            new Keyframe(0f, 0.08f),
            new Keyframe(0.22f, 0.62f),
            new Keyframe(0.72f, 1f),
            new Keyframe(1f, 0.42f));
        trail.widthCurve = width;
    }


}
