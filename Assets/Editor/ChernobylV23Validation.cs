#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class ChernobylV23Validation
{
    [MenuItem("INFECTED AREA/Validation/V23 Pattern Contracts")]
    public static void Run()
    {
        int checkedMasks=0;
        for(int phase=1;phase<=3;phase++)
        {
            int cols=phase==1?6:phase==2?8:10,rows=phase==1?4:phase==2?6:8;
            int count=ChernobylPatternsV23.Count(phase);
            for(int id=0;id<count;id++) for(int variant=0;variant<4;variant++)
            {
                int steps=ChernobylPatternsV23.Steps(id,cols,rows,variant);var coverage=new HashSet<int>();
                for(int step=0;step<steps;step++)
                {
                    int tiles=0;
                    for(int y=0;y<rows;y++) for(int x=0;x<cols;x++)
                        if(ChernobylPatternsV23.Active(id,step,variant,x,y,cols,rows,cols/3,rows/3)) {tiles++;coverage.Add(y*cols+x);}
                    if(tiles==0 || tiles==cols*rows) throw new Exception("V23 invalid mask: phase="+phase+" id="+id+" variant="+variant+" step="+step+" tiles="+tiles);
                    checkedMasks++;
                }
                // Arena sweeps must actually reach every tile over their full sequence.
                if((id==0 || id==1 || id==2 || id==4 || id==5 || id==7 || id==13) && coverage.Count!=cols*rows)
                    throw new Exception("V23 incomplete arena coverage: "+phase+"/"+id+"/"+variant);
            }
            // Target locks always include the sampled player tile, including all mirror variants.
            for(int y=0;y<rows;y++) for(int x=0;x<cols;x++) for(int variant=0;variant<4;variant++)
                if(!ChernobylPatternsV23.Active(3,0,variant,x,y,cols,rows,x,y)) throw new Exception("V23 target lock missed its sampled tile");
        }
        Debug.Log("V23 pattern contract checks passed: "+checkedMasks+" masks. This does not replace a Play Mode movement/performance test.");
    }
}
#endif
