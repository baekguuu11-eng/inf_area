using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks Byte pickups by their owning room so room transitions can collect and clean them safely.
/// </summary>
public static class ByteRoomRegistry
{
    private static readonly Dictionary<RoomController, HashSet<BytePickup>> PickupsByRoom =
        new Dictionary<RoomController, HashSet<BytePickup>>();

    public static void Register(BytePickup pickup, RoomController room)
    {
        if (pickup == null || room == null)
            return;

        CleanupDeadEntries();
        if (!PickupsByRoom.TryGetValue(room, out HashSet<BytePickup> pickups))
        {
            pickups = new HashSet<BytePickup>();
            PickupsByRoom.Add(room, pickups);
        }

        pickups.Add(pickup);
    }

    public static void Unregister(BytePickup pickup, RoomController room)
    {
        if (pickup == null || room == null)
            return;

        if (!PickupsByRoom.TryGetValue(room, out HashSet<BytePickup> pickups))
            return;

        pickups.Remove(pickup);
        if (pickups.Count == 0)
            PickupsByRoom.Remove(room);
    }

    /// <summary>
    /// Starts a fast fly-to-UI collection for every Byte belonging to the room.
    /// Returns true when at least one pickup was requested to collect.
    /// </summary>
    public static bool ForceCollectRoom(RoomController room, float duration)
    {
        if (room == null)
            return false;

        CleanupDeadEntries();
        if (!PickupsByRoom.TryGetValue(room, out HashSet<BytePickup> pickups) || pickups.Count == 0)
            return false;

        List<BytePickup> snapshot = new List<BytePickup>(pickups);
        bool requested = false;
        for (int i = 0; i < snapshot.Count; i++)
        {
            BytePickup pickup = snapshot[i];
            if (pickup == null || pickup.IsCollected)
                continue;

            pickup.ForceCollect(Mathf.Max(0.03f, duration));
            requested = true;
        }

        return requested;
    }

    /// <summary>
    /// Guarantees no pickup from the old room can remain after a transition.
    /// Remaining pickups are granted immediately rather than lost.
    /// </summary>
    public static void FinalizeRoomCollection(RoomController room)
    {
        if (room == null)
            return;

        CleanupDeadEntries();
        if (!PickupsByRoom.TryGetValue(room, out HashSet<BytePickup> pickups))
            return;

        List<BytePickup> snapshot = new List<BytePickup>(pickups);
        for (int i = 0; i < snapshot.Count; i++)
        {
            BytePickup pickup = snapshot[i];
            if (pickup != null && !pickup.IsCollected)
                pickup.CollectImmediately();
        }

        PickupsByRoom.Remove(room);
    }

    public static int CountForRoom(RoomController room)
    {
        if (room == null)
            return 0;

        CleanupDeadEntries();
        return PickupsByRoom.TryGetValue(room, out HashSet<BytePickup> pickups) ? pickups.Count : 0;
    }

    public static void ForgetRoom(RoomController room, bool grantRemaining)
    {
        if (room == null)
            return;

        if (grantRemaining)
            FinalizeRoomCollection(room);
        else
            PickupsByRoom.Remove(room);
    }

    private static void CleanupDeadEntries()
    {
        if (PickupsByRoom.Count == 0)
            return;

        List<RoomController> deadRooms = null;
        foreach (KeyValuePair<RoomController, HashSet<BytePickup>> pair in PickupsByRoom)
        {
            if (pair.Key == null)
            {
                if (deadRooms == null) deadRooms = new List<RoomController>();
                deadRooms.Add(pair.Key);
                continue;
            }

            pair.Value.RemoveWhere(pickup => pickup == null || pickup.IsCollected);
            if (pair.Value.Count == 0)
            {
                if (deadRooms == null) deadRooms = new List<RoomController>();
                deadRooms.Add(pair.Key);
            }
        }

        if (deadRooms == null)
            return;

        for (int i = 0; i < deadRooms.Count; i++)
            PickupsByRoom.Remove(deadRooms[i]);
    }
}
