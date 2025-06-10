using UnityEngine;

public class BuildingClickHandler : MonoBehaviour
{
    [Header("UI Prefab")]
    public GameObject infoPanelPrefab;

    [Header("UI Spawn Settings")]
    public Vector3 panelOffset = new Vector3(0f, 2f, 0f); // di atas bangunan
    public float panelScale = 0.02f;

    private Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                if (hit.collider.CompareTag("Building"))
                {
                    // Spawn satu UI di atas bangunan
                    Vector3 spawnPos = hit.collider.bounds.center + panelOffset;
                    GameObject panel = Instantiate(infoPanelPrefab, spawnPos, Quaternion.identity);
                    panel.transform.localScale = Vector3.one * panelScale;

                    // Hadapkan panel menghadap kamera
                    panel.transform.LookAt(panel.transform.position + cam.transform.rotation * Vector3.forward,
                                            cam.transform.rotation * Vector3.up);
                    // Jangan Destroy; biarkan banyak panel muncul
                }
            }
        }
    }
}