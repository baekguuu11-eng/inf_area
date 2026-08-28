// TARGET: Assets/Scripts/Systems/CameraRecoil.cs
using UnityEngine;

[DisallowMultipleComponent]
public class CameraRecoil : MonoBehaviour
{
    [Header("Ranged Weapon Recoil")]
    [SerializeField] private float recoilStrength = 0.10f;
    [SerializeField] private float microShakeDuration = 0.045f;
    [SerializeField] private float microShakeMagnitude = 0.022f;

    private CameraFeedbackController feedback;

    private void Awake()
    {
        feedback = GetComponent<CameraFeedbackController>();
        if (feedback == null)
            feedback = gameObject.AddComponent<CameraFeedbackController>();
    }

    public void TriggerRecoil(Vector2 shootDirection)
    {
        if (feedback == null)
            feedback = GetComponent<CameraFeedbackController>();
        if (feedback == null)
            return;

        feedback.AddRecoil(shootDirection, recoilStrength);
        feedback.Shake(microShakeDuration, microShakeMagnitude, -shootDirection);
    }
}
