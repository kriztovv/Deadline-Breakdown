using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Switches a Shader Graph pulse on/off by driving a float property (0 = off, 1 = on).
/// Put it on the parent object. It finds the child renderers whose shader has the property
/// and ignores all the others. Fades smoothly instead of snapping.
/// </summary>
public class ShaderPulseHighlight : MonoBehaviour
{
    [SerializeField] string propertyReference = "_Active"; // Reference name from the Graph Inspector, NOT the display name
    [SerializeField] float fadeSpeed = 4f;                 // higher = faster fade (1 / seconds)

    [Tooltip("Optional. Leave empty to auto-find every child renderer whose shader has the property.")]
    [SerializeField] Renderer[] targets;

    readonly List<Renderer> pulsing = new List<Renderer>();
    int propertyId;
    MaterialPropertyBlock block;
    float current;
    float goal;

    void Awake()
    {
        propertyId = Shader.PropertyToID(propertyReference);
        block = new MaterialPropertyBlock();

        var source = (targets != null && targets.Length > 0)
            ? targets
            : GetComponentsInChildren<Renderer>(true);

        foreach (var r in source)
        {
            if (r == null) continue;

            foreach (var mat in r.sharedMaterials)
            {
                if (mat != null && mat.HasProperty(propertyId))
                {
                    pulsing.Add(r);
                    break;
                }
            }
        }

        if (pulsing.Count == 0)
            Debug.LogWarning($"{name}: no renderer found with property '{propertyReference}'.", this);

        Apply(0f);
        enabled = false; // Update only runs while fading
    }

    public void SetActive(bool on)
    {
        goal = on ? 1f : 0f;
        enabled = true;
    }

    void Update()
    {
        current = Mathf.MoveTowards(current, goal, fadeSpeed * Time.deltaTime);
        Apply(current);

        if (Mathf.Approximately(current, goal))
            enabled = false; // fade finished; the pulse itself runs in the shader
    }

    void Apply(float value)
    {
        foreach (var r in pulsing)
        {
            r.GetPropertyBlock(block);
            block.SetFloat(propertyId, value);
            r.SetPropertyBlock(block);
        }
    }
}