using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class ParticleSlowMotion : MonoBehaviour
{
    private ParticleSystem ps;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
    }

    private void Update()
    {
        float timeScale = SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;
        var main = ps.main;
        main.simulationSpeed = timeScale;
    }
}
