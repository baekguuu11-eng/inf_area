using UnityEngine;
public sealed class ChipBurstVisualV22 : MonoBehaviour
{
    private LineRenderer ring;
    private float age;
    public static void Spawn(Vector2 center)
    {
        GameObject g=new GameObject("ChipBurstV22");g.transform.position=center;
        ChipBurstVisualV22 v=g.AddComponent<ChipBurstVisualV22>();v.ring=ChernobylRingV22.Create(g.transform,.045f);
        ChernobylRingV22.Draw(v.ring,1f,new Color(1f,.6f,.15f,.9f));
    }
    private void Update()
    {
        age+=Time.deltaTime;if(age>=.25f) {Destroy(gameObject);return;}
        ChernobylRingV22.Draw(ring,1f,new Color(1f,.6f,.15f,1f-age/.25f));
    }
}
