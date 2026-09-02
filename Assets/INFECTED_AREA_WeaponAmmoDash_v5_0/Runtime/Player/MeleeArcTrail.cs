using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class MeleeArcTrail : MonoBehaviour
{
    private TrailRenderer trail;
    private LineRenderer whipLine;
    private LineRenderer whipTipLine;
    private WeaponDefinition weapon;
    private float alpha = 1f;
    private Color accent = Color.white;
    private Vector3 lastSamplePosition;
    private bool hasLastSample;
    private bool visualActive;
    private float activePlayerHeight = 0.8f;
    private float whipBendSign = 1f;

    public bool IsEmitting => visualActive;

    private void Awake()
    {
        EnsureTrail();
        CancelImmediate();
    }

    public void Begin(WeaponDefinition definition, float playerHeight, int sortingLayerId, int sortingOrder,
        Vector2 gripPosition, Vector2 startPosition)
    {
        EnsureTrail();
        EnsureWhipLine();
        weapon = definition;
        activePlayerHeight = Mathf.Max(0.3f, playerHeight);
        alpha = 1f;
        accent = definition != null ? definition.accentColor : Color.white;
        visualActive = true;

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
        ApplyGradient();

        bool isWhip = definition != null && definition.weaponId == "debug_whip";
        if (isWhip)
        {
            // V9: 실제 채찍 스프라이트가 판정 거리까지 직접 늘어나므로 별도의 보라색 범위선은 사용하지 않는다.
            // 공격 중 화면에 보이는 채찍 그 자체가 곧 실제 공격 길이이다.
            trail.emitting = false;
            if (whipLine != null) whipLine.enabled = false;
            if (whipTipLine != null) whipTipLine.enabled = false;
        }
        else
        {
            trail.AddPosition(startPosition);
            trail.emitting = true;
            if (whipLine != null) whipLine.enabled = false;
            if (whipTipLine != null) whipTipLine.enabled = false;
        }
    }

    public void Sample(Vector2 gripPosition, Vector2 worldTip)
    {
        if (!visualActive) return;

        bool isWhip = weapon != null && weapon.weaponId == "debug_whip";
        if (isWhip)
        {
            transform.position = worldTip;
            if (whipLine != null) whipLine.enabled = false;
            if (whipTipLine != null) whipTipLine.enabled = false;
            lastSamplePosition = worldTip;
            hasLastSample = true;
            return;
        }

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
        ApplyWhipColors();
    }

    public void End()
    {
        visualActive = false;
        if (trail != null)
            trail.emitting = false;
        if (whipLine != null)
            whipLine.enabled = false;
        if (whipTipLine != null)
            whipTipLine.enabled = false;
    }

    public void CancelImmediate()
    {
        EnsureTrail();
        EnsureWhipLine();
        visualActive = false;
        trail.emitting = false;
        trail.Clear();
        hasLastSample = false;
        if (whipLine != null)
            whipLine.enabled = false;
        if (whipTipLine != null)
            whipTipLine.enabled = false;
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

    private void EnsureWhipLine()
    {
        if (whipLine != null && whipTipLine != null) return;

        Material material = V620SpriteMaterialUtility.GetTrailMaterial();

        if (whipLine == null)
        {
            GameObject lineObject = new GameObject("WhipReachVisual");
            lineObject.transform.SetParent(transform, false);
            whipLine = lineObject.AddComponent<LineRenderer>();
            if (material != null)
                whipLine.sharedMaterial = material;
            whipLine.useWorldSpace = true;
            whipLine.alignment = LineAlignment.View;
            whipLine.textureMode = LineTextureMode.Stretch;
            whipLine.positionCount = 14;
            whipLine.numCornerVertices = 1;
            whipLine.numCapVertices = 1;
            whipLine.generateLightingData = false;
            whipLine.shadowCastingMode = ShadowCastingMode.Off;
            whipLine.receiveShadows = false;
            whipLine.enabled = false;
        }

        if (whipTipLine == null)
        {
            GameObject tipObject = new GameObject("WhipTipHighlight");
            tipObject.transform.SetParent(transform, false);
            whipTipLine = tipObject.AddComponent<LineRenderer>();
            if (material != null)
                whipTipLine.sharedMaterial = material;
            whipTipLine.useWorldSpace = true;
            whipTipLine.alignment = LineAlignment.View;
            whipTipLine.textureMode = LineTextureMode.Stretch;
            whipTipLine.positionCount = 3;
            whipTipLine.numCornerVertices = 1;
            whipTipLine.numCapVertices = 1;
            whipTipLine.generateLightingData = false;
            whipTipLine.shadowCastingMode = ShadowCastingMode.Off;
            whipTipLine.receiveShadows = false;
            whipTipLine.enabled = false;
        }
    }

    private void UpdateWhipLine(Vector2 gripPosition, Vector2 tipPosition, float playerHeight,
        int sortingLayerId, int sortingOrder)
    {
        if (weapon == null || weapon.weaponId != "debug_whip")
        {
            if (whipLine != null) whipLine.enabled = false;
            if (whipTipLine != null) whipTipLine.enabled = false;
            return;
        }

        EnsureWhipLine();
        if (playerHeight <= 0f)
            playerHeight = 0.8f;

        Vector2 delta = tipPosition - gripPosition;
        float length = delta.magnitude;
        Vector2 forward = length > 0.0001f ? delta / length : Vector2.right;
        Vector2 perpendicular = new Vector2(-forward.y, forward.x) * whipBendSign;

        // 실제 판정 끝점까지 항상 플레이어 손에서 연결된 하나의 채찍으로 표시한다.
        // 가운데만 아주 약하게 휘게 만들어 채찍 느낌은 살리되 별도 반원처럼 분리되지 않게 한다.
        const int pointCount = 14;
        whipLine.enabled = true;
        whipLine.sortingLayerID = sortingLayerId;
        whipLine.sortingOrder = sortingOrder;
        whipLine.positionCount = pointCount;
        float bend = Mathf.Min(length * 0.085f, playerHeight * 0.14f);
        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (pointCount - 1f);
            float curve = Mathf.Sin(t * Mathf.PI) * bend * (0.55f + 0.45f * t);
            Vector2 point = Vector2.Lerp(gripPosition, tipPosition, t) + perpendicular * curve;
            whipLine.SetPosition(i, point);
        }

        float thickness = Mathf.Max(0.018f, playerHeight * weapon.sweepThicknessPlayerRatio * 0.34f);
        whipLine.startWidth = thickness * 0.48f;
        whipLine.endWidth = thickness * 0.80f;

        // 끝 18%를 조금 더 밝고 굵게 표시해 실제 최대 사거리 끝을 읽기 쉽게 한다.
        whipTipLine.enabled = true;
        whipTipLine.sortingLayerID = sortingLayerId;
        whipTipLine.sortingOrder = sortingOrder + 1;
        whipTipLine.startWidth = thickness * 0.70f;
        whipTipLine.endWidth = thickness * 1.12f;
        whipTipLine.SetPosition(0, whipLine.GetPosition(pointCount - 3));
        whipTipLine.SetPosition(1, whipLine.GetPosition(pointCount - 2));
        whipTipLine.SetPosition(2, whipLine.GetPosition(pointCount - 1));
        ApplyWhipColors();
    }

    private void ApplyWhipColors()
    {
        if (weapon == null) return;

        if (whipLine != null && whipLine.enabled)
        {
            Color start = Color.Lerp(weapon.accentColor, Color.white, 0.34f);
            Color end = weapon.accentColor;
            start.a = alpha * 0.86f;
            end.a = alpha;
            whipLine.startColor = start;
            whipLine.endColor = end;
        }

        if (whipTipLine != null && whipTipLine.enabled)
        {
            Color bright = Color.Lerp(weapon.accentColor, Color.white, 0.72f);
            Color end = Color.Lerp(weapon.accentColor, Color.white, 0.28f);
            bright.a = alpha;
            end.a = alpha * 0.96f;
            whipTipLine.startColor = end;
            whipTipLine.endColor = bright;
        }
    }

    private void ApplyGradient()
    {
        if (trail == null) return;

        Color bright = Color.Lerp(accent, Color.white, 0.38f);
        bright.a = alpha;
        Color middle = accent;
        middle.a = alpha * 0.78f;

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
