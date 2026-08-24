using UnityEngine;

public class PortalRoomShopTrigger : MonoBehaviour
{
    [Header("Shop")]
    [SerializeField] private ShopManager shopManager;

    [Header("Option")]
    [SerializeField] private bool openOnlyOnce = true;

    private bool hasOpened;

    private void Awake()
    {
        if (shopManager == null)
        {
            shopManager = FindFirstObjectByType<ShopManager>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("PortalRoomShopTrigger 감지됨: " + other.name);

        if (openOnlyOnce && hasOpened)
        {
            return;
        }

        if (!IsPlayer(other))
        {
            Debug.Log("감지됐지만 Player가 아님: " + other.name);
            return;
        }

        hasOpened = true;

        if (shopManager != null)
        {
            Debug.Log("상점 열기 실행");
            shopManager.OpenShop();
        }
        else
        {
            Debug.LogWarning("PortalRoomShopTrigger: ShopManager가 연결되지 않았습니다.");
        }
    }

    private bool IsPlayer(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            return true;
        }

        if (other.GetComponentInParent<PlayerHealth>() != null)
        {
            return true;
        }

        if (other.GetComponentInParent<PlayerMovement>() != null)
        {
            return true;
        }

        return false;
    }
}