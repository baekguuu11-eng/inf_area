using System.Collections;
using UnityEngine;

public sealed class BeamVisual : MonoBehaviour
{
    private LineRenderer glow;
    private LineRenderer core;
    private float duration;
    private static Material sharedMaterial;

    public static BeamVisual Spawn(Vector3 start, Vector3 end, WeaponDefinition weapon)
    {
        GameObject obj = new GameObject("LaserBeamVisual");
        BeamVisual visual = obj.AddComponent<BeamVisual>();
        visual.Build(start, end, weapon);
        return visual;
    }

    private void Build(Vector3 start, Vector3 end, WeaponDefinition weapon)
    {
        duration = weapon != null ? Mathf.Max(0.02f, weapon.beamDuration) : 0.08f;
        Color accent = weapon != null ? weapon.accentColor : new Color(0.15f, 0.9f, 1f, 1f);

        glow = CreateLine("Glow", weapon != null ? weapon.beamGlowWidth : 0.11f, new Color(accent.r, accent.g, accent.b, 0.48f), 39);
        core = CreateLine("Core", weapon != null ? weapon.beamCoreWidth : 0.03f, new Color(0.96f, 1f, 1f, 1f), 40);
        SetPositions(glow, start, end);
        SetPositions(core, start, end);
        StartCoroutine(FadeRoutine());
    }

    private LineRenderer CreateLine(string objectName, float width, Color color, int order)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        LineRenderer line = child.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = width;
        line.endWidth = width * 0.72f;
        line.numCapVertices = 4;
        line.material = GetSharedMaterial();
        line.startColor = color;
        line.endColor = color;
        line.sortingOrder = order;
        return line;
    }

    private static Material GetSharedMaterial()
    {
        if (sharedMaterial == null)
        {
            sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            sharedMaterial.name = "INFECTED_AREA_Beam_Runtime";
        }
        return sharedMaterial;
    }

    private static void SetPositions(LineRenderer line, Vector3 start, Vector3 end)
    {
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private IEnumerator FadeRoutine()
    {
        float elapsed = 0f;
        Color glowStart = glow.startColor;
        Color coreStart = core.startColor;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = 1f - t * t;
            SetAlpha(glow, glowStart, alpha);
            SetAlpha(core, coreStart, alpha);
            yield return null;
        }
        Destroy(gameObject);
    }

    private static void SetAlpha(LineRenderer line, Color start, float multiplier)
    {
        Color color = start;
        color.a *= multiplier;
        line.startColor = color;
        line.endColor = color;
    }
}
