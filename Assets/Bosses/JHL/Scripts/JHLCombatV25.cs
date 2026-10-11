using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// V25 replaces the legacy pattern loop. Room routing and EnemyHealth remain compatible.
public sealed class JHLCombatV25 : MonoBehaviour
{
    private sealed class Hand
    {
        public Transform root;
        public SpriteRenderer art, shadow, weak;
        public BoxCollider2D hurt;
        public Vector3 home;
        public bool vulnerable;
        public int pose;
        public float earned;
    }
    private MapManager map;
    private RoomController room;
    private EnemyHealth hp;
    private PlayerHealth player;
    private Collider2D playerCollider;
    private Bounds arena;
    private Hand[] hands = new Hand[2];
    private Transform face;
    private SpriteRenderer faceArt, postureFill;
    private BoxCollider2D faceHurt;
    private Vector3 faceHome;
    private JHLBossHUD hud;
    private JHLBossMusic music;
    private AudioSource audioSource;
    private readonly Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();
    private readonly List<GameObject> effects = new List<GameObject>();
    private Sprite[] atlas;
    private bool started, dead, exposed, resetting;
    private IDisposable inputLock;
    private float posture, nextFeedback, nextDamage, flashUntil;
    private int phase = 1, previous = -1, selected;
    private const float PostureLimit = 100f;
    private const int MaxHP = 1800;
    public int Phase => phase;
    public bool IsDead => dead;
    public string Pattern { get; private set; } = "대기";
    public Transform Face => face;
    public Transform Left => hands[0].root;
    public Transform Right => hands[1].root;
    public EnemyHealth Health => hp;
    public RoomController Room => room;

    public void Initialize(MapManager manager, RoomController owner, EnemyHealth health)
    {
        map = manager; room = owner; hp = health;
        if (!RoomLayoutV24.TryGetBounds(room, out arena)) arena = room.EnemySpawnArea.bounds;
        player = FindAnyObjectByType<PlayerHealth>();
        if (player != null) playerCollider = player.GetComponent<Collider2D>();
        hp.SetMaxHealth(MaxHP, true);
        atlas = JHLArtV25.Load();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false; audioSource.spatialBlend = 0f;
        foreach (string key in new[] { "Windup", "Impact", "Sweep", "Charge", "Beam", "Hit", "Break", "Armor" })
            sounds[key] = Resources.Load<AudioClip>("Bosses/JHL/V25/" + key);
        for (int i = 0; i < 2; i++)
        {
            Hand h = new Hand(); hands[i] = h;
            GameObject go = new GameObject(i == 0 ? "LeftHandV25" : "RightHandV25");
            go.transform.SetParent(transform, false); h.root = go.transform;
            h.home = arena.center + new Vector3((i == 0 ? -1 : 1) * (arena.extents.x - 1.7f), 1.1f, 0f);
            h.root.position = h.home;
            h.shadow = RectSprite("ContactShadow", go.transform, new Vector2(2.7f, .60f), new Color(0, 0, 0, .38f), 8);
            h.art = new GameObject("Art").AddComponent<SpriteRenderer>();
            h.art.transform.SetParent(go.transform, false); h.art.sortingOrder = 24;
            h.art.flipX = i == 1;
            h.weak = RectSprite("WristCore", go.transform, new Vector2(.85f, .55f), new Color(.4f, 1f, .95f, .8f), 26);
            h.shadow.sprite = JHLRuntimeSprites.FilledCircle;
            h.weak.sprite = JHLRuntimeSprites.Ring;
            h.weak.transform.localScale = new Vector3(.75f, .65f, 1);
            h.weak.transform.localPosition = new Vector3(0, .65f, 0); h.weak.enabled = false;
            GameObject hit = new GameObject("WristHurtbox"); hit.transform.SetParent(go.transform, false);
            hit.layer = LayerMask.NameToLayer("Enemy");
            h.hurt = hit.AddComponent<BoxCollider2D>(); h.hurt.isTrigger = true;
            h.hurt.size = new Vector2(1.2f, .8f); h.hurt.offset = new Vector2(0, .65f); h.hurt.enabled = false;
            Pose(h, 0);
        }
        face = new GameObject("FaceV25").transform; face.SetParent(transform, false);
        faceHome = arena.center + new Vector3(0, arena.extents.y - 1.05f, 0);
        face.position = faceHome;
        faceArt = face.gameObject.AddComponent<SpriteRenderer>(); faceArt.sortingOrder = 22;
        SetArt(faceArt, 6, 3.5f);
        GameObject fh = new GameObject("FaceHurtbox"); fh.transform.SetParent(face, false); fh.layer = LayerMask.NameToLayer("Enemy");
        faceHurt = fh.AddComponent<BoxCollider2D>(); faceHurt.isTrigger = true; faceHurt.size = new Vector2(2.3f, 2.4f); faceHurt.enabled = false;
        SpriteRenderer bar = RectSprite("PostureBack", face, new Vector2(2.2f, .10f), new Color(.10f, .07f, .16f), 29);
        bar.transform.localPosition = new Vector3(0, -1.8f, 0);
        postureFill = RectSprite("PostureFill", face, new Vector2(.01f, .10f), new Color(.35f, 1f, .85f), 30);
        postureFill.transform.localPosition = bar.transform.localPosition;
        hud = JHLBossHUD.Create(); hud.transform.SetParent(room.transform, false); hud.HideAllImmediate();
        music = JHLBossMusic.Create(transform);
        hp.Damaged += Damaged;
    }

    private void SetArt(SpriteRenderer sr, int index, float width)
    {
        sr.sprite = atlas[index];
        if (sr.sprite != null) sr.transform.localScale = Vector3.one * (width / sr.sprite.bounds.size.x);
    }
    private void Pose(Hand h, int pose)
    {
        h.pose = pose; SetArt(h.art, pose, 3.4f);
    }
    private static SpriteRenderer RectSprite(string name, Transform parent, Vector2 size, Color color, int order)
    {
        GameObject go = new GameObject(name); go.transform.SetParent(parent, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>(); sr.sprite = JHLRuntimeSprites.WhitePixel;
        sr.color = color; sr.sortingOrder = order; go.transform.localScale = new Vector3(size.x, size.y, 1f); return sr;
    }
    private void Sound(string key, float volume = .65f)
    {
        if (sounds.TryGetValue(key, out AudioClip clip) && clip != null)
        { audioSource.pitch = UnityEngine.Random.Range(.96f, 1.04f); audioSource.PlayOneShot(clip, volume); }
    }
    public void Begin()
    {
        if (started || dead) return;
        started = true; StartCoroutine(Intro());
    }
    private IEnumerator Intro()
    {
        inputLock = GameInputState.Acquire("JHLIntroV25");
        try
        {
            music.PrepareIntro(); hud.ShowIntro("JHL — CENTRAL ADMINISTRATOR");
            Sound("Charge", .6f);
            Vector3 from = faceHome + Vector3.up * 3f;
            yield return Move(face, from, faceHome, .75f);
            hud.ReportHealth(hp.CurrentHealth, MaxHP, true); hud.ShowBossBar(true);
        }
        finally { ReleaseInput(); }
        hud.ShowSystem("공격을 유도하고 노출된 손목을 공략하세요");
        faceHurt.enabled = true; music.PlayPhase(1); StartCoroutine(Combat());
    }
    private IEnumerator Combat()
    {
        yield return new WaitForSeconds(.5f);
        while (!dead && player != null && !player.IsDead)
        {
            int targetPhase = hp.NormalizedHealth > .7f ? 1 : hp.NormalizedHealth > .35f ? 2 : 3;
            if (targetPhase != phase)
            { phase = targetPhase; music.PlayPhase(phase); hud.ShowPhase("권한 단계 " + phase); Sound("Charge"); yield return new WaitForSeconds(.65f); }
            int choice;
            do { choice = UnityEngine.Random.Range(0, phase == 1 ? 3 : phase == 2 ? 5 : 6); } while (choice == previous);
            if (forced >= 0) { choice = forced; forced = -1; }
            previous = choice;
            int side = UnityEngine.Random.Range(0, 2);
            if (choice == 0) yield return Slam(hands[side], true);
            else if (choice == 1) yield return Sweep(side);
            else if (choice == 2) yield return Laser();
            else if (choice == 3)
            {
                Pattern = "교대 내려찍기";
                yield return Slam(hands[side], false);
                if (posture < PostureLimit) yield return Slam(hands[1-side], true);
            }
            else if (choice == 4) yield return Compression();
            else
            {
                Pattern = "강제 실행";
                yield return Slam(hands[side], false);
                if (posture < PostureLimit) yield return Sweep(1-side);
                if (posture < PostureLimit) yield return Slam(hands[side], true);
            }
            if (posture >= PostureLimit) yield return Expose();
            foreach (Hand h in hands) { Vulnerable(h, false); Pose(h, 0); }
            yield return ReturnHands(.35f);
            yield return new WaitForSeconds(phase == 1 ? .45f : phase == 2 ? .28f : .18f);
        }
        ClearEffects(); SetHitboxes(false); music.CrossFadeBackToGameplay(.8f);
    }
    private Vector3 PlayerPoint(float margin)
    {
        Vector3 p = player != null ? player.transform.position : arena.center;
        p.x = Mathf.Clamp(p.x, arena.min.x + margin, arena.max.x - margin);
        p.y = Mathf.Clamp(p.y, arena.min.y + margin, arena.max.y - margin); p.z = 0; return p;
    }
    private IEnumerator Slam(Hand h, bool longRecovery)
    {
        Pattern = "추적 내려찍기"; Vulnerable(h, false); Pose(h, 1); Sound("Windup");
        Vector3 target = PlayerPoint(1.7f);
        GameObject warning = Mark(target, new Vector2(2.6f, 2.0f), new Color(1f, .2f, .38f, .25f));
        float t = 0;
        while (t < .6f && !dead)
        {
            t += Time.deltaTime; target = PlayerPoint(1.7f); warning.transform.position = target;
            h.root.position = Vector3.Lerp(h.root.position, target + Vector3.up * 1.8f, 1f-Mathf.Exp(-10f*Time.deltaTime));
            h.shadow.transform.position = target; yield return null;
        }
        // Lock target and allow a genuine reaction window, including low-speed builds.
        float speed = 5f; PlayerStats stats = player != null ? player.GetComponent<PlayerStats>() : null;
        if (stats != null) speed = Mathf.Max(.5f, stats.MoveSpeed);
        yield return new WaitForSeconds(Mathf.Max(.38f, 1.6f / speed));
        yield return Move(h.root, h.root.position, target, .15f);
        Remove(warning); h.shadow.transform.localPosition = Vector3.zero; Pose(h, 2);
        Impact(target, 1f); DamageRect(target, new Vector2(2.6f, 2f));
        Vulnerable(h, true);
        yield return new WaitForSeconds(longRecovery ? 1.5f : .95f);
        Vulnerable(h, false);
    }
    private IEnumerator Sweep(int side)
    {
        Pattern = "바닥 쓸기"; Hand h = hands[side]; Pose(h, 3); Vulnerable(h, false);
        float y = PlayerPoint(1.4f).y;
        Vector3 a = new Vector3(side == 0 ? arena.min.x+1.5f : arena.max.x-1.5f, y, 0);
        Vector3 b = new Vector3(side == 0 ? arena.max.x-1.5f : arena.min.x+1.5f, y, 0);
        yield return Move(h.root, h.root.position, a, .28f);
        GameObject warning = Mark(new Vector3(arena.center.x, y, 0), new Vector2(arena.size.x, 1.5f), new Color(1f, .22f, .3f, .20f));
        Sound("Windup", .5f); yield return new WaitForSeconds(.9f); Remove(warning); Sound("Sweep");
        float time = 0; const float duration = 1.25f; bool hit = false;
        while (time < duration && !dead)
        {
            time += Time.deltaTime; Vector3 prev = h.root.position;
            h.root.position = Vector3.Lerp(a,b,Mathf.SmoothStep(0,1,time/duration));
            if (!hit) hit = DamageSegment(prev,h.root.position,.78f);
            yield return null;
        }
        Pose(h, 5); Vulnerable(h, true); yield return new WaitForSeconds(1.35f); Vulnerable(h, false);
    }
    private IEnumerator Compression()
    {
        Pattern = "양손 압박";
        float y = Mathf.Clamp(PlayerPoint(2f).y, arena.min.y+2f, arena.max.y-2f);
        Vector3 a = new Vector3(arena.min.x+1.5f,y,0), b = new Vector3(arena.max.x-1.5f,y,0);
        hands[0].root.position = a; hands[1].root.position = b;
        foreach (Hand h in hands) { Pose(h,4); Vulnerable(h,false); }
        GameObject warning = Mark(new Vector3(arena.center.x,y,0), new Vector2(arena.size.x,2.2f),new Color(1f,.23f,.38f,.20f));
        hud.ShowSystem("위 또는 아래로 이탈"); Sound("Windup"); yield return new WaitForSeconds(1.1f);
        Remove(warning); Sound("Sweep"); float t = 0; bool hit=false;
        Vector3 endA = new Vector3(arena.center.x-1.25f,y,0), endB = new Vector3(arena.center.x+1.25f,y,0);
        while(t<1.15f && !dead)
        {
            t+=Time.deltaTime;
            Vector3 pa=hands[0].root.position,pb=hands[1].root.position;
            hands[0].root.position=Vector3.Lerp(a,endA,Mathf.SmoothStep(0,1,t/1.15f));
            hands[1].root.position=Vector3.Lerp(b,endB,Mathf.SmoothStep(0,1,t/1.15f));
            if(!hit) hit=DamageSegment(pa,hands[0].root.position,1.05f)||DamageSegment(pb,hands[1].root.position,1.05f);
            yield return null;
        }
        // Damage only during the motion; no moving solid collider can imprison the player.
        Impact(new Vector3(arena.center.x,y,0),.6f);
        foreach(Hand h in hands) { Pose(h,5); Vulnerable(h,true); }
        yield return new WaitForSeconds(1.5f);
        foreach(Hand h in hands) Vulnerable(h,false);
    }
    private IEnumerator Laser()
    {
        Pattern="얼굴 절단 광선";
        Vector3 origin=face.position+Vector3.down*.8f;
        Vector2 delta=(Vector2)(PlayerPoint(1f)-origin);
        float aim=Mathf.Clamp(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,-150f,-30f);
        float start=aim-18f, end=aim+18f;
        LineRenderer line=Line(origin, RayEnd(origin,start), .09f, new Color(1f,.25f,.7f,.8f));
        LineRenderer final=Line(origin,RayEnd(origin,end),.035f,new Color(1f,.25f,.7f,.4f));
        Sound("Charge"); yield return new WaitForSeconds(1.05f); Remove(final.gameObject);
        line.startWidth=line.endWidth=.27f; line.startColor=line.endColor=new Color(1f,.65f,1f); Sound("Beam");
        float t=0;
        while(t<1.65f && !dead)
        {
            t+=Time.deltaTime; Vector3 tip=RayEnd(origin,Mathf.Lerp(start,end,t/1.65f));
            line.SetPosition(0,origin);line.SetPosition(1,tip);DamageSegment(origin,tip,.18f);yield return null;
        }
        Remove(line.gameObject);
        // Small direct opportunity without consuming the earned posture break.
        exposed=true;SetArt(faceArt,7,3.5f);yield return new WaitForSeconds(.8f);exposed=false;SetArt(faceArt,6,3.5f);
    }
    private Vector3 RayEnd(Vector3 origin,float degrees)
    {
        float r=degrees*Mathf.Deg2Rad;Vector3 d=new Vector3(Mathf.Cos(r),Mathf.Sin(r),0);
        float dist=40;
        if(d.x>.001f)dist=Mathf.Min(dist,(arena.max.x-origin.x)/d.x);
        if(d.x<-.001f)dist=Mathf.Min(dist,(arena.min.x-origin.x)/d.x);
        if(d.y>.001f)dist=Mathf.Min(dist,(arena.max.y-origin.y)/d.y);
        if(d.y<-.001f)dist=Mathf.Min(dist,(arena.min.y-origin.y)/d.y);
        return origin+d*Mathf.Max(0,dist);
    }
    private IEnumerator Expose()
    {
        Pattern="자세 붕괴"; posture=0;Sound("Break",.85f);
        hud.ShowSystem("코어 노출 — 집중 공격");
        foreach(Hand h in hands){Vulnerable(h,false);Pose(h,5);}
        yield return ReturnHands(.3f);
        SetArt(faceArt,7,3.5f);
        yield return Move(face,face.position,arena.center+Vector3.up*.75f,.42f);
        exposed=true;Impact(face.position,.65f);
        yield return new WaitForSeconds(3.2f);
        exposed=false;SetArt(faceArt,6,3.5f);yield return Move(face,face.position,faceHome,.4f);
    }
    private void Vulnerable(Hand h,bool value)
    {if(value&&!h.vulnerable)h.earned=0;h.vulnerable=value;h.hurt.enabled=value;h.weak.enabled=value;}
    public bool FilterDamage(ref DamageContext context)
    {
        if(resetting)return true;
        if(!started||dead||!faceHurt.enabled)return false;
        Hand wrist=null;
        foreach(Hand h in hands)
            if(h.vulnerable && Vector2.Distance(h.hurt.ClosestPoint(context.HitPoint),context.HitPoint)<.22f){wrist=h;break;}
        if(wrist!=null)
        {
            float gain=Mathf.Min(Mathf.Max(0,40f-wrist.earned),context.Damage*.75f);
            wrist.earned+=gain;posture=Mathf.Min(PostureLimit,posture+gain);
            context.Damage=Mathf.Max(1,Mathf.RoundToInt(context.Damage*.85f));
        }
        else
        {
            context.Damage=Mathf.Max(1,Mathf.RoundToInt(context.Damage*(exposed?1.5f:.18f)));
        }
        return true;
    }
    private void Damaged(EnemyHealth sender,int damage,Vector2 direction)
    {
        hud.ReportHealth(hp.CurrentHealth,hp.MaxHealth,false);
        if(resetting||dead||Time.unscaledTime<nextFeedback)return;
        nextFeedback=Time.unscaledTime+.075f;
        Vector2 p=hp.LastDamageContext.HitPoint;
        bool meaningful=exposed;
        foreach(Hand h in hands) if(h.vulnerable&&Vector2.Distance(h.hurt.ClosestPoint(p),p)<.25f){meaningful=true;h.art.color=new Color(.6f,1f,1f);}
        Sound(meaningful?"Hit":"Armor",meaningful?.6f:.24f);
        flashUntil=Time.unscaledTime+.06f;
        if(exposed)faceArt.color=new Color(.6f,1f,1f);
        Burst(p,meaningful?new Color(.5f,1f,.9f):new Color(.7f,.6f,.8f),meaningful?.4f:.16f);
        if(meaningful&&GameFeelManager.Instance!=null){GameFeelManager.Instance.DoHitStop(.025f);GameFeelManager.Instance.DirectionalShake(.07f,.025f,direction);}
    }
    private void LateUpdate()
    {
        if(dead) return;
        if(postureFill!=null)postureFill.transform.localScale=new Vector3(Mathf.Max(.01f,2.2f*posture/PostureLimit),.1f,1f);
        if(Time.unscaledTime>flashUntil)
        {if(faceArt!=null)faceArt.color=Color.white;foreach(Hand h in hands)if(h!=null)h.art.color=Color.white;}
    }
    private bool DamageRect(Vector3 center,Vector2 size)
    {
        if(playerCollider==null||player==null||player.IsDead||Time.time<nextDamage)return false;
        Bounds b=new Bounds(center,new Vector3(size.x,size.y,10));
        if(!b.Intersects(playerCollider.bounds))return false;
        player.TakeDamage(1);nextDamage=Time.time+.65f;return true;
    }
    private bool DamageSegment(Vector3 a,Vector3 b,float radius)
    {
        if(playerCollider==null||player==null||player.IsDead||Time.time<nextDamage)return false;
        Vector2 p=playerCollider.bounds.center,d=(Vector2)(b-a);
        float t=d.sqrMagnitude>.0001f?Mathf.Clamp01(Vector2.Dot(p-(Vector2)a,d)/d.sqrMagnitude):0;
        Vector2 nearest=(Vector2)a+d*t;
        float pr=Mathf.Min(playerCollider.bounds.extents.x,playerCollider.bounds.extents.y);
        if(Vector2.Distance(nearest,p)>radius+pr)return false;
        player.TakeDamage(1);nextDamage=Time.time+.65f;return true;
    }
    private IEnumerator Move(Transform tr,Vector3 a,Vector3 b,float duration)
    {
        float t=0;while(t<duration&&!dead){t+=Time.deltaTime;tr.position=Vector3.Lerp(a,b,Mathf.SmoothStep(0,1,t/duration));yield return null;}
        if(!dead)tr.position=b;
    }
    private IEnumerator ReturnHands(float duration)
    {
        Vector3 a=hands[0].root.position,b=hands[1].root.position;float t=0;
        while(t<duration&&!dead){t+=Time.deltaTime;float q=Mathf.SmoothStep(0,1,t/duration);hands[0].root.position=Vector3.Lerp(a,hands[0].home,q);hands[1].root.position=Vector3.Lerp(b,hands[1].home,q);yield return null;}
    }
    private GameObject Mark(Vector3 p,Vector2 size,Color color)
    {
        SpriteRenderer sr=RectSprite("WarningV25",transform,size,color,9);sr.transform.position=p;effects.Add(sr.gameObject);return sr.gameObject;
    }
    private LineRenderer Line(Vector3 a,Vector3 b,float width,Color color)
    {
        GameObject go=new GameObject("BeamV25");go.transform.SetParent(transform,false);effects.Add(go);
        LineRenderer lr=go.AddComponent<LineRenderer>();lr.useWorldSpace=true;lr.positionCount=2;lr.SetPosition(0,a);lr.SetPosition(1,b);
        lr.sharedMaterial=JHLArtV25.LineMaterial;lr.startWidth=lr.endWidth=width;lr.startColor=lr.endColor=color;lr.sortingOrder=18;return lr;
    }
    private void Impact(Vector3 p,float strength)
    {Sound("Impact",.65f*strength);Burst(p,new Color(.7f,.45f,1f),1.4f*strength);if(GameFeelManager.Instance!=null)GameFeelManager.Instance.DirectionalShake(.16f,.10f*strength,Vector2.down);}
    private void Burst(Vector3 p,Color color,float size){StartCoroutine(BurstRoutine(p,color,size));}
    private IEnumerator BurstRoutine(Vector3 p,Color color,float size)
    {
        GameObject go=Mark(p,new Vector2(size,size*.55f),color);float t=0;
        while(t<.18f&&go!=null){t+=Time.deltaTime;go.transform.localScale=new Vector3(size*(1+t*5),size*.55f*(1+t*5),1);SpriteRenderer sr=go.GetComponent<SpriteRenderer>();color.a=1-t/.18f;sr.color=color;yield return null;}
        Remove(go);
    }
    private void Remove(GameObject go){if(go==null)return;effects.Remove(go);Destroy(go);}
    private void ClearEffects(){foreach(GameObject go in effects)if(go!=null)Destroy(go);effects.Clear();}
    private void SetHitboxes(bool value){if(faceHurt!=null)faceHurt.enabled=value;foreach(Hand h in hands)if(h!=null)Vulnerable(h,false);}
    public bool Die(Vector2 direction)
    {
        if(dead)return false;dead=true;Pattern="종료";StopAllCoroutines();ReleaseInput();ClearEffects();SetHitboxes(false);StartCoroutine(Death(direction));return true;
    }
    private IEnumerator Death(Vector2 direction)
    {
        Sound("Break",.9f);hud.ReportHealth(0,MaxHP,true);music.CrossFadeBackToGameplay(1.2f);
        float t=0;while(t<1.2f){t+=Time.unscaledDeltaTime;Color c=new Color(1,1,1,Mathf.Clamp01(1-t/1.2f));faceArt.color=c;foreach(Hand h in hands)h.art.color=c;yield return null;}
        hud.HideAllImmediate();
        if(player!=null&&!player.IsDead)player.RestoreToFull();
        ByteDropper drop=GetComponent<ByteDropper>();if(drop==null)drop=gameObject.AddComponent<ByteDropper>();drop.ConfigureDrops(6,6,4);drop.DropBytes(arena.center);
        for(int i=0;i<3;i++)AmmoDropper.SpawnPickupAt(arena.center+Vector3.right*(i-1)*.3f,8,room);
        map.NotifyJHLDefeated(room);hp.CompleteDeferredDeath(direction);
    }
    private int forced=-1;
    public void DebugCycle(int delta){selected=(selected+delta+6)%6;hud.ShowSystem("패턴 선택: "+selected);}
    public void DebugForce(){forced=selected;}
    public void DebugStart(float ratio)
    {
        StopAllCoroutines();ReleaseInput();ClearEffects();dead=false;started=true;exposed=false;posture=0;SetHitboxes(true);hp.ResetHealth();
        resetting=true;hp.TakeDamage(Mathf.Max(0,Mathf.RoundToInt(MaxHP*(1-Mathf.Clamp(ratio,.02f,1f)))));resetting=false;
        face.position=faceHome;foreach(Hand h in hands){h.root.position=h.home;h.shadow.transform.localPosition=Vector3.zero;Pose(h,0);h.art.color=Color.white;}SetArt(faceArt,6,3.5f);
        hud.ReportHealth(hp.CurrentHealth,MaxHP,true);hud.ShowBossBar(true);phase=hp.NormalizedHealth>.7f?1:hp.NormalizedHealth>.35f?2:3;music.PlayPhase(phase);StartCoroutine(Combat());
    }
    private void ReleaseInput(){if(inputLock!=null){inputLock.Dispose();inputLock=null;}}
    private void OnDisable(){StopAllCoroutines();ReleaseInput();ClearEffects();SetHitboxes(false);}
    private void OnDestroy(){if(hp!=null)hp.Damaged-=Damaged;if(hud!=null)Destroy(hud.gameObject);if(music!=null&&!dead)music.RestoreGameplayImmediate();}
}
