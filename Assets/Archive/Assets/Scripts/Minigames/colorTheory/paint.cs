using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Put this on the (invisible) paintbrush object. It no longer cares how it is moved:
/// paintSpawner moves the object to the pointer and calls BeginDrag / EndDrag / Lock.
/// While a drag is active it leaves a stroke behind: a ribbon mesh with the brush texture
/// stretched (or tiled) along the path.
///
/// Brush object setup:
///  - no SpriteRenderer (or a disabled one) so it stays invisible
///  - brush texture should be WHITE (or greyscale) with alpha, because the stroke color is a tint
public class paint : MonoBehaviour
{
    [Header("Brush")]
    [SerializeField] private Texture2D brushTexture;           // alpha PNG, long axis left -> right
    [SerializeField] private Vector2 tipOffset;                // where the paint comes out, in the brush's local space
    [SerializeField] private float width = 0.6f;               // world units of the minigame scene
    [SerializeField] private float minPointDistance = 0.1f;    // spacing of recorded points
    [SerializeField, Range(1, 12)] private int subdivisions = 6;

    [Header("Texture")]
    [SerializeField] private bool stretchTexture = true;       // true: one texture over the stroke, false: tile
    [SerializeField] private float tileLength = 2f;            // world units per repeat when tiling

    [Header("Taper")]
    [SerializeField] private float taperLength = 1f;           // distance over which the ends narrow, 0 = no taper
    [SerializeField, Range(0f, 1f)] private float endWidth = 0.3f;   // width factor at the very ends

    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int baseSortingOrder = 0;         // each new stroke goes one above the last
    [SerializeField] private float strokeZ = 0f;

    private readonly List<Vector3> _raw = new List<Vector3>();
    private readonly List<GameObject> _strokes = new List<GameObject>();
    private Mesh _mesh;
    private Material _material;
    private Color _color = Color.white;
    private bool _painting;
    private bool _locked;

    // shared by every brush: each new stroke gets the next number, so it is drawn above all earlier ones
    private static int s_nextSortingOrder;

    public bool IsLocked => _locked;
    public bool IsPainting => _painting;

    /// Call when the board is cleared, so the counter doesn't keep growing.
    public static void ResetSortingOrder()
    {
        s_nextSortingOrder = 0;
    }

    private void Awake()
    {
        if (brushTexture != null)
            brushTexture.wrapMode = stretchTexture ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        _material = new Material(Shader.Find("Sprites/Default")) { mainTexture = brushTexture };
        _material.color = _color;
    }

    private void OnDisable()
    {
        _painting = false;
    }

    // LateUpdate: the spawner has moved the brush in Update by now
    private void LateUpdate()
    {
        if (_painting) AddPoint(TipPosition(), false);
    }

    // ---------- public API (used by paintSpawner) ----------

    public void SetColor(Color color)
    {
        _color = color;
        if (_material != null) _material.color = color;
    }

    /// Resets the previous stroke and starts a fresh one at the brush's current position.
    public void BeginDrag()
    {
        if (_locked) return;
        ClearStrokes();
        BeginStroke();
        AddPoint(TipPosition(), true);
    }

    /// Stops painting. keepStroke = false also erases what was painted.
    public void EndDrag(bool keepStroke)
    {
        if (_painting) AddPoint(TipPosition(), true);   // finish exactly where the brush is
        _painting = false;
        if (!keepStroke) ClearStrokes();
    }

    /// Called when the color is completed: the stroke stays, but the brush can't paint anymore.
    public void Lock()
    {
        _locked = true;
        _painting = false;
    }

    public void ClearStrokes()
    {
        foreach (GameObject s in _strokes)
        {
            if (s == null) continue;
            Destroy(s.GetComponent<MeshFilter>().sharedMesh);
            Destroy(s);
        }
        _strokes.Clear();
        _painting = false;
    }

    // ---------- stroke building ----------

    private Vector3 TipPosition()
    {
        Vector3 p = transform.TransformPoint(tipOffset);
        p.z = strokeZ;
        return p;
    }

    private void BeginStroke()
    {
        _painting = true;
        _raw.Clear();

        var go = new GameObject("Stroke");
        SceneManager.MoveGameObjectToScene(go, gameObject.scene);   // keep it in the minigame scene

        _mesh = new Mesh { name = "StrokeMesh" };
        _mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = _mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _material;
        mr.sortingLayerName = sortingLayerName;
        mr.sortingOrder = Mathf.Min(baseSortingOrder + s_nextSortingOrder++, 32000);   // sorting order is limited to 16 bits

        _strokes.Add(go);
    }

    private void AddPoint(Vector3 p, bool force)
    {
        if (_raw.Count > 0)
        {
            float sq = (p - _raw[_raw.Count - 1]).sqrMagnitude;
            float min = force ? 0.0001f * 0.0001f : minPointDistance * minPointDistance;
            if (sq < min) return;
        }
        _raw.Add(p);
        if (_raw.Count >= 2) RebuildMesh();
    }

    // Catmull-Rom smoothing so the path is curved, not jagged.
    private List<Vector3> Smooth(List<Vector3> pts)
    {
        var result = new List<Vector3>();
        int n = pts.Count;
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 p0 = pts[Mathf.Max(i - 1, 0)];
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[i + 1];
            Vector3 p3 = pts[Mathf.Min(i + 2, n - 1)];
            for (int s = 0; s < subdivisions; s++)
            {
                float t = s / (float)subdivisions;
                float t2 = t * t, t3 = t2 * t;
                result.Add(0.5f * ((2f * p1) + (-p0 + p2) * t
                    + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                    + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        result.Add(pts[n - 1]);
        return result;
    }

    private void RebuildMesh()
    {
        List<Vector3> path = Smooth(_raw);
        int n = path.Count;

        var dist = new float[n];
        for (int i = 1; i < n; i++) dist[i] = dist[i - 1] + Vector3.Distance(path[i], path[i - 1]);
        float total = Mathf.Max(dist[n - 1], 0.0001f);

        var verts = new Vector3[n * 2];
        var uvs = new Vector2[n * 2];
        var tris = new int[(n - 1) * 6];

        for (int i = 0; i < n; i++)
        {
            Vector3 tangent = (path[Mathf.Min(i + 1, n - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
            Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f);

            // narrow towards both ends, measured in world units so already-drawn parts don't change width
            float k = 1f;
            if (taperLength > 0f)
                k = Mathf.Lerp(endWidth, 1f, Mathf.Clamp01(Mathf.Min(dist[i], total - dist[i]) / taperLength));

            float half = 0.5f * width * k;
            verts[2 * i]     = path[i] - normal * half;
            verts[2 * i + 1] = path[i] + normal * half;

            float u = stretchTexture ? dist[i] / total : dist[i] / tileLength;
            uvs[2 * i]     = new Vector2(u, 0f);
            uvs[2 * i + 1] = new Vector2(u, 1f);
        }

        for (int i = 0; i < n - 1; i++)
        {
            int a = 2 * i, b = a + 1, c = a + 2, d = a + 3;
            int t = i * 6;
            tris[t] = a; tris[t + 1] = b; tris[t + 2] = d;
            tris[t + 3] = a; tris[t + 4] = d; tris[t + 5] = c;
        }

        _mesh.Clear();
        _mesh.vertices = verts;
        _mesh.uv = uvs;
        _mesh.triangles = tris;
        _mesh.RecalculateBounds();
    }
}