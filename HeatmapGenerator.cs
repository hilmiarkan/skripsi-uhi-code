using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Attach to a GameObject (e.g., "HeatmapManager").
/// Assign your Terrain, marker parent, gradient, button and heatmap material in the Inspector.
/// Click the button to toggle a generated heatmap texture on the terrain.
/// </summary>
public class HeatmapGenerator : MonoBehaviour
{
    [Header("Heatmap Target")]
    [Tooltip("Assign your MeshRenderer here (the mesh to overlay the heatmap).")]
    public MeshRenderer targetMeshRenderer;

    [Header("Markers Parent")]
    [Tooltip("Parent Transform containing your Marker instances.")]
    public Transform markerParent;

    [Header("Heatmap Settings")]
    [Tooltip("Gradient from low (left) to high (right) temperatures.")]
    public Gradient colorGradient;
    public int textureWidth = 512;
    public int textureHeight = 512;
    public float interpolationRadius = 50f;
    public float minTemperature = 15f;
    public float maxTemperature = 45f;

    [Header("UI Elements")]
    public Button showHeatmapButton;

    [Header("Heatmap Material")]
    [Tooltip("A simple Unlit/Texture material.")]
    public Material heatmapMaterial;

    private Texture2D heatmapTexture;
    private Material originalMaterial;
    private bool isShowingHeatmap = false;

    [Header("Heatmap Opacity")]
    [Range(0f, 1f)]
    public float heatmapAlpha = 0.5f;

    void Start()
    {
        if (targetMeshRenderer != null)
        {
            originalMaterial = targetMeshRenderer.sharedMaterial;
        }

        // Hide mesh initially until user toggles heatmap
        if (targetMeshRenderer != null)
            targetMeshRenderer.enabled = false;

        showHeatmapButton.onClick.RemoveAllListeners();
        showHeatmapButton.onClick.AddListener(OnHeatmapButtonClicked);

        // Initialize button label
        var btnText = showHeatmapButton.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null)
            btnText.text = "Show Heatmap";
    }

    private void OnHeatmapButtonClicked()
    {
        if (!isShowingHeatmap)
            StartCoroutine(ShowHeatmapRoutine());
        else
            HideHeatmap();
    }

    private IEnumerator ShowHeatmapRoutine()
    {
        showHeatmapButton.interactable = false;
        var btnText = showHeatmapButton.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null)
            btnText.text = "Mapping...";

        EnsureHeatmapGenerated();

        // Enable mesh renderer to display heatmap
        if (targetMeshRenderer != null)
            targetMeshRenderer.enabled = true;

        // Apply desired alpha to heatmap material
        if (heatmapMaterial != null)
        {
            Color matColor = heatmapMaterial.color;
            matColor.a = heatmapAlpha;
            heatmapMaterial.color = matColor;
        }

        // Assign generated texture to the heatmap material
        heatmapMaterial.mainTexture = heatmapTexture;

        // Swap in the heatmap material
        if (targetMeshRenderer != null)
            targetMeshRenderer.sharedMaterial = heatmapMaterial;

        isShowingHeatmap = true;

        yield return null; // wait one frame

        if (btnText != null)
            btnText.text = "Hide Heatmap";
        showHeatmapButton.interactable = true;
    }

    private void HideHeatmap()
    {
        if (targetMeshRenderer != null)
            targetMeshRenderer.sharedMaterial = originalMaterial;

        if (targetMeshRenderer != null)
            targetMeshRenderer.enabled = false;

        isShowingHeatmap = false;
        var btnText = showHeatmapButton.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null)
            btnText.text = "Show Heatmap";
    }

    /// <summary>
    /// Generates a heatmap texture via inverse-distance weighting of marker temperatures.
    /// </summary>
    private void EnsureHeatmapGenerated()
    {
        if (heatmapTexture != null)
            return; // already generated

        if (targetMeshRenderer == null || markerParent == null || colorGradient == null || heatmapMaterial == null)
        {
            Debug.LogError("HeatmapGenerator: Assign MeshRenderer, Marker Parent, Gradient, and Heatmap Material.");
            return;
        }

        // World-space bounds of the mesh
        Bounds meshBounds = targetMeshRenderer.bounds;
        Vector3 meshMin = meshBounds.min;
        Vector3 meshSize = meshBounds.size;

        heatmapTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp
        };

        // Gather marker positions & temperatures
        var markers = new List<(Vector3 pos, float temp)>();
        foreach (Transform child in markerParent)
        {
            var info  = child.GetComponent<MarkerInfo>();
            var label = child.GetComponentInChildren<TextMeshProUGUI>();
            if (info != null && label != null)
            {
                string txt = label.text.Replace("°C", "");
                if (float.TryParse(txt, out float at))
                    markers.Add((child.position, at));
            }
        }

        // For each pixel in the texture, interpolate world position & compute color
        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                float u = x / (float)(textureWidth - 1);
                float v = y / (float)(textureHeight - 1);
                Vector3 worldPos = new Vector3(
                    meshMin.x + u * meshSize.x,
                    meshMin.y,
                    meshMin.z + v * meshSize.z
                );

                // Inverse-distance weighting
                float sumW = 0f, sumT = 0f;
                foreach (var (pos, temp) in markers)
                {
                    float d = Vector2.Distance(
                        new Vector2(worldPos.x, worldPos.z),
                        new Vector2(pos.x, pos.z)
                    );
                    if (d <= interpolationRadius)
                    {
                        float w = 1f - (d / interpolationRadius);
                        sumW += w;
                        sumT += w * temp;
                    }
                }

                float value = sumW > 0f ? sumT / sumW : minTemperature;
                float tNorm = Mathf.InverseLerp(minTemperature, maxTemperature, value);
                Color c = colorGradient.Evaluate(tNorm);
                heatmapTexture.SetPixel(x, y, c);
            }
        }

        heatmapTexture.Apply();

        // NOTE: The heatmapMaterial must use a shader that supports transparency (e.g., "Unlit/Transparent")
        // and its alpha channel should be set so the mesh appears semi-transparent.
    }
}
