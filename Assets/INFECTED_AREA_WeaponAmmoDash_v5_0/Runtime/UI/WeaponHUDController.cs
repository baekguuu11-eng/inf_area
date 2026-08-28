using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WeaponHUDController : MonoBehaviour
{
    private PlayerWeaponInventory inventory; private PlayerAmmoController ammo; private Canvas canvas; private CanvasGroup group; private Image icon; private TMP_Text nameText; private TMP_Text ammoText; private Image reloadFill;
    private void Awake(){inventory=GetComponent<PlayerWeaponInventory>();ammo=GetComponent<PlayerAmmoController>();Build();}
    private void OnEnable(){if(inventory!=null)inventory.WeaponChanged+=OnWeapon;if(ammo!=null){ammo.AmmoChanged+=Refresh;ammo.ReloadStateChanged+=OnReload;}Refresh();}
    private void OnDisable(){if(inventory!=null)inventory.WeaponChanged-=OnWeapon;if(ammo!=null){ammo.AmmoChanged-=Refresh;ammo.ReloadStateChanged-=OnReload;}}
    private void Update(){if(group!=null){bool show=GameplayIntroShield.GameplayVisible;group.alpha=show?1f:0f;group.blocksRaycasts=false;group.interactable=false;}}
    private void OnWeapon(WeaponDefinition a,WeaponDefinition b)=>Refresh();
    private void OnReload(bool active,float progress){if(reloadFill!=null){reloadFill.transform.parent.gameObject.SetActive(active);reloadFill.fillAmount=Mathf.Clamp01(progress);}Refresh();}
    private void Refresh()
    {
        if(inventory==null)return; WeaponDefinition w=inventory.CurrentWeapon; if(w==null)return;
        if(icon!=null){icon.sprite=w.idleSprite;icon.preserveAspect=true;icon.color=Color.white;}
        if(nameText!=null)nameText.text=w.displayName;
        if(ammoText!=null)
        {
            if(w.category==WeaponCategory.Melee)ammoText.text="MELEE  ∞";
            else ammoText.text=(ammo!=null?ammo.GetShotsRemaining(w):0)+" / "+(ammo!=null?ammo.ReserveAmmo:0);
        }
    }
    private void Build()
    {
        GameObject existing=GameObject.Find("WeaponHUDCanvas");
        if(existing!=null){canvas=existing.GetComponent<Canvas>();group=existing.GetComponent<CanvasGroup>();Transform root=existing.transform.Find("HUDRoot");if(root!=null){icon=root.Find("Icon")?.GetComponent<Image>();nameText=root.Find("Name")?.GetComponent<TMP_Text>();ammoText=root.Find("Ammo")?.GetComponent<TMP_Text>();reloadFill=root.Find("Reload/Fill")?.GetComponent<Image>();}if(icon!=null&&nameText!=null&&ammoText!=null)return;Destroy(existing);}
        GameObject c=new GameObject("WeaponHUDCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(CanvasGroup));canvas=c.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=250;group=c.GetComponent<CanvasGroup>();
        CanvasScaler scaler=c.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        GameObject r=UI("HUDRoot",c.transform);RectTransform rr=r.GetComponent<RectTransform>();rr.anchorMin=rr.anchorMax=new Vector2(1,0);rr.pivot=new Vector2(1,0);rr.anchoredPosition=new Vector2(-26,24);rr.sizeDelta=new Vector2(270,88);
        Image bg=r.AddComponent<Image>();bg.color=new Color(.015f,.035f,.055f,.78f);
        GameObject line=UI("Accent",r.transform);RectTransform lr=line.GetComponent<RectTransform>();lr.anchorMin=new Vector2(0,0);lr.anchorMax=new Vector2(0,1);lr.pivot=new Vector2(0,.5f);lr.sizeDelta=new Vector2(4,0);line.AddComponent<Image>().color=new Color(.15f,.95f,1f,1f);
        icon=ImageNode("Icon",r.transform,new Vector2(10,10),new Vector2(64,68));
        nameText=TextNode("Name",r.transform,new Vector2(84,49),new Vector2(172,25),18,TextAlignmentOptions.Left);
        ammoText=TextNode("Ammo",r.transform,new Vector2(84,14),new Vector2(172,33),27,TextAlignmentOptions.Left);
        GameObject reload=UI("Reload",r.transform);RectTransform re=reload.GetComponent<RectTransform>();re.anchorMin=re.anchorMax=new Vector2(0,0);re.pivot=new Vector2(0,0);re.anchoredPosition=new Vector2(84,7);re.sizeDelta=new Vector2(166,4);reload.AddComponent<Image>().color=new Color(.08f,.14f,.18f,.9f);reload.SetActive(false);
        GameObject fill=UI("Fill",reload.transform);RectTransform fr=fill.GetComponent<RectTransform>();fr.anchorMin=fr.anchorMax=new Vector2(0,.5f);fr.pivot=new Vector2(0,.5f);fr.anchoredPosition=Vector2.zero;fr.sizeDelta=new Vector2(166,4);reloadFill=fill.AddComponent<Image>();reloadFill.color=new Color(.2f,1f,.95f,1f);reloadFill.type=Image.Type.Filled;reloadFill.fillMethod=Image.FillMethod.Horizontal;reloadFill.fillOrigin=0;
    }
    private static GameObject UI(string n,Transform p){GameObject g=new GameObject(n,typeof(RectTransform));g.transform.SetParent(p,false);return g;}
    private static Image ImageNode(string n,Transform p,Vector2 pos,Vector2 size){GameObject g=UI(n,p);RectTransform r=g.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(0,0);r.pivot=new Vector2(0,0);r.anchoredPosition=pos;r.sizeDelta=size;return g.AddComponent<Image>();}
    private static TMP_Text TextNode(string n,Transform p,Vector2 pos,Vector2 size,float font,TextAlignmentOptions align){GameObject g=UI(n,p);RectTransform r=g.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(0,0);r.pivot=new Vector2(0,0);r.anchoredPosition=pos;r.sizeDelta=size;TMP_Text t=g.AddComponent<TextMeshProUGUI>();t.font=ResolveFont();t.fontSize=font;t.alignment=align;t.enableWordWrapping=false;t.overflowMode=TextOverflowModes.Ellipsis;t.color=new Color(.87f,1f,1f,1f);return t;}
    private static TMP_FontAsset ResolveFont(){TMP_FontAsset[] fonts=Resources.FindObjectsOfTypeAll<TMP_FontAsset>();for(int i=0;i<fonts.Length;i++)if(fonts[i]!=null&&fonts[i].name.ToLowerInvariant().Contains("galmuri"))return fonts[i];return TMP_Settings.defaultFontAsset;}
}
