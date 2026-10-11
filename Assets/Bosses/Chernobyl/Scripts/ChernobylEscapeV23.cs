using System.Collections.Generic;
using UnityEngine;
public sealed partial class ChernobylBossController
{
    // A bounded time-expanded movement search. It never edits a committed or proposed attack mask.
    // Uses walking only, including a reaction delay; dash is additional player freedom.
    private bool HasEscapeV23(BlastBatchV20 candidate,List<BlastBatchV20> pending,Bounds arena,Vector2 tile,int cols,int rows)
    {
        if(playerHealth==null) return true;
        Vector2 origin=playerHealth.transform.position, half=new Vector2(.2f,.2f), offset=Vector2.zero;
        bool first=true;Bounds playerBounds=new Bounds();
        foreach(Collider2D col in playerHealth.GetComponentsInChildren<Collider2D>())
        {
            if(!col.enabled || col.isTrigger) continue;
            if(first) {playerBounds=col.bounds;first=false;} else playerBounds.Encapsulate(col.bounds);
        }
        if(!first) {half=playerBounds.extents;offset=(Vector2)playerBounds.center-origin;}
        half+=Vector2.one*.035f;
        PlayerStats stats=playerHealth.GetComponent<PlayerStats>();float speed=stats!=null?stats.MoveSpeed:5f;
        speed=Mathf.Max(.1f,speed);
        float now=Time.time,end=candidate.due;
        foreach(var b in pending) end=Mathf.Max(end,b.due);
        List<BlastBatchV20> all=new List<BlastBatchV20>(pending);all.Add(candidate);
        // Small enough steps to represent exits within a tile, not only tile centres.
        float stride=Mathf.Min(.32f,Mathf.Min(tile.x,tile.y)*.35f), dt=stride/speed;
        Bounds obstacle=solidCollider!=null?solidCollider.bounds:new Bounds(new Vector3(99999,99999,0),Vector3.zero);
        obstacle.Expand(new Vector3(half.x*2,half.y*2,2));
        HashSet<Vector2Int> reachable=new HashSet<Vector2Int>{Vector2Int.zero};
        Vector2Int[] moves={Vector2Int.zero,Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down};
        for(float at=now;at<=end+.001f;at+=dt)
        {
            HashSet<Vector2Int> next=new HashSet<Vector2Int>();
            foreach(Vector2Int cell in reachable)
            {
                Vector2 from=origin+(Vector2)cell*stride;
                // Once waiting here survives every remaining detonation, a valid path exists.
                bool canWait=true;
                foreach(var b in all) if(b.due>=at && TileThreatV23(b,from+offset,half,arena,tile,cols,rows)) {canWait=false;break;}
                if(canWait) return true;
                foreach(Vector2Int move in moves)
                {
                    if(at-now<.16f && move!=Vector2Int.zero) continue;
                    Vector2Int key=cell+move;if(next.Contains(key)) continue;
                    Vector2 to=origin+(Vector2)key*stride,center=to+offset;
                    if(center.x-half.x<arena.min.x || center.x+half.x>arena.max.x || center.y-half.y<arena.min.y || center.y+half.y>arena.max.y) continue;
                    Vector3 a=new Vector3(from.x+offset.x,from.y+offset.y,obstacle.center.z),z=new Vector3(center.x,center.y,obstacle.center.z);
                    Vector3 d=z-a;
                    if(obstacle.Contains(z) || (d.sqrMagnitude>.00001f && obstacle.IntersectRay(new Ray(a,d.normalized),out float distance) && distance<=d.magnitude)) continue;
                    bool safe=true;
                    foreach(var b in all)
                    {
                        if(b.due<at || b.due>at+dt) continue;
                        Vector2 atBlast=Vector2.Lerp(from,to,Mathf.Clamp01((b.due-at)/dt))+offset;
                        if(TileThreatV23(b,atBlast,half,arena,tile,cols,rows)) {safe=false;break;}
                    }
                    if(safe) next.Add(key);
                }
            }
            if(next.Count==0) return false;
            reachable=next;
        }
        return reachable.Count>0;
    }
    private static bool TileThreatV23(BlastBatchV20 b,Vector2 position,Vector2 half,Bounds arena,Vector2 tile,int cols,int rows)
    {
        int minX=Mathf.Clamp(Mathf.FloorToInt((position.x-half.x-arena.min.x)/tile.x),0,cols-1);
        int maxX=Mathf.Clamp(Mathf.FloorToInt((position.x+half.x-arena.min.x)/tile.x),0,cols-1);
        int minY=Mathf.Clamp(Mathf.FloorToInt((position.y-half.y-arena.min.y)/tile.y),0,rows-1);
        int maxY=Mathf.Clamp(Mathf.FloorToInt((position.y+half.y-arena.min.y)/tile.y),0,rows-1);
        for(int y=minY;y<=maxY;y++) for(int x=minX;x<=maxX;x++) if(b.mask.Contains(y*cols+x)) return true;
        return false;
    }
    private bool AdmitBlastV23(BlastBatchV20 batch,List<BlastBatchV20> pending,Bounds arena,Vector2 size,int cols,int rows,float maxLead)
    {
        foreach(int tile in batch.tiles) batch.mask.Add(tile);
        for(int attempt=0;attempt<4;attempt++)
        {
            if(batch.due-Time.time>maxLead+.001f) return false;
            if(HasEscapeV23(batch,pending,arena,size,cols,rows)) return true;
            batch.due+=.18f; // Extend the unshown warning, never erase the player's tile.
        }
        return false;
    }
}
