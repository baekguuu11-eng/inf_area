using System.Collections;
using UnityEngine;
public sealed partial class ChernobylBossController
{
    private bool phasePresentationV22;
    private IEnumerator RestartCueV22(bool shockwave)
    {
        GameObject cue=null;
        if(shockwave) {cue=new GameObject("ShockwavePreparationV22");RegisterSpawnedObject(cue);cue.transform.position=transform.position;}
        SpriteRenderer[] ticks=new SpriteRenderer[24];
        if(cue!=null) for(int i=0;i<ticks.Length;i++)
        {
            GameObject g=new GameObject("ChargeTick");g.transform.SetParent(cue.transform,false);
            ticks[i]=g.AddComponent<SpriteRenderer>();ticks[i].sprite=ChernobylRuntimeSprites.WhitePixel;ticks[i].sortingOrder=15;
        }
        float age=0f;
        while(age<1.2f && !dead)
        {
            age+=Time.deltaTime;
            SetThermalV19(1f,age<.9f?1f:1f-Mathf.Clamp01((age-.9f)/.3f),age<.9f);
            if(cue!=null) for(int i=0;i<ticks.Length;i++)
            {
                float a=i*Mathf.PI*2/ticks.Length;
                float radius=Mathf.Lerp(2.1f,1.35f,Mathf.Clamp01(age/.9f));
                ticks[i].transform.localPosition=new Vector3(Mathf.Cos(a),Mathf.Sin(a))*radius;
                ticks[i].transform.localRotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg+90f);
                ticks[i].transform.localScale=new Vector3((age>.9f?.40f:.17f)/ticks[i].sprite.bounds.size.x,.055f/ticks[i].sprite.bounds.size.y,1);
                ticks[i].color=age>.9f?new Color(1f,.9f,.5f):new Color(1f,.4f,.08f,.8f);
            }
            yield return null;
        }
        if(audioV21!=null) audioV21.PlayNamed("HatchClose",.42f);
        if(cue!=null) ForgetAndDestroy(cue);
    }
    private IEnumerator PhaseCutsceneV22(int target)
    {
        phasePresentationV22=true;
        CleanupSpawnedObjects();ClearAllGridWarningsV11();SetThermalV19(1f,1f,false);
        inputLock=GameInputState.Acquire("ChernobylPhaseV22");
        float duration=target==2?2.2f:2.6f;
        if(playerHealth!=null)
        {
            playerHealth.GrantTemporaryInvulnerability(duration+.4f);
            PlayerDashController dash=playerHealth.GetComponent<PlayerDashController>();if(dash!=null) dash.CancelForExternalForce();
            PlayerAmmoController ammo=playerHealth.GetComponent<PlayerAmmoController>();if(ammo!=null) ammo.CancelReload();
            playerHealth.SendMessage("CancelCurrentActionForDash",SendMessageOptions.DontRequireReceiver);
            Rigidbody2D rb=playerHealth.GetComponent<Rigidbody2D>();if(rb!=null) rb.linearVelocity=Vector2.zero;
        }
        if(audioV21!=null) {audioV21.Steam(false);audioV21.PlayNamed("HatchClose",.42f);}
        if(music!=null) music.SetDefeatMix(.12f,.7f);
        Vector3 original=visualRoot!=null?visualRoot.localPosition:Vector3.zero;
        bool switched=false;float age=0f;
        try
        {
            while(age<duration && !dead)
            {
                age+=Time.deltaTime;float t=Mathf.Clamp01(age/duration);
                if(cameraFxV10!=null) cameraFxV10.SetCutsceneZoomV22(.05f*Mathf.Sin(t*Mathf.PI));
                SetThermalV19(Mathf.Clamp01(1f-t*.6f),1f-Mathf.Clamp01(age/.4f),false);
                if(visualRoot!=null) visualRoot.localPosition=original+Vector3.right*(Mathf.Sin(age*65f)*.022f*Mathf.Sin(t*Mathf.PI));
                if(!switched && age>=.8f)
                {
                    switched=true;phase=target;ApplyPhaseVisuals();NotifyGridReconfiguredV11();
                    if(cameraFxV10!=null) cameraFxV10.SetPhase(phase);
                    if(music!=null) music.PlayPhaseTwo(1.1f); // Existing phase-two playhead survives phase three.
                    if(audioV21!=null) audioV21.PlayNamed("Restart",.48f);
                }
                yield return null;
            }
        }
        finally
        {
            phasePresentationV22=false;
            if(visualRoot!=null) visualRoot.localPosition=original;
            if(cameraFxV10!=null) cameraFxV10.SetCutsceneZoomV22(0);
            if(inputLock!=null) {inputLock.Dispose();inputLock=null;}
        }
        SetThermalV19(0,0,false);
        yield return WaitCombatSeconds(.4f);
    }
}
