using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class PortalTrigger : MonoBehaviour
{
    [SerializeField] private float localCooldown = 0.5f;
    private float nextUseTime;

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null && renderer.sprite == null)
        {
            renderer.sprite = RuntimePixelSpriteFactory.GetPortalSprite();
            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 2);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.unscaledTime < nextUseTime) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;
        MapManager manager = FindAnyObjectByType<MapManager>();
        if (manager == null) return;
        nextUseTime = Time.unscaledTime + Mathf.Max(0.05f, localCooldown);
        manager.TryUsePortal();
    }
}
