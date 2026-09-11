using UnityEngine;

// V4 compatibility shell. JHL debug controls now follow the same MapManager hotkey style
// as Executor/Chernobyl; no F12 IMGUI window is created or read here.
[DisallowMultipleComponent]
public sealed class JHLDebugController : MonoBehaviour
{
    public void Initialize(JHLBossController target) { }
}
