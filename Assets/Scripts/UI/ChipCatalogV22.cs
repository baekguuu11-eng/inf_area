using UnityEngine;
using T=ChipSlotManager.ChipType;
public static class ChipCatalogV22
{
    public static readonly T[] Types={T.MoveSpeed,T.RoomRepair,T.MeleeDamage,T.AmmoRecovery,T.DataCollector,T.AmmoEfficiency,T.DeathBurst,T.SlowRounds,T.MaxHealth};
    private static readonly string[] Names={"기동 부스터 칩","자동 복구 칩","근접 증폭 칩","탄약 회수 칩","데이터 수집 칩","탄약 효율 칩","잔류 폭발 칩","둔화 탄환 칩","생체 확장 칩"};
    private static readonly string[] Descriptions={
        "이동 속도 +12%\n장착 중 항상 적용됩니다.",
        "처음 방문하는 방에서 체력 1칸 회복\n시작 방·재방문 제외. 최대 체력까지만 회복합니다.",
        "근접 공격 피해 +20%\n모든 근접 무기에 적용됩니다.",
        "근접 처치 시 예비 탄약 +1\n일반 적 직접 처치에 적용. 탄약 최대치까지 회수합니다.",
        "적에게서 얻는 바이트 +20%\n소수점 보너스는 누적됩니다.",
        "원거리 탄약 소비 -25%\n발사 비용 기준. 절약분은 누적 적용됩니다.",
        "일반 적 처치 시 주변 폭발\n반경 1, 처치 공격 피해의 40%. 폭발끼리 재발동하지 않습니다.",
        "탄환 적중 시 이동 속도 -25%\n일반 적에게 1초 적용. 중첩 없이 지속시간 갱신.",
        "최대 체력 +1칸\n장착만으로 현재 체력이 회복되지는 않습니다."};
    private static readonly int[] Prices={14,28,14,18,16,18,22,18,20};
    private static readonly string[] Colors={"31DED8","64E887","FF6571","BCD5E7","FFD75E","5D9EFF","FF993E","B28BFF","FF8BCB"};
    // Procedural, readable 8x8 UI glyphs; no generated sprite sheet dependency.
    private static readonly string[] Glyphs={
        "00011000/00011000/00111000/00111000/00111110/00111111/00111111/00000000",
        "00111100/00100100/00100100/11111111/11111111/00100100/00100100/00111100",
        "00000011/00000111/00001110/00011100/10111000/01110000/11101000/11000000",
        "00111000/00101000/00101000/00111000/00000010/01111111/01000010/01111100",
        "00011000/00111100/01111110/11111111/11111111/01111110/00111100/00011000",
        "00011000/00111100/00100100/00100100/00111100/00111100/11000011/01111110",
        "10011001/01011010/00111100/11111111/11111111/00111100/01011010/10011001",
        "00001100/00011110/00010010/00011110/10111110/10100000/10100000/00000000",
        "01100110/11111111/11111111/11111111/01111110/00111100/00011000/00000000"};
    private static readonly Sprite[] Icons=new Sprite[9];
    public static bool Contains(T type) => System.Array.IndexOf(Types,type)>=0;
    private static int Index(T type) => System.Array.IndexOf(Types,type);
    public static string Name(T type) {int i=Index(type);return i<0?"미사용 칩":Names[i];}
    public static string Description(T type) {int i=Index(type);return i<0?"":Descriptions[i];}
    public static int Price(T type) {int i=Index(type);return i<0?0:Prices[i];}
    public static Color ColorFor(T type) {int i=Index(type);ColorUtility.TryParseHtmlString("#"+(i<0?"888888":Colors[i]),out Color c);return c;}
    public static Sprite Icon(T type)
    {
        int i=Index(type);if(i<0) return null;if(Icons[i]!=null) return Icons[i];
        Texture2D t=new Texture2D(24,24,TextureFormat.RGBA32,false);t.filterMode=FilterMode.Point;t.wrapMode=TextureWrapMode.Clamp;
        Color accent=ColorFor(type);Color dark=new Color(.035f,.075f,.1f,1f);
        string[] rows=Glyphs[i].Split('/');
        for(int y=0;y<24;y++) for(int x=0;x<24;x++)
        {
            Color c=Color.clear;
            if(x>=3 && x<=20 && y>=3 && y<=20) c=(x==3 || x==20 || y==3 || y==20)?accent:dark;
            if(((x==1 || x==22) && y>=5 && y<=18 && y%3==0) || ((y==1 || y==22) && x>=5 && x<=18 && x%3==0)) c=accent*.75f;
            if(x>=8 && x<16 && y>=8 && y<16 && rows[15-y][x-8]=='1') c=accent;
            t.SetPixel(x,y,c);
        }
        t.Apply();Icons[i]=Sprite.Create(t,new Rect(0,0,24,24),new Vector2(.5f,.5f),24);return Icons[i];
    }
}
