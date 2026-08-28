using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PortalRoomShopTrigger : MonoBehaviour
{
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private bool openOnlyOnce = true;
    private bool hasOpened;

    private void Awake()
    {
        if (shopManager == null) shopManager = FindAnyObjectByType<ShopManager>();
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if ((openOnlyOnce && hasOpened) || !IsPlayer(other)) return;
        if (shopManager == null) shopManager = ShopManager.Instance != null ? ShopManager.Instance : FindAnyObjectByType<ShopManager>();
        if (shopManager == null) return;
        hasOpened = true;
        shopManager.OpenShop();
    }

    private bool IsPlayer(Collider2D other)
    {
        return other != null && (other.CompareTag("Player") || other.GetComponentInParent<PlayerHealth>() != null);
    }
}
