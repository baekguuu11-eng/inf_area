using UnityEngine;
public static class ChernobylRingV22
{
    private static Material material;
    public static LineRenderer Create(Transform parent,float thickness)
    {
        GameObject g=new GameObject("EnergyRingV22");g.transform.SetParent(parent,false);
        LineRenderer line=g.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=96;
        line.startWidth=line.endWidth=thickness;line.sortingOrder=14;
        if(material==null) material=new Material(Shader.Find("Sprites/Default"));line.sharedMaterial=material;
        return line;
    }
    public static void Draw(LineRenderer line,float radius,Color color)
    {
        for(int i=0;i<line.positionCount;i++) {float a=i*2*Mathf.PI/line.positionCount;line.SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a))*radius);}
        line.startColor=line.endColor=color;
    }
}
