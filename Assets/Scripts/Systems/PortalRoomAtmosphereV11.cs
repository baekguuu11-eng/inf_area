using UnityEngine;

/// <summary>V11 포탈룸의 저비용 분위기 연출. 상점 로직은 건드리지 않는다.</summary>
[DisallowMultipleComponent]
public sealed class PortalRoomAtmosphereV11 : MonoBehaviour
{
    private SpriteRenderer glow;
    private Vector3 baseScale;
    private float clock;

    public static void Ensure(RoomController room)
    {
        if (room == null || room.GetComponent<PortalRoomAtmosphereV11>() != null) return;
        room.gameObject.AddComponent<PortalRoomAtmosphereV11>();
    }

    private void Start()
    {
        PortalTrigger portal = GetComponentInChildren<PortalTrigger>(true);
        if (portal == null) return;
        SpriteRenderer source = portal.GetComponent<SpriteRenderer>();
        if (source == null) source = portal.GetComponentInChildren<SpriteRenderer>(true);

        GameObject fx = new GameObject("V11_PortalRoomPulse");
        fx.transform.SetParent(portal.transform, false);
        fx.transform.localPosition = Vector3.zero;
        glow = fx.AddComponent<SpriteRenderer>();
        glow.sprite = source != null && source.sprite != null ? source.sprite : RuntimePixelSpriteFactory.GetPortalSprite();
        glow.sortingOrder = source != null ? source.sortingOrder - 1 : 15;
        glow.color = new Color(0.42f, 0.20f, 1f, 0.13f);
        V620SpriteMaterialUtility.Apply(glow);
        baseScale = Vector3.one * 1.16f;
        fx.transform.localScale = baseScale;
    }

    private void Update()
    {
        if (glow == null) return;
        clock += Time.unscaledDeltaTime;
        float pulse = Mathf.Sin(clock * 2.6f) * 0.5f + 0.5f;
        Color c = glow.color;
        c.a = Mathf.Lerp(0.075f, 0.20f, pulse);
        glow.color = c;
        glow.transform.localScale = baseScale * Mathf.Lerp(0.98f, 1.08f, pulse);
    }
}
