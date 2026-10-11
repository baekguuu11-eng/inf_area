using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public sealed class ChipPanelV22 : MonoBehaviour
{
    private static ChipPanelV22 active;
    private static int closedFrame=-1;
    public static bool CapturesInput => active!=null || closedFrame==Time.frameCount;
    private int openedFrame;
    private IDisposable inputLock;
    private float oldTimeScale;
    private bool paused;
    private TMP_FontAsset font;
    public static void OpenInventory(ChipSlotManager chips)
    {
        if(active!=null) return;
        ChipPanelV22 p=Create("칩 보관함 · 동시 장착 3개 · C / ESC 닫기");
        for(int i=0;i<ChipCatalogV22.Types.Length;i++)
        {
            var type=ChipCatalogV22.Types[i];bool owned=chips.OwnsChip(type);
            string label="["+(i+1)+"] "+ChipCatalogV22.Name(type)+"\n"+ChipCatalogV22.Description(type)+"\n"+(chips.IsChipEquipped(type)?"장착 중 · 클릭하여 해제":owned?"보유 중 · 클릭하여 장착":"미보유 · 상점에서 구매");
            p.Card(label,new Vector2(-350+(i%3)*350,205-(i/3)*180),new Vector2(330,165),ChipCatalogV22.ColorFor(type),()=>{p.Close();chips.ToggleChip(type);},owned,ChipCatalogV22.Icon(type));
        }
    }
    public static void OpenReplacement(ChipSlotManager chips,ChipSlotManager.ChipType incoming,Action<int> chosen)
    {
        if(active!=null) return;
        ChipPanelV22 p=Create(ChipCatalogV22.Name(incoming)+" · 교체할 장착 칩 선택");
        p.Label(ChipCatalogV22.Description(incoming),new Vector2(0,190),new Vector2(1000,100),22);
        for(int i=0;i<chips.Equipped.Count;i++)
        {
            int slot=i;var type=chips.Equipped[i];
            p.Card(ChipCatalogV22.Name(type)+"\n"+ChipCatalogV22.Description(type)+"\n이 칩과 교체",new Vector2(-350+i*350,-30),new Vector2(330,210),ChipCatalogV22.ColorFor(type),()=>{p.Close();chosen(slot);},true,ChipCatalogV22.Icon(type));
        }
        p.Label("ESC · 취소 — 기존 칩은 보관함에 남습니다",new Vector2(0,-220),new Vector2(1000,60),22);
    }
    private static ChipPanelV22 Create(string title)
    {
        GameObject g=new GameObject("ChipPanelV22",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        Canvas canvas=g.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
        CanvasScaler scaler=g.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        ChipPanelV22 p=g.AddComponent<ChipPanelV22>();active=p;p.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/Galmuri11 SDF");
        p.openedFrame=Time.frameCount;p.oldTimeScale=Time.timeScale;p.paused=true;Time.timeScale=0f;p.inputLock=GameInputState.Acquire("ChipPanelV22");
        GameObject bg=new GameObject("Backdrop",typeof(RectTransform),typeof(Image));bg.transform.SetParent(g.transform,false);
        RectTransform br=bg.GetComponent<RectTransform>();br.anchorMin=Vector2.zero;br.anchorMax=Vector2.one;br.offsetMin=br.offsetMax=Vector2.zero;bg.GetComponent<Image>().color=new Color(.015f,.035f,.05f,.97f);
        p.Label(title,new Vector2(0,320),new Vector2(1150,50),27);return p;
    }
    private void Label(string text,Vector2 at,Vector2 size,float fontSize)
    {
        GameObject g=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI));g.transform.SetParent(transform,false);
        RectTransform r=g.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=at;r.sizeDelta=size;
        TMP_Text t=g.GetComponent<TMP_Text>();if(font!=null)t.font=font;t.fontSize=fontSize;t.text=text;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;
    }
    private void Card(string text,Vector2 at,Vector2 size,Color color,Action click,bool enabled,Sprite icon)
    {
        GameObject g=new GameObject("ChipCard",typeof(RectTransform),typeof(Image),typeof(Button));g.transform.SetParent(transform,false);
        RectTransform r=g.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=at;r.sizeDelta=size;
        g.GetComponent<Image>().color=Color.Lerp(new Color(.025f,.06f,.08f,1f),color,enabled?.20f:.06f);
        Button b=g.GetComponent<Button>();b.interactable=enabled;b.onClick.AddListener(()=>click());
        Label(text,at+new Vector2(24,0),size-new Vector2(55,14),17);
        GameObject ic=new GameObject("Icon",typeof(RectTransform),typeof(Image));ic.transform.SetParent(g.transform,false);
        RectTransform ir=ic.GetComponent<RectTransform>();ir.anchorMin=ir.anchorMax=new Vector2(0,.5f);ir.anchoredPosition=new Vector2(25,0);ir.sizeDelta=Vector2.one*40;
        Image image=ic.GetComponent<Image>();image.sprite=icon;image.raycastTarget=false;image.color=enabled?Color.white:Color.gray;
    }
    private void Update() {if(Time.frameCount>openedFrame && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.C))) Close();}
    private void Close() {closedFrame=Time.frameCount;Restore();Destroy(gameObject);}
    private void Restore()
    {
        if(inputLock!=null) {inputLock.Dispose();inputLock=null;}
        if(paused) {Time.timeScale=oldTimeScale;paused=false;}
        if(active==this) active=null;
    }
    private void OnDisable() {Restore();}
    private void OnDestroy() {Restore();}
}
