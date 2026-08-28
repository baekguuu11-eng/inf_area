using System.Collections.Generic;
using UnityEngine;

public static class AmmoRoomRegistry
{
    private static readonly Dictionary<RoomController, HashSet<AmmoPickup>> ByRoom = new Dictionary<RoomController, HashSet<AmmoPickup>>();

    public static void Register(AmmoPickup pickup, RoomController room)
    {
        if (pickup == null || room == null) return;
        HashSet<AmmoPickup> set;
        if (!ByRoom.TryGetValue(room, out set))
        {
            set = new HashSet<AmmoPickup>();
            ByRoom[room] = set;
        }
        set.Add(pickup);
    }

    public static void Unregister(AmmoPickup pickup, RoomController room)
    {
        if (pickup == null || room == null) return;
        HashSet<AmmoPickup> set;
        if (!ByRoom.TryGetValue(room, out set)) return;
        set.Remove(pickup);
        if (set.Count == 0) ByRoom.Remove(room);
    }

    public static bool ForceCollectRoom(RoomController room, float duration)
    {
        if (room == null) return false;
        HashSet<AmmoPickup> set;
        if (!ByRoom.TryGetValue(room, out set) || set.Count == 0) return false;
        List<AmmoPickup> snapshot = new List<AmmoPickup>(set);
        bool requested = false;
        for (int i = 0; i < snapshot.Count; i++)
        {
            AmmoPickup pickup = snapshot[i];
            if (pickup == null || pickup.IsCollected) continue;
            pickup.ForceCollect(duration);
            requested = true;
        }
        return requested;
    }

    public static void FinalizeRoomCollection(RoomController room)
    {
        if (room == null) return;
        HashSet<AmmoPickup> set;
        if (!ByRoom.TryGetValue(room, out set)) return;
        List<AmmoPickup> snapshot = new List<AmmoPickup>(set);
        for (int i = 0; i < snapshot.Count; i++)
            if (snapshot[i] != null && !snapshot[i].IsCollected) snapshot[i].CollectImmediately();
        ByRoom.Remove(room);
    }
}
