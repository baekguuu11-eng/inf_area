using UnityEngine;

/// <summary>Pixel hatch halves and pooled local steam. No UI text or damage numbers.</summary>
public sealed class ChernobylThermalVisualV19 : MonoBehaviour
{
    private SpriteRenderer left, right, glow;
    private SpriteRenderer[] heatBarsV22=new SpriteRenderer[6];
    private SpriteRenderer[] steam=new SpriteRenderer[12];
    private float[] age=new float[12];
    private Vector3 center;
    private float heat, opening, flash;
    private bool vulnerable, restart;
    private EnemyHealth health;
    private Texture2D hatchTexture;
    private Sprite leftSprite, rightSprite;
    public void Initialize(Transform parent, SpriteRenderer core)
    {
        health=GetComponent<EnemyHealth>();
        if(parent==null || core==null) { enabled=false; return; }
        center=parent.InverseTransformPoint(core.transform.position);
        Texture2D source=Resources.Load<Texture2D>("Bosses/Chernobyl/V16/HatchV19");
        if(source==null || !source.isReadable) { Debug.LogError("Chernobyl hatch texture missing or unreadable",this); enabled=false; return; }
        Color32[] src=source.GetPixels32(); int minX=source.width,minY=source.height,maxX=-1,maxY=-1;
        for(int y=0;y<source.height;y++) for(int x=0;x<source.width;x++)
        {
            if(src[y*source.width+x].a<128) continue;
            minX=Mathf.Min(minX,x); minY=Mathf.Min(minY,y); maxX=Mathf.Max(maxX,x); maxY=Mathf.Max(maxY,y);
        }
        if(maxX<minX) { enabled=false; return; }
        // Import normalization uses the same nearest-pixel sampling and PPU as Body/Core/Ring V16.
        const int n=48; hatchTexture=new Texture2D(n,n,TextureFormat.RGBA32,false);
        hatchTexture.filterMode=FilterMode.Point; hatchTexture.wrapMode=TextureWrapMode.Clamp;
        Color32[] pixels=new Color32[n*n];
        for(int y=0;y<n;y++) for(int x=0;x<n;x++)
        {
            int sx=minX+Mathf.Min(maxX-minX,Mathf.FloorToInt((x+0.5f)*(maxX-minX+1)/n));
            int sy=minY+Mathf.Min(maxY-minY,Mathf.FloorToInt((y+0.5f)*(maxY-minY+1)/n));
            Color32 c=src[sy*source.width+sx]; c.a=c.a>=128?(byte)255:(byte)0; pixels[y*n+x]=c;
        }
        hatchTexture.SetPixels32(pixels); hatchTexture.Apply();
        leftSprite=Sprite.Create(hatchTexture,new Rect(0,0,24,48),new Vector2(1f,0.5f),ChernobylArtV16.PixelsPerUnit);
        rightSprite=Sprite.Create(hatchTexture,new Rect(24,0,24,48),new Vector2(0f,0.5f),ChernobylArtV16.PixelsPerUnit);
        int order=core.sortingOrder+4;
        left=Make(parent,"Hatch_LeftV19",leftSprite,order); right=Make(parent,"Hatch_RightV19",rightSprite,order);
        glow=Make(parent,"ThermalHaloV19",ChernobylRuntimeSprites.Ring,order-1);
        glow.transform.localScale=Vector3.one*(1.1f/glow.sprite.bounds.size.x);
        for(int i=0;i<steam.Length;i++)
        {
            steam[i]=Make(parent,"SteamV19_"+i,ChernobylRuntimeSprites.FilledCircle,order+1);
            age[i]=i/(float)steam.Length; steam[i].enabled=false;
        }
        for(int i=0;i<heatBarsV22.Length;i++)
        {
            SpriteRenderer bar=Make(parent,"HeatVentV22_"+i,ChernobylRuntimeSprites.WhitePixel,order);
            bar.transform.localPosition=center+new Vector3(-.4f+i*.16f,-.60f,0);
            bar.transform.localScale=new Vector3(.10f/bar.sprite.bounds.size.x,.055f/bar.sprite.bounds.size.y,1);
            heatBarsV22[i]=bar;
        }
        SetState(0f,0f,false);
    }
    private SpriteRenderer Make(Transform parent,string name,Sprite sprite,int order)
    {
        GameObject go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.localPosition=center;
        SpriteRenderer sr=go.AddComponent<SpriteRenderer>(); sr.sprite=sprite; sr.sortingOrder=order; return sr;
    }
    public void SetState(float value,float open,bool exposed) { heat=value; opening=open; vulnerable=exposed; }
    public void WarnRestart(bool value) { restart=value; }
    public void Blocked() { flash=0.08f; }
    private void LateUpdate()
    {
        if(left==null) return;
        bool alive=health==null || health.CurrentHealth>0;
        left.enabled=right.enabled=glow.enabled=alive;
        for(int i=0;i<heatBarsV22.Length;i++) if(heatBarsV22[i]!=null)
        {heatBarsV22[i].enabled=alive;heatBarsV22[i].color=heat>i/6f?Color.Lerp(new Color(1,.65f,.08f),new Color(1,.18f,.03f),heat):new Color(.22f,.12f,.07f); }
        float t=opening*opening*(3f-2f*opening);
        left.transform.localPosition=center+Vector3.left*(0.47f*t);
        right.transform.localPosition=center+Vector3.right*(0.47f*t);
        left.transform.localRotation=Quaternion.Euler(0,0,8f*t); right.transform.localRotation=Quaternion.Euler(0,0,-8f*t);
        flash=Mathf.Max(0,flash-Time.deltaTime);
        Color warm=Color.Lerp(Color.white,new Color(1f,0.50f,0.24f),heat*0.55f);
        left.color=right.color=flash>0?new Color(1.8f,1.8f,1.8f):warm;
        float pulse=restart?0.5f+0.5f*Mathf.Sin(Time.time*22f):0.5f+0.5f*Mathf.Sin(Time.time*(3f+heat*7f));
        Color glowColor=vulnerable?new Color(1f,0.82f,0.42f):new Color(1f,0.30f,0.08f);
        glowColor.a=heat*(0.12f+0.18f*pulse); glow.color=glowColor;
        for(int i=0;i<steam.Length;i++)
        {
            age[i]=Mathf.Repeat(age[i]+Time.deltaTime*(vulnerable?0.9f:0.5f),1f);
            SpriteRenderer sr=steam[i]; sr.enabled=alive && heat>0.45f;
            float side=i%2==0?-1f:1f;
            sr.transform.localPosition=center+new Vector3(side*(0.4f+age[i]*0.3f)+Mathf.Sin(i+Time.time*2f)*0.04f,0.2f+age[i]*0.7f,0f);
            sr.transform.localScale=Vector3.one*((0.04f+age[i]*0.11f)/sr.sprite.bounds.size.x);
            sr.color=new Color(0.9f,0.82f,0.7f,(1f-age[i])*heat*(vulnerable?0.28f:0.13f));
        }
    }
    private void OnDestroy()
    {
        if(leftSprite!=null) Destroy(leftSprite); if(rightSprite!=null) Destroy(rightSprite);
        if(hatchTexture!=null) Destroy(hatchTexture);
    }
}
