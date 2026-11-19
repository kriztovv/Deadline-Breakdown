using UnityEngine;

/// <summary>
/// Temporary test driver: pushes a value into the graph once per second.
/// Drag the Energy slider in the Inspector during Play mode, or tick Random Walk to let it wander.
/// Delete this once your real energy calculation calls EnergyGraph.PushSample.
/// </summary>
public class EnergyGraphTestFeeder : MonoBehaviour
{
    [SerializeField] EnergyGraph graph;
    [Range(0f, 100f)] public float energy = 20f;
    [SerializeField] bool randomWalk = false;
    [SerializeField] float walkStep = 10f;
    [Tooltip("Keep equal to the graph's Sample Interval.")]
    [SerializeField] float interval = 1f;

    float _timer;
    bool _started;

    void Update()
    {
        if (graph == null) return;

        _timer += Time.deltaTime;
        if (_started && _timer < interval) return;

        _timer = 0f;
        _started = true;

        if (randomWalk)
            energy = Mathf.Clamp(energy + Random.Range(-walkStep, walkStep), 0f, 100f);

        graph.PushSample(energy);
    }
}
