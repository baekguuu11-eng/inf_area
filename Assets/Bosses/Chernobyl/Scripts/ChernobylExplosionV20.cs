using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class ChernobylBossController
{
    private readonly AudioClip[] teamAudioV20 = new AudioClip[30];
    private float lastBlastSoundV20=-10f;
    private sealed class BlastBatchV20
    {
        public float due;
        public bool secondary;
        public readonly HashSet<int> mask=new HashSet<int>();
        public readonly List<int> tiles=new List<int>();
        public readonly List<GameObject> marks=new List<GameObject>();
    }
    private void LoadTeamAudioV20()
    {
        audioV21=gameObject.AddComponent<ChernobylAudioV21>();
        audioV21.Initialize();
        hitSfxV11=heavyHitSfxV11=audioV21.Clip("CoreHit");
        explosionSfx=gridExplosionSfxV11=teamAudioV20[17]=audioV21.Clip("GridBlast");
        activationSfx=pageTransitionSfxV11=audioV21.Clip("Restart");
        overloadSfx=shockwaveChargeSfxV11=antiBurstChargeSfxV11=null;
        gridWarningSfxV11=null;
        shockwaveFireSfxV11=antiBurstFireSfxV11=audioV21.Clip("ShockwaveMix");
        shutdownSfx=audioV21.Clip("Death");
    }
    private IEnumerator ExplosionCycleV20()
    {
        int cols=phase==1?6:phase==2?8:10,rows=phase==1?4:phase==2?6:8;
        Bounds arena=GetArenaBounds();Vector2 size=new Vector2(arena.size.x/cols,arena.size.y/rows);
        Vector3[] centers=new Vector3[cols*rows];
        for(int y=0;y<rows;y++) for(int x=0;x<cols;x++) centers[y*cols+x]=new Vector3(arena.min.x+(x+.5f)*size.x,arena.min.y+(y+.5f)*size.y,0);
        float duration=phase==1?16f:phase==2?20f:24f,started=Time.time,next=started,nextTarget=started+1.7f;
        int id=NextThermalPatternV19(),step=0,variant=Random.Range(0,4);
        List<BlastBatchV20> pending=new List<BlastBatchV20>();
        while(!dead && (playerHealth==null || !playerHealth.IsDead) && (Time.time-started<duration || pending.Count>0))
        {
            float now=Time.time,heat=Mathf.Clamp01((now-started)/duration);SetThermalV19(heat,0,false);
            bool hit=false;int exploded=0;
            for(int i=0;i<pending.Count;)
            {
                var batch=pending[i];if(now<batch.due) {i++;continue;}
                foreach(var mark in batch.marks) ForgetAndDestroy(mark);
                int detailStride=Mathf.Max(1,Mathf.CeilToInt(batch.tiles.Count/12f));
                for(int n=0;n<batch.tiles.Count;n++)
                {
                    int cell=batch.tiles[n];RegisterSpawnedObject(ChernobylGridVisualV21.Blast(centers[cell],size,n%detailStride==0));
                    if(PlayerInsideRect(centers[cell],size)) hit=true;
                }
                exploded+=batch.tiles.Count;pending.RemoveAt(i);
            }
            if(hit) DamagePlayerOnce(1);
            if(exploded>0 && now-lastBlastSoundV20>=.12f)
            {
                PlaySfx(teamAudioV20[17],.38f,1f);lastBlastSoundV20=now;
                if(cameraFxV10!=null) cameraFxV10.PulseExplosion(Mathf.Min(.38f,.10f+exploded*.015f));
            }
            // End with the last valid blast, then release the whole arena into overheat.
            float left=duration-(now-started);
            if(now>=next && left>1.05f)
            {
                float lead=phase==1?.97f:phase==2?.88f:.81f;
                if(id>=8) lead+=.16f;
                BlastBatchV20 batch=BuildBlastV23(id,step,variant,cols,rows,arena,size,now+lead);
                if(batch.tiles.Count==0) {step++;next=now+.02f;}
                else if(AdmitBlastV23(batch,pending,arena,size,cols,rows,Mathf.Min(1.55f,left)))
                {
                    CommitBlastV23(batch,pending,centers,size);step++;
                    next=now+(phase==1?.625f:phase==2?.5375f:Mathf.Lerp(.525f,.375f,heat));
                }
                else next=now+.12f; // Keep the main step; allow committed attacks to clear first.
                int total=ChernobylPatternsV23.Steps(id,cols,rows,variant);
                if(step>=total) {step=0;id=NextThermalPatternV19();variant=Random.Range(0,4);}
            }
            if(now>=nextTarget && left>1.4f)
            {
                // Secondary lock is only used if standing still is currently safe.
                int px=Mathf.Clamp(Mathf.FloorToInt((GetPlayerPosition().x-arena.min.x)/size.x),0,cols-1);
                int py=Mathf.Clamp(Mathf.FloorToInt((GetPlayerPosition().y-arena.min.y)/size.y),0,rows-1);
                bool covered=false,hasTarget=false;
                foreach(var b in pending) {if(b.mask.Contains(py*cols+px)) covered=true;if(b.secondary) hasTarget=true;}
                if(!covered && !hasTarget)
                {
                    var target=BuildBlastV23(3,0,0,cols,rows,arena,size,now+(phase==3?.94f:1.1f));target.secondary=true;
                    if(AdmitBlastV23(target,pending,arena,size,cols,rows,Mathf.Min(1.55f,left))) CommitBlastV23(target,pending,centers,size);
                }
                nextTarget=now+(phase==1?3.25f:phase==2?2.625f:Mathf.Lerp(2.25f,1.5625f,heat));
            }
            yield return null;
        }
        foreach(var batch in pending) foreach(var mark in batch.marks) ForgetAndDestroy(mark);
    }
    private BlastBatchV20 BuildBlastV23(int id,int step,int variant,int cols,int rows,Bounds arena,Vector2 size,float due)
    {
        var b=new BlastBatchV20 {due=due};
        int px=Mathf.Clamp(Mathf.FloorToInt((GetPlayerPosition().x-arena.min.x)/size.x),0,cols-1);
        int py=Mathf.Clamp(Mathf.FloorToInt((GetPlayerPosition().y-arena.min.y)/size.y),0,rows-1);
        for(int y=0;y<rows;y++) for(int x=0;x<cols;x++)
            if(ChernobylPatternsV23.Active(id,step,variant,x,y,cols,rows,px,py)) b.tiles.Add(y*cols+x);
        return b;
    }
    private void CommitBlastV23(BlastBatchV20 batch,List<BlastBatchV20> pending,Vector3[] centers,Vector2 size)
    {
        float seconds=batch.due-Time.time;
        foreach(int cell in batch.tiles)
        {
            var mark=ChernobylGridVisualV21.Warning(centers[cell],size,seconds);batch.marks.Add(mark);RegisterSpawnedObject(mark);
        }
        pending.Add(batch);
    }
}
