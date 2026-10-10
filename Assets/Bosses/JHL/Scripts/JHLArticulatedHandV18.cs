using System.Collections.Generic;
using UnityEngine;

public enum JHLHandPoseV18 { Open, Fist, FingerGun }

// Runtime bone rig, not a flat replacement sprite. All dimensions are normalized to Visual.
public sealed class JHLArticulatedHandV18 : MonoBehaviour
{
    private readonly List<Transform> joints = new List<Transform>();
    private readonly List<int> fingers = new List<int>();
    private readonly List<int> levels = new List<int>();
    private readonly List<Transform> contactPieces = new List<Transform>();
    private PolygonCollider2D palmBarrier;
    private readonly Vector2[] palmPoints = new Vector2[4];
    private Transform rig;
    private Transform indexTip;
    private Transform emitter;
    private float handedness;
    private JHLHandPoseV18 pose;
    private Vector2 aim = Vector2.up;
    public Vector2 Muzzle => indexTip != null ? (Vector2)indexTip.position : (Vector2)transform.position;
    public Vector2 Direction => indexTip != null ? ((Vector2)indexTip.TransformVector(Vector3.up)).normalized : aim;

    public static JHLArticulatedHandV18 Build(Transform hand, Transform visual, bool left,
        List<SpriteRenderer> renderers, Transform emitter)
    {
        JHLArticulatedHandV18 h = hand.gameObject.AddComponent<JHLArticulatedHandV18>();
        h.handedness = left ? 1f : -1f; h.emitter = emitter;
        h.palmBarrier = hand.GetComponentInChildren<PolygonCollider2D>(true);
        SpriteRenderer old = visual.GetComponent<SpriteRenderer>();
        if (old != null) old.sprite = null;
        h.rig = new GameObject("ArticulatedPalm").transform;
        h.rig.SetParent(visual, false);
        h.Piece(h.rig, "Palm", new Vector2(0f, -0.10f), new Vector2(0.44f, 0.40f), renderers);
        h.Piece(h.rig, "Wrist", new Vector2(0f, -0.35f), new Vector2(0.27f, 0.15f), renderers);
        string[] names = { "Thumb", "Index", "Middle", "Ring", "Little" };
        float[] bases = { -0.22f, -0.16f, -0.05f, 0.065f, 0.17f };
        float[] lengths = { 0.14f, 0.14f, 0.155f, 0.14f, 0.115f };
        for (int finger = 0; finger < 5; finger++)
        {
            Transform parent = h.rig;
            int count = finger == 0 ? 2 : 3;
            for (int level = 0; level < count; level++)
            {
                Transform joint = new GameObject(names[finger] + "_Joint_" + level).transform;
                joint.SetParent(parent, false);
                float length = lengths[finger] * (level == 2 ? 0.78f : 1f);
                joint.localPosition = level == 0 ? new Vector3(bases[finger] * h.handedness, finger == 0 ? -0.12f : 0.075f, 0f)
                    : new Vector3(0f, lengths[finger], 0f);
                h.joints.Add(joint); h.fingers.Add(finger); h.levels.Add(level);
                h.Piece(joint, names[finger] + "_Phalanx_" + level, new Vector2(0f, length * 0.5f),
                    new Vector2(finger == 0 ? 0.105f : 0.085f, length), renderers);
                parent = joint;
                if (finger == 1 && level == 2)
                {
                    h.indexTip = new GameObject("Index_Muzzle").transform;
                    h.indexTip.SetParent(joint, false); h.indexTip.localPosition = new Vector3(0f, length, 0f);
                }
            }
        }
        h.SetPose(JHLHandPoseV18.Open, Vector2.up);
        h.ApplyPose(1f);
        return h;
    }

    private void Piece(Transform parent, string name, Vector2 center, Vector2 size, List<SpriteRenderer> all)
    {
        Transform piece = new GameObject(name).transform;
        piece.SetParent(parent, false); piece.localPosition = center;
        piece.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer sr = piece.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = JHLRuntimeSprites.WhitePixel; sr.sortingOrder = 32;
        sr.color = new Color(0.91f, 0.87f, 0.84f); all.Add(sr); contactPieces.Add(piece);
        Transform edge = new GameObject("JointShadow").transform;
        edge.SetParent(piece, false); edge.localPosition = new Vector3(0f, -0.38f, 0f);
        edge.localScale = new Vector3(0.88f, 0.12f, 1f);
        SpriteRenderer shade = edge.gameObject.AddComponent<SpriteRenderer>();
        shade.sprite = JHLRuntimeSprites.WhitePixel; shade.sortingOrder = 33;
        shade.color = new Color(0.36f, 0.19f, 0.28f); all.Add(shade);
    }

    public void SetPose(JHLHandPoseV18 next, Vector2 direction)
    { pose = next; if (direction.sqrMagnitude > 0.001f) aim = direction.normalized; }

    private void ApplyPose(float blend)
    {
        if (rig == null) return;
        for (int i = 0; i < joints.Count; i++)
        {
            int f = fingers[i], level = levels[i];
            float angle = f == 0 ? (level == 0 ? 55f : 12f) * handedness : (level == 0 ? (2 - f) * 5f : 0f) * handedness;
            if (pose == JHLHandPoseV18.Fist) angle = (level == 0 ? 72f : 95f) * handedness;
            if (pose == JHLHandPoseV18.FingerGun)
                angle = f == 1 ? 0f : f == 0 ? (level == 0 ? 65f : -35f) * handedness : (level == 0 ? 85f : 100f) * handedness;
            joints[i].localRotation = Quaternion.Slerp(joints[i].localRotation, Quaternion.Euler(0, 0, angle), blend);
        }
        Vector3 localAim = rig.parent.InverseTransformVector(aim);
        float rotation = pose == JHLHandPoseV18.FingerGun ? Mathf.Atan2(localAim.y, localAim.x) * Mathf.Rad2Deg - 90f : 0f;
        rig.localRotation = Quaternion.Slerp(rig.localRotation, Quaternion.Euler(0, 0, rotation), blend);
        if (emitter != null && indexTip != null) emitter.position = indexTip.position;
    }

    private void LateUpdate()
    {
        ApplyPose(1f - Mathf.Exp(-22f * Time.deltaTime));
        if (palmBarrier != null && contactPieces.Count > 0)
        {
            Transform palm = contactPieces[0];
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = new Vector3(i == 0 || i == 3 ? -0.5f : 0.5f, i < 2 ? -0.5f : 0.5f, 0f);
                palmPoints[i] = palmBarrier.transform.InverseTransformPoint(palm.TransformPoint(corner));
            }
            palmBarrier.points = palmPoints;
        }
    }

    // SAT against the player's bounds; use each current palm/phalanx, never the old hand ellipse.
    public bool Touches(Collider2D player)
    {
        if (player == null || !player.enabled) return false;
        Bounds b = player.bounds;
        foreach (Transform part in contactPieces)
        {
            if (part == null) continue;
            Vector2 x = part.TransformVector(Vector3.right * 0.5f);
            Vector2 y = part.TransformVector(Vector3.up * 0.5f);
            Vector2 delta = (Vector2)b.center - (Vector2)part.position;
            if (Separated(delta, x, y, b.extents, Vector2.right) || Separated(delta, x, y, b.extents, Vector2.up) ||
                Separated(delta, x, y, b.extents, new Vector2(-x.y, x.x)) ||
                Separated(delta, x, y, b.extents, new Vector2(-y.y, y.x))) continue;
            return true;
        }
        return false;
    }

    private static bool Separated(Vector2 delta, Vector2 x, Vector2 y, Vector2 extents, Vector2 axis)
    {
        float r = Mathf.Abs(Vector2.Dot(x, axis)) + Mathf.Abs(Vector2.Dot(y, axis)) +
            extents.x * Mathf.Abs(axis.x) + extents.y * Mathf.Abs(axis.y);
        return Mathf.Abs(Vector2.Dot(delta, axis)) > r;
    }
}
