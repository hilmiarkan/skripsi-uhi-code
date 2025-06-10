using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;

public class MitigationManager : MonoBehaviour
{
    [Header("Prefab Mitigasi")]
    [Tooltip("Prefab pohon yang akan di-Instantiate")]
    public GameObject treePrefab;
    [Tooltip("Prefab bangunan yang akan di-Instantiate")]
    public GameObject buildingPrefab;

    [Header("UI Elements")]
    [Tooltip("Prefab UI Button untuk memilih Tree")]
    public GameObject treeButtonPrefab;
    [Tooltip("Prefab UI Button untuk memilih Building")]
    public GameObject buildingButtonPrefab;

    [Header("Mitigation Settings")]
    [Tooltip("Perubahan suhu jika menanam pohon (negatif = mengurangi)")]
    public float treeTempOffset = -5f;
    [Tooltip("Perubahan suhu jika membangun bangunan (positif = menambah)")]
    public float buildingTempOffset = +5f;
    [Tooltip("Durasi animasi muncul (dalam detik)")]
    public float riseDuration = 0.5f;
    [Tooltip("Jarak vertikal (Y) dari bawah ke posisi hotspot saat muncul")]
    public float riseDistance = 3f;

    [Header("References")]
    [Tooltip("Referensi ke HotspotManager untuk menghapus hotspot")]
    public HotspotManager hotspotManager;
    [Tooltip("Referensi ke FuzzyRuntimeCalculator untuk menghitung ulang UHI")]
    public FuzzyRuntimeCalculator fuzzyCalculator;
    [Tooltip("Path lengkap ke file CSV raw terakhir")]
    public string rawCsvPath;

    private GameObject currentTreeButton;
    private GameObject currentBuildingButton;
    private bool uiVisible = false;

    /// <summary>
    /// Menampilkan UI pilihan mitigasi (Tree/Building)
    /// </summary>
    public void ShowMitigationUI(Vector3 position)
    {
        if (uiVisible) return;

        // Instantiate UI buttons
        if (treeButtonPrefab != null)
        {
            currentTreeButton = Instantiate(treeButtonPrefab, position, Quaternion.identity);
            var treeBtn = currentTreeButton.GetComponent<Button>();
            if (treeBtn != null)
                treeBtn.onClick.AddListener(OnSelectTree);
        }

        if (buildingButtonPrefab != null)
        {
            currentBuildingButton = Instantiate(buildingButtonPrefab, position, Quaternion.identity);
            var buildingBtn = currentBuildingButton.GetComponent<Button>();
            if (buildingBtn != null)
                buildingBtn.onClick.AddListener(OnSelectBuilding);
        }

        uiVisible = true;
    }

    /// <summary>
    /// Menghapus UI pilihan mitigasi
    /// </summary>
    public void HideMitigationUI()
    {
        if (currentTreeButton != null)
            Destroy(currentTreeButton);
        if (currentBuildingButton != null)
            Destroy(currentBuildingButton);
        uiVisible = false;
    }

    private void OnSelectTree()
    {
        if (treePrefab != null)
        {
            StartCoroutine(SpawnAndRisePrefab(treePrefab, treeTempOffset));
        }
        HideMitigationUI();
    }

    private void OnSelectBuilding()
    {
        if (buildingPrefab != null)
        {
            StartCoroutine(SpawnAndRisePrefab(buildingPrefab, buildingTempOffset));
        }
        HideMitigationUI();
    }

    private IEnumerator SpawnAndRisePrefab(GameObject prefab, float tempOffset)
    {
        Vector3 basePos = transform.position;
        Vector3 spawnPos = new Vector3(basePos.x, basePos.y - riseDistance, basePos.z);
        GameObject inst = Instantiate(prefab, spawnPos, Quaternion.identity);

        Vector3 targetPos = new Vector3(basePos.x, basePos.y, basePos.z);
        float elapsed = 0f;

        while (elapsed < riseDuration)
        {
            float t = elapsed / riseDuration;
            inst.transform.position = Vector3.Lerp(spawnPos, targetPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        inst.transform.position = targetPos;

        // Update UHI calculation
        if (fuzzyCalculator != null && !string.IsNullOrEmpty(rawCsvPath))
        {
            fuzzyCalculator.ComputeUhiFromRaw(rawCsvPath);
        }
    }
} 