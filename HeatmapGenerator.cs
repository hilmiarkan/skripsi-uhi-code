using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Refactored HeatmapGenerator - focuses only on heatmap texture generation and display.
/// Hotspot creation delegated to HotspotManager, UI management to UIManager.
/// </summary>
public class HeatmapGenerator : MonoBehaviour
{
    [Header("Heatmap Targets")]
    [Tooltip("MeshRenderer untuk heatmap yang berbasis nilai fuzzy (Apparent Temperature).")]
    public MeshRenderer targetMeshRendererFuzzy;
    [Tooltip("MeshRenderer untuk heatmap yang berbasis nilai raw (Temperature_2m).")]
    public MeshRenderer targetMeshRendererRaw;

    [Header("Marker Parents")]
    [Tooltip("Parent Transform yang menampung marker-marker nilai fuzzy (Apparent Temperature).")]
    public Transform markerParentFuzzy;
    [Tooltip("Parent Transform yang menampung marker-marker nilai raw (Temperature_2m).")]
    public Transform markerParentRaw;

    [Header("Heatmap Settings")]
    [Tooltip("Lebar (pixel) dari setiap tekstur heatmap.")]
    public int textureWidth = 512;
    [Tooltip("Tinggi (pixel) dari setiap tekstur heatmap.")]
    public int textureHeight = 512;
    [Tooltip("Radius interpolasi (world units) untuk sebagian besar marker (default).")]
    public float baseInterpolationRadius = 100f;
    [Tooltip("Radius interpolasi (world units) untuk top 5 hottest marker (driver heat spots).")]
    public float hotspotInterpolationRadius = 150f;

    [Header("Heatmap Material")]
    [Tooltip("Material Unlit/Transparent (contoh: Unlit/Transparent colored).")]
    public Material heatmapMaterial;

    [Header("Heatmap Opacity")]
    [Range(0f, 1f)]
    [Tooltip("Opacity (alpha) dari heatmap overlay.")]
    public float heatmapAlpha = 0.5f;

    [Header("Animation Settings")]
    [Tooltip("Durasi (detik) untuk animasi Y saat show/hide heatmap.")]
    public float animationDuration = 0.5f;
    [Tooltip("Posisi Y lokal saat heatmap tersembunyi.")]
    public float hiddenY = -1f;
    [Tooltip("Posisi Y lokal saat heatmap muncul.")]
    public float visibleY = 0.31f;

    // Hard-coded gradient colors (hex):
    // #0896FF → #57EEEE → #60EC4D → #EE9418 → #EE405E
    private static readonly Color[] heatColors = new Color[]
    {
        new Color( 8f/255f, 150f/255f, 255f/255f ),   // #0896FF (coolest)
        new Color(87f/255f, 238f/255f, 238f/255f ),
        new Color(96f/255f, 236f/255f,  77f/255f ),
        new Color(238f/255f,148f/255f,  24f/255f ),
        new Color(238f/255f, 64f/255f,  94f/255f )    // #EE405E (hottest)
    };

    // Internal state
    private Texture2D heatmapTextureFuzzy = null;
    private Texture2D heatmapTextureRaw = null;
    private Material originalMaterialFuzzy = null;
    private Material originalMaterialRaw = null;
    private bool isShowingFuzzyHeatmap = false;
    private bool isShowingRawHeatmap = false;
    private Coroutine generateRoutineFuzzy = null;
    private Coroutine generateRoutineRaw = null;

    // Events for integration with new system
    public System.Action<List<Transform>> OnFuzzyHeatmapGenerated;
    public System.Action<List<Transform>> OnRawHeatmapGenerated;

    public static HeatmapGenerator Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeHeatmaps();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeHeatmaps()
    {
        // Initialize Fuzzy Heatmap
        if (targetMeshRendererFuzzy != null)
        {
            originalMaterialFuzzy = targetMeshRendererFuzzy.sharedMaterial;
            Vector3 posFuzzy = targetMeshRendererFuzzy.transform.localPosition;
            posFuzzy.y = hiddenY;
            targetMeshRendererFuzzy.transform.localPosition = posFuzzy;
            targetMeshRendererFuzzy.enabled = false;
        }

        // Initialize Raw Heatmap
        if (targetMeshRendererRaw != null)
        {
            originalMaterialRaw = targetMeshRendererRaw.sharedMaterial;
            Vector3 posRaw = targetMeshRendererRaw.transform.localPosition;
            posRaw.y = hiddenY;
            targetMeshRendererRaw.transform.localPosition = posRaw;
            targetMeshRendererRaw.enabled = false;
        }
    }

    // Public API for UI integration
    public void ToggleFuzzyHeatmap()
    {
        if (!isShowingFuzzyHeatmap)
            StartCoroutine(ShowFuzzyHeatmapRoutine());
        else
            StartCoroutine(HideFuzzyHeatmapRoutine());
    }

    public void ToggleRawHeatmap()
    {
        if (!isShowingRawHeatmap)
            StartCoroutine(ShowRawHeatmapRoutine());
        else
            StartCoroutine(HideRawHeatmapRoutine());
    }

    public bool IsFuzzyHeatmapVisible => isShowingFuzzyHeatmap;
    public bool IsRawHeatmapVisible => isShowingRawHeatmap;

    // Generate and show fuzzy heatmap
    private IEnumerator ShowFuzzyHeatmapRoutine()
    {
        Debug.Log("[HeatmapGenerator] Showing fuzzy heatmap...");

        // Generate texture if not exists
        if (heatmapTextureFuzzy == null)
        {
            if (generateRoutineFuzzy != null)
                StopCoroutine(generateRoutineFuzzy);
            generateRoutineFuzzy = StartCoroutine(GenerateFuzzyHeatmapAsync());

            while (generateRoutineFuzzy != null)
                yield return null;
        }

        // Apply material and animate
        if (targetMeshRendererFuzzy != null && heatmapMaterial != null && heatmapTextureFuzzy != null)
        {
            targetMeshRendererFuzzy.enabled = true;
            Color matColor = heatmapMaterial.color;
            matColor.a = heatmapAlpha;
            heatmapMaterial.color = matColor;
            heatmapMaterial.mainTexture = heatmapTextureFuzzy;
            targetMeshRendererFuzzy.sharedMaterial = heatmapMaterial;

            StartCoroutine(AnimateY(targetMeshRendererFuzzy.transform, hiddenY, visibleY, animationDuration));
        }

        isShowingFuzzyHeatmap = true;
        Debug.Log("[HeatmapGenerator] Fuzzy heatmap visible");
    }

    // Hide fuzzy heatmap
    private IEnumerator HideFuzzyHeatmapRoutine()
    {
        Debug.Log("[HeatmapGenerator] Hiding fuzzy heatmap...");

        if (targetMeshRendererFuzzy != null)
            yield return AnimateY(targetMeshRendererFuzzy.transform, visibleY, hiddenY, animationDuration);

        if (targetMeshRendererFuzzy != null)
        {
            targetMeshRendererFuzzy.sharedMaterial = originalMaterialFuzzy;
            targetMeshRendererFuzzy.enabled = false;
        }

        isShowingFuzzyHeatmap = false;
        Debug.Log("[HeatmapGenerator] Fuzzy heatmap hidden");
    }

    // Generate and show raw heatmap
    private IEnumerator ShowRawHeatmapRoutine()
    {
        Debug.Log("[HeatmapGenerator] Showing raw heatmap...");

        if (heatmapTextureRaw == null)
        {
            if (generateRoutineRaw != null)
                StopCoroutine(generateRoutineRaw);
            generateRoutineRaw = StartCoroutine(GenerateRawHeatmapAsync());

            while (generateRoutineRaw != null)
                yield return null;
        }

        if (targetMeshRendererRaw != null && heatmapMaterial != null && heatmapTextureRaw != null)
        {
            targetMeshRendererRaw.enabled = true;
            Color matColor = heatmapMaterial.color;
            matColor.a = heatmapAlpha;
            heatmapMaterial.color = matColor;
            heatmapMaterial.mainTexture = heatmapTextureRaw;
            targetMeshRendererRaw.sharedMaterial = heatmapMaterial;

            StartCoroutine(AnimateY(targetMeshRendererRaw.transform, hiddenY, visibleY, animationDuration));
        }

        isShowingRawHeatmap = true;
        Debug.Log("[HeatmapGenerator] Raw heatmap visible");
    }

    // Hide raw heatmap
    private IEnumerator HideRawHeatmapRoutine()
    {
        Debug.Log("[HeatmapGenerator] Hiding raw heatmap...");

        if (targetMeshRendererRaw != null)
            yield return AnimateY(targetMeshRendererRaw.transform, visibleY, hiddenY, animationDuration);

        if (targetMeshRendererRaw != null)
        {
            targetMeshRendererRaw.sharedMaterial = originalMaterialRaw;
            targetMeshRendererRaw.enabled = false;
        }

        isShowingRawHeatmap = false;
        Debug.Log("[HeatmapGenerator] Raw heatmap hidden");
    }

    // Generate fuzzy heatmap texture
    private IEnumerator GenerateFuzzyHeatmapAsync()
    {
        Debug.Log("[HeatmapGenerator] Generating fuzzy heatmap texture...");

        if (targetMeshRendererFuzzy == null || markerParentFuzzy == null || heatmapMaterial == null)
        {
            Debug.LogError("[HeatmapGenerator] Missing fuzzy components for heatmap generation");
            generateRoutineFuzzy = null;
            yield break;
        }

        Bounds meshBounds = targetMeshRendererFuzzy.bounds;
        Vector3 meshMin = meshBounds.min;
        Vector3 meshSize = meshBounds.size;

        heatmapTextureFuzzy = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp
        };

        // Collect marker data
        var markers = new List<(Transform tf, Vector3 pos, float temp)>();
        foreach (Transform child in markerParentFuzzy)
        {
            var label = child.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                string txt = label.text.Replace("°C", "").Trim();
                if (float.TryParse(txt, out float at))
                {
                    markers.Add((child, child.position, at));
                }
            }
        }

        if (markers.Count == 0)
        {
            Debug.LogWarning("[HeatmapGenerator] No fuzzy markers found");
            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                    heatmapTextureFuzzy.SetPixel(x, y, Color.clear);
                yield return null;
            }
            heatmapTextureFuzzy.Apply();
            generateRoutineFuzzy = null;
            yield break;
        }

        // Find top 5 hottest for radius calculation
        markers.Sort((a, b) => b.temp.CompareTo(a.temp));
        var topHotspots = new List<Transform>();
        for (int i = 0; i < Mathf.Min(5, markers.Count); i++)
            topHotspots.Add(markers[i].tf);

        // Generate heatmap using IDW
        yield return StartCoroutine(GenerateHeatmapIDW(heatmapTextureFuzzy, markers, topHotspots, meshMin, meshSize));

        heatmapTextureFuzzy.Apply();
        generateRoutineFuzzy = null;

        // Notify system about top hotspots
        OnFuzzyHeatmapGenerated?.Invoke(topHotspots);
        Debug.Log("[HeatmapGenerator] Fuzzy heatmap generation complete");
    }

    // Generate raw heatmap texture
    private IEnumerator GenerateRawHeatmapAsync()
    {
        Debug.Log("[HeatmapGenerator] Generating raw heatmap texture...");

        if (targetMeshRendererRaw == null || markerParentRaw == null || heatmapMaterial == null)
        {
            Debug.LogError("[HeatmapGenerator] Missing raw components for heatmap generation");
            generateRoutineRaw = null;
            yield break;
        }

        Bounds meshBounds = targetMeshRendererRaw.bounds;
        Vector3 meshMin = meshBounds.min;
        Vector3 meshSize = meshBounds.size;

        heatmapTextureRaw = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp
        };

        // Collect marker data
        var markers = new List<(Transform tf, Vector3 pos, float temp)>();
        foreach (Transform child in markerParentRaw)
        {
            var label = child.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                string txt = label.text.Replace("°C", "").Trim();
                if (float.TryParse(txt, out float t2m))
                {
                    markers.Add((child, child.position, t2m));
                }
            }
        }

        if (markers.Count == 0)
        {
            Debug.LogWarning("[HeatmapGenerator] No raw markers found");
            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                    heatmapTextureRaw.SetPixel(x, y, Color.clear);
                yield return null;
            }
            heatmapTextureRaw.Apply();
            generateRoutineRaw = null;
            yield break;
        }

        // Find top 5 hottest for radius calculation
        markers.Sort((a, b) => b.temp.CompareTo(a.temp));
        var topHotspots = new List<Transform>();
        for (int i = 0; i < Mathf.Min(5, markers.Count); i++)
            topHotspots.Add(markers[i].tf);

        // Generate heatmap using IDW
        yield return StartCoroutine(GenerateHeatmapIDW(heatmapTextureRaw, markers, topHotspots, meshMin, meshSize));

        heatmapTextureRaw.Apply();
        generateRoutineRaw = null;

        // Notify system about top hotspots
        OnRawHeatmapGenerated?.Invoke(topHotspots);
        Debug.Log("[HeatmapGenerator] Raw heatmap generation complete");
    }

    // Common IDW heatmap generation logic
    private IEnumerator GenerateHeatmapIDW(Texture2D texture, List<(Transform tf, Vector3 pos, float temp)> markers, List<Transform> topHotspots, Vector3 meshMin, Vector3 meshSize)
    {
        // Precompute marker data
        var markerData = new List<(Vector2 uv, float temp, float radius)>();
        foreach (var (tf, worldPos, temp) in markers)
        {
            float u = (worldPos.x - meshMin.x) / meshSize.x;
            float v = (worldPos.z - meshMin.z) / meshSize.z;
            float radius = topHotspots.Contains(tf) ? hotspotInterpolationRadius : baseInterpolationRadius;
            markerData.Add((new Vector2(u, v), temp, radius));
        }

        // Find min & max temperature
        float dataMin = float.MaxValue, dataMax = float.MinValue;
        foreach (var (_, temp, _) in markerData)
        {
            if (temp < dataMin) dataMin = temp;
            if (temp > dataMax) dataMax = temp;
        }
        if (Mathf.Approximately(dataMin, dataMax))
        {
            dataMin -= 0.5f;
            dataMax += 0.5f;
        }

        int gradientCount = heatColors.Length;

        // Generate pixels using IDW
        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                float u = 1f - (x / (float)(textureWidth - 1));
                float v = 1f - (y / (float)(textureHeight - 1));
                Vector2 uv = new Vector2(u, v);

                // Inverse Distance Weighting
                float sumW = 0f, sumT = 0f;
                foreach (var (markerUV, temp, radius) in markerData)
                {
                    float dx = (uv.x - markerUV.x) * meshSize.x;
                    float dy = (uv.y - markerUV.y) * meshSize.z;
                    float dWorld = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dWorld <= radius)
                    {
                        float w = 1f - (dWorld / radius);
                        sumW += w;
                        sumT += w * temp;
                    }
                }

                float value = (sumW > 0f) ? (sumT / sumW) : dataMin;
                float tNorm = Mathf.InverseLerp(dataMin, dataMax, value);
                tNorm = Mathf.Clamp01(tNorm);

                // Map to gradient
                float scaled = tNorm * (gradientCount - 1);
                int indexLow = Mathf.FloorToInt(scaled);
                int indexHigh = Mathf.Clamp(indexLow + 1, 0, gradientCount - 1);
                float fract = scaled - indexLow;

                Color cLow = heatColors[indexLow];
                Color cHigh = heatColors[indexHigh];
                Color c = Color.Lerp(cLow, cHigh, fract);

                texture.SetPixel(x, y, c);
            }
            yield return null; // Keep responsive
        }
    }

    // Animation utility
    private IEnumerator AnimateY(Transform t, float startY, float endY, float duration)
    {
        float elapsed = 0f;
        Vector3 basePos = t.localPosition;
        while (elapsed < duration)
        {
            float fraction = elapsed / duration;
            float y = Mathf.Lerp(startY, endY, fraction);
            t.localPosition = new Vector3(basePos.x, y, basePos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }
        t.localPosition = new Vector3(basePos.x, endY, basePos.z);
    }

    // Public methods for external control
    public void ClearFuzzyHeatmap()
    {
        if (heatmapTextureFuzzy != null)
        {
            DestroyImmediate(heatmapTextureFuzzy);
            heatmapTextureFuzzy = null;
        }
    }

    public void ClearRawHeatmap()
    {
        if (heatmapTextureRaw != null)
        {
            DestroyImmediate(heatmapTextureRaw);
            heatmapTextureRaw = null;
        }
    }
}
