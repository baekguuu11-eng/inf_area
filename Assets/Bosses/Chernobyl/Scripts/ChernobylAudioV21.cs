using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChernobylAudioV21 : MonoBehaviour
{
    private readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
    private readonly Dictionary<AudioClip,float> last=new Dictionary<AudioClip,float>();
    private readonly AudioSource[] voices=new AudioSource[6];
    private AudioSource steam;
    private float steamTarget;
    private int blockedIndex;
    public void Initialize()
    {
        foreach(string name in new[]{"ArmorA","ArmorB","CoreHit","GridBlast","HatchOpen","HatchClose","SteamLoop","Restart","ShockwaveMix","Death"})
            clips[name]=Resources.Load<AudioClip>("Bosses/Chernobyl/UserSFXV21/"+name);
        for(int i=0;i<voices.Length;i++) voices[i]=Source();
        steam=Source(); steam.clip=Clip("SteamLoop"); steam.loop=true; steam.volume=0f;
    }
    private AudioSource Source()
    {
        AudioSource s=gameObject.AddComponent<AudioSource>(); s.playOnAwake=false;
        s.spatialBlend=0f; s.ignoreListenerPause=false; s.priority=80; return s;
    }
    public AudioClip Clip(string name) { return clips.TryGetValue(name,out AudioClip clip)?clip:null; }
    public void PlayNamed(string name,float volume) { Play(Clip(name),volume,1f); }
    public void Blocked() { PlayNamed((blockedIndex++%2)==0?"ArmorA":"ArmorB",.28f); }
    public void Play(AudioClip clip,float volume,float pitch)
    {
        if(clip==null) return;
        int offset=(clip==Clip("GridBlast"))?0:(clip==Clip("ArmorA") || clip==Clip("ArmorB") || clip==Clip("CoreHit"))?2:4;
        float interval=offset==0?.14f:offset==2?.09f:.12f;
        if(last.TryGetValue(clip,out float at) && Time.time-at<interval) return;
        last[clip]=Time.time;
        AudioSource v=voices[offset];
        if(v.isPlaying) v=voices[offset+1];
        if(v.isPlaying && offset!=4) return; // Preserve transients; never stack unbounded tile voices.
        v.Stop(); v.clip=clip; v.pitch=clip==Clip("CoreHit")?(pitch<.96f?.749f:.794f):Mathf.Clamp(pitch,.9f,1.1f);
        v.volume=Mathf.Clamp01(volume)*.8f; v.Play();
    }
    public void Steam(bool active)
    {
        steamTarget=active?.18f:0f;
        if(active && steam!=null && steam.clip!=null && !steam.isPlaying) steam.Play();
    }
    private void Update()
    {
        if(steam==null) return;
        steam.volume=Mathf.MoveTowards(steam.volume,steamTarget,Time.deltaTime*.8f);
        if(steamTarget==0f && steam.volume<=0f) steam.Stop();
    }
    public void StopAll()
    {
        steamTarget=0f;
        if(steam!=null) { steam.Stop(); steam.volume=0f; }
        foreach(AudioSource s in voices) if(s!=null) s.Stop();
    }
    private void OnDisable() { StopAll(); }
}
