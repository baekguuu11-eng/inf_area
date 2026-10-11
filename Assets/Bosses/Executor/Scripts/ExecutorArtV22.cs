using UnityEngine;
public static class ExecutorArtV22
{
    private static readonly Sprite[] sprites=new Sprite[3];
    private static readonly string[] names={"New_Piskel_32_1","New_Piskel_29_3","New_Piskel_32_2"};
    public static Sprite Get(int stage)
    {
        stage=Mathf.Clamp(stage,0,2);if(sprites[stage]!=null) return sprites[stage];
        Texture2D texture=Resources.Load<Texture2D>("Bosses/Executor/V22/"+names[stage]);if(texture==null) return null;
        texture.filterMode=FilterMode.Point;
        // Unity texture coordinates start at the bottom; original alpha bounds: x193,y182,w126,h148.
        Rect crop=stage==0?new Rect(193,182,126,148):new Rect(0,0,126,148);
        sprites[stage]=Sprite.Create(texture,crop,new Vector2(.5f,0),74f,0,SpriteMeshType.FullRect);
        sprites[stage].name="Executor_V22_Phase"+(stage+1);return sprites[stage];
    }
}
