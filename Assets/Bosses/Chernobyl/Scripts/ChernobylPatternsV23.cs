using UnityEngine;
// Stable, side-effect-free tile masks. Every emitted mask is telegraphed before commitment.
public static class ChernobylPatternsV23
{
    public static int Count(int phase) {return phase==1?8:phase==2?13:18;}
    public static int Steps(int id,int cols,int rows,int variant)
    {
        switch(id)
        {
            case 0:return variant<2?Mathf.Min(6,cols):Mathf.Min(6,rows);
            case 1:return 7;
            case 2:return 2;
            case 3:return 3;
            case 4:case 5:return rows/2;
            case 6:return 6;
            case 7:return 4;
            case 8:return 4;
            case 9:case 10:case 11:case 12:return 6;
            case 13:return 7;
            case 14:return 8;
            case 15:return 6;
            case 16:return 8;
            default:return 6;
        }
    }
    public static int Family(int id)
    {
        if(id==1 || id==8 || id==9 || id==10 || id==13) return 1;
        if(id==4 || id==5 || id==17) return 4;
        if(id==6 || id==12 || id==14 || id==15) return 6;
        if(id==7 || id==16) return 7;
        return id;
    }
    public static bool Active(int id,int step,int variant,int x,int y,int cols,int rows,int px,int py)
    {
        // Mirroring changes the entire path, not individual random tiles.
        if((variant&1)!=0) {x=cols-1-x;px=cols-1-px;}
        if((variant&2)!=0) {y=rows-1-y;py=rows-1-py;}
        int layer=Mathf.Min(Mathf.Min(x,cols-1-x),Mathf.Min(y,rows-1-y));
        int quadrant=y>=rows/2?(x<cols/2?0:1):(x>=cols/2?2:3);
        int diagonal=x+y;
        int diagBand=Mathf.Min(6,diagonal*7/(cols+rows-1));
        switch(id)
        {
            case 0: return variant<2?x*Mathf.Min(6,cols)/cols==step:y*Mathf.Min(6,rows)/rows==step;
            case 1: return diagBand==step;
            case 2: return (x+y)%2==step;
            case 3: return x==px && y==py;
            case 4: return layer==step;
            case 5: return layer==rows/2-1-step;
            case 6: // Alternating horizontal pipes joined at their ends, spanning the arena.
                return step<3?y==rows/3 && x*3/cols==step:
                    (x==cols-1 && y>=rows/3 && y<=rows*2/3) || (y==rows*2/3 && (cols-1-x)*3/cols==step-3);
            case 7:return quadrant==step;
            case 8: // Two slanted ribbons, each followed by an offset ribbon.
                return step<2?Mathf.Abs(x*rows-y*cols-(step==0?-cols:cols))<cols:
                    Mathf.Abs((cols-1-x)*rows-y*cols-(step==2?-cols:cols))<cols;
            case 9: // V branches unfold from the lower centre.
                return y*6/rows==step && Mathf.Abs(Mathf.Abs(x-(cols-1)*.5f)-(y+.5f)*cols/(2f*rows))<1.1f;
            case 10:return (x+y)%3==step%3 && (step<3?x<cols*2/3:x>=cols/3);
            case 11:
                int corridor=step%(cols-1);
                return x<corridor || x>corridor+1;
            case 12: // Hook: top sweep, side descent, return across bottom.
                return step<2?y>=rows-2 && x*2/cols==step:
                    step<4?x>=cols-2 && (rows-1-y)*2/rows==step-2:
                    y<2 && (cols-1-x)*2/cols==step-4;
            case 13:return diagBand==step || diagBand==6-step;
            case 14:
                int edge=layer;
                int perimeter=x==edge?0:y==rows-1-edge?1:x==cols-1-edge?2:3;
                return edge==step/4 && perimeter==step%4;
            case 15:
                return step<2?Mathf.Abs(x-cols/2)<=0 && y*2/rows==step:
                    y==rows/2 && Mathf.Abs(x-cols/2)==step-2 ||
                    step>=4 && (x==cols/4 || x==cols*3/4) && y*2/rows==step-4;
            case 16:return quadrant==step%4 && (step<4 || (x+y)%2==step%2);
            default:
                return step<2?layer==step:step<4?Mathf.Abs(x*rows-y*cols)<cols && (x<cols/2)==(step==2):
                    step==4?x==cols/2 || y==rows/2:layer==0;
        }
    }
}
