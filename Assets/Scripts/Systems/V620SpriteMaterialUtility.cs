using UnityEngine;

/// <summary>
/// v6.2 공용 2D 렌더링 안전장치.
/// URP 2D에서 지원되지 않는 재질 때문에 Sprite가 분홍색으로 출력되는 경우를 막고,
/// 적 본체·공격 경고·투사체·파편이 하나의 지원되는 Unlit 재질을 공유하게 합니다.
/// </summary>
public static class V620SpriteMaterialUtility
{
    private static Material spriteMaterial;
    private static Material trailMaterial;
    private static bool spriteLookupFinished;
    private static bool trailLookupFinished;

    public static void Apply(SpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        Material material = GetSpriteMaterial();
        renderer.sharedMaterial = material;
        renderer.maskInteraction = SpriteMaskInteraction.None;
    }

    public static Material GetSpriteMaterial()
    {
        if (spriteLookupFinished)
            return spriteMaterial;

        spriteLookupFinished = true;
        Shader shader = FindSupportedShader(
            "Universal Render Pipeline/2D/Sprite-Unlit-Default",
            "Sprites/Default",
            "Universal Render Pipeline/Unlit");

        if (shader == null)
            return null;

        spriteMaterial = new Material(shader)
        {
            name = "IA_V620_SafeSpriteUnlit",
            hideFlags = HideFlags.HideAndDontSave
        };
        return spriteMaterial;
    }

    public static Material GetTrailMaterial()
    {
        if (trailLookupFinished)
            return trailMaterial;

        trailLookupFinished = true;
        Shader shader = FindSupportedShader(
            "Sprites/Default",
            "Universal Render Pipeline/2D/Sprite-Unlit-Default",
            "Universal Render Pipeline/Unlit");

        if (shader == null)
            return null;

        trailMaterial = new Material(shader)
        {
            name = "IA_V620_SafeTrailUnlit",
            hideFlags = HideFlags.HideAndDontSave
        };
        return trailMaterial;
    }

    private static Shader FindSupportedShader(params string[] candidates)
    {
        for (int i = 0; i < candidates.Length; i++)
        {
            Shader shader = Shader.Find(candidates[i]);
            if (shader != null && shader.isSupported)
                return shader;
        }

        Debug.LogWarning("[v6.2 Rendering] 지원되는 Sprite/Unlit Shader를 찾지 못했습니다. Unity 기본 재질을 사용합니다.");
        return null;
    }
}
