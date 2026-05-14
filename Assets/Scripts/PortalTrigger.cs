using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class PortalTrigger : MonoBehaviour
{
    private void Awake()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null)
            return;

        MapManager manager = FindAnyObjectByType<MapManager>();
        if (manager == null)
            return;

        manager.TryUsePortal();
    }
}