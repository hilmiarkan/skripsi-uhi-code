using UnityEngine;

public class ActionHighlighter : MonoBehaviour
{
    [Header("Raycast Settings")]
    public Camera rayOriginCamera;
    public float maxDistance = 50f;
    public string buildingTag = "Building";

    [Header("Highlight Settings")]
    public Color highlightColor = new Color(0f, 0f, 1f, 0.4f);

    private Renderer previousRenderer = null;
    private Material[] originalMaterials = null;

    void Start()
    {
        if (rayOriginCamera == null && Camera.main != null)
        {
            rayOriginCamera = Camera.main;
        }
        if (rayOriginCamera == null)
        {
            Debug.LogWarning("[ActionHighlighter] Tidak ada camera yang di‐assign. Highlight tidak akan berfungsi.");
        }
    }

    void Update()
    {
        if (rayOriginCamera == null) return;

        Ray ray = rayOriginCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hitInfo;

        if (Physics.Raycast(ray, out hitInfo, maxDistance))
        {
            if (hitInfo.collider.CompareTag(buildingTag))
            {
                Renderer hitRenderer = hitInfo.collider.GetComponent<Renderer>();

                if (hitRenderer != null && hitRenderer != previousRenderer)
                {
                    RestorePreviousObject();

                    previousRenderer = hitRenderer;
                    originalMaterials = hitRenderer.materials;

                    Material[] newMats = new Material[originalMaterials.Length];
                    for (int i = 0; i < originalMaterials.Length; i++)
                    {
                        Material matInstance = new Material(originalMaterials[i]);

                        if (matInstance.HasProperty("_BaseColor"))
                        {
                            Color orig = matInstance.GetColor("_BaseColor");
                            Color tinted = Color.Lerp(orig, highlightColor, highlightColor.a);
                            tinted.a = highlightColor.a;
                            matInstance.SetColor("_BaseColor", tinted);
                        }
                        else if (matInstance.HasProperty("_Color"))
                        {
                            Color orig = matInstance.GetColor("_Color");
                            Color tinted = Color.Lerp(orig, highlightColor, highlightColor.a);
                            tinted.a = highlightColor.a;
                            matInstance.SetColor("_Color", tinted);
                        }
                        else
                        {
                            matInstance.color = highlightColor;
                        }

                        newMats[i] = matInstance;
                    }

                    hitRenderer.materials = newMats;
                }
                return;
            }
        }

        RestorePreviousObject();
    }

    private void RestorePreviousObject()
    {
        if (previousRenderer != null && originalMaterials != null)
        {
            previousRenderer.materials = originalMaterials;
            previousRenderer = null;
            originalMaterials = null;
        }
    }
}