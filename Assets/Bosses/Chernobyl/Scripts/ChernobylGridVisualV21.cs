using System.Collections.Generic;
using UnityEngine;
// Native geometry only. Ground footprint is damaging once; rising heat and debris are decorative.
public sealed class ChernobylGridVisualV21 : MonoBehaviour
{
    private static readonly Queue<ChernobylGridVisualV21> blastPool=new Queue<ChernobylGridVisualV21>();
    private SpriteRenderer fill;
    private SpriteRenderer[] edges,sparks,plumes;
    private Vector2[] velocities;
    private Vector2 size;
    private float age,duration;
    private bool warning,returned,detail;
    private static SpriteRenderer Part(Transform root,string name,int order)
    {
        GameObject g=new GameObject(name);g.transform.SetParent(root,false);
        var sr=g.AddComponent<SpriteRenderer>();sr.sprite=ChernobylRuntimeSprites.WhitePixel;sr.sortingOrder=order;return sr;
    }
    private static void Place(SpriteRenderer sr,Vector2 pos,Vector2 scale,Color color)
    {
        sr.transform.localPosition=pos;sr.transform.localScale=new Vector3(scale.x/sr.sprite.bounds.size.x,scale.y/sr.sprite.bounds.size.y,1);sr.color=color;
    }
    private static GameObject Create(Vector3 center,Vector2 size,bool warning,float seconds,bool detail)
    {
        ChernobylGridVisualV21 v=null;
        if(!warning) while(blastPool.Count>0 && v==null) v=blastPool.Dequeue();
        if(v==null)
        {
            GameObject g=new GameObject(warning?"CHN_GridWarningV23":"CHN_NativeBlastV23");v=g.AddComponent<ChernobylGridVisualV21>();
            v.fill=Part(g.transform,"Floor",10);v.edges=new SpriteRenderer[4];
            for(int i=0;i<4;i++) v.edges[i]=Part(g.transform,"Border",warning?14:11);
            if(!warning)
            {
                v.sparks=new SpriteRenderer[6];v.velocities=new Vector2[6];v.plumes=new SpriteRenderer[3];
                for(int i=0;i<6;i++) v.sparks[i]=Part(g.transform,"Debris",12);
                for(int i=0;i<3;i++) v.plumes[i]=Part(g.transform,"HeatColumn",12);
            }
        }
        v.transform.position=center;v.warning=warning;v.size=size;v.age=0;v.duration=seconds;v.returned=false;v.detail=detail;
        if(v.sparks!=null) for(int i=0;i<v.sparks.Length;i++)
        {
            v.sparks[i].enabled=detail;
            float angle=(i*60f+Random.Range(-15f,15f))*Mathf.Deg2Rad;
            v.velocities[i]=new Vector2(Mathf.Cos(angle)*size.x,Mathf.Sin(angle)*size.y)*Random.Range(.6f,1.2f);
        }
        if(v.plumes!=null) foreach(var sr in v.plumes) sr.enabled=detail;
        v.gameObject.SetActive(true);v.Paint();return v.gameObject;
    }
    public static GameObject Warning(Vector3 center,Vector2 size,float seconds) {return Create(center,size,true,seconds,false);}
    public static GameObject Blast(Vector3 center,Vector2 size,bool detail=true) {return Create(center,size,false,.46f,detail);}
    private void Update()
    {
        age+=Time.deltaTime;
        if(age>=duration)
        {
            if(warning) {Destroy(gameObject);return;}
            if(!returned) {returned=true;gameObject.SetActive(false);if(blastPool.Count<128) blastPool.Enqueue(this);else Destroy(gameObject);}
            return;
        }
        Paint();
    }
    private void Paint()
    {
        float t=Mathf.Clamp01(age/Mathf.Max(.01f,duration)),flash=Mathf.Clamp01(1f-age/.075f);
        Color edge;float expand=1f;
        if(warning)
        {
            float armed=Mathf.Clamp01((age-duration+.15f)/.15f);
            Place(fill,Vector2.zero,size,new Color(1,.22f,.025f,.065f+t*.07f+armed*.1f));
            edge=Color.Lerp(new Color(1,.35f,.08f,.7f),new Color(1,.88f,.45f,1),armed);
        }
        else
        {
            Place(fill,Vector2.zero,size,new Color(1,.69f,.18f,.47f*flash));
            edge=Color.Lerp(new Color(1,.42f,.04f,(1-t)*.6f),new Color(1,.97f,.75f,.95f),flash);
            expand=1+Mathf.Clamp01(age/.22f)*.13f;
            if(detail)
            {
                for(int i=0;i<sparks.Length;i++)
                {
                    Vector2 at=velocities[i]*age+Vector2.up*(.18f*Mathf.Sin(Mathf.Clamp01(age/.4f)*Mathf.PI));
                    Place(sparks[i],at,Vector2.one*Mathf.Lerp(.08f,.025f,t),new Color(1,.65f,.18f,(1-t)*.9f));
                }
                float rise=Mathf.Clamp01(age/.22f),fade=Mathf.Clamp01(1-age/.36f);
                for(int i=0;i<plumes.Length;i++)
                {
                    float height=size.y*(.18f+rise*.65f)*(i==1?1f:.7f);
                    Place(plumes[i],new Vector2((i-1)*size.x*.19f,height*.45f),new Vector2(size.x*(.22f-.13f*rise),height),
                        Color.Lerp(new Color(1,.33f,.025f,fade*.42f),new Color(1,.92f,.58f,fade*.65f),flash));
                }
            }
        }
        Vector2 extent=size*.5f*expand;float width=warning?.03f:.045f;
        Place(edges[0],new Vector2(0,-extent.y),new Vector2(size.x*expand,width),edge);
        Place(edges[1],new Vector2(0,extent.y),new Vector2(size.x*expand,width),edge);
        Place(edges[2],new Vector2(-extent.x,0),new Vector2(width,size.y*expand),edge);
        Place(edges[3],new Vector2(extent.x,0),new Vector2(width,size.y*expand),edge);
    }
}
