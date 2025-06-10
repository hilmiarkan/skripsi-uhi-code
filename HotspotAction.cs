using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HotspotAction : MonoBehaviour
{
    [Header("Referensi Marker dan Fuzzy")]
    [Tooltip("Transform marker (TextMeshPro) yang akan diubah temperaturnya")]
    public Transform targetMarker;

    [Tooltip("Skrip FuzzyRuntimeCalculator, dipakai untuk menghitung ulang UHI")]
    public FuzzyRuntimeCalculator fuzzyCalculator;

    [Tooltip("Jika diperlukan: path ke file CSV raw agar bisa update data (optional)")]
    public string rawCsvPath;

    [Header("Prefab untuk Mitigasi")]
    [Tooltip("Prefab pohon yang akan di‐instantiate")]
    public GameObject treePrefab;

    [Tooltip("Prefab bangunan yang akan di‐instantiate")]
    public GameObject buildingPrefab;

    [Header("Prefab UI Tombol")]
    [Tooltip("Prefab UI Button untuk memilih Tree")]
    public GameObject treeButtonPrefab;

    [Tooltip("Prefab UI Button untuk memilih Building")]
    public GameObject buildingButtonPrefab;

    [Header("Konfigurasi Suhu")]
    [Tooltip("Perubahan suhu jika menanam pohon (negatif = mengurangi)")]
    public float treeTempOffset = -5f;

    [Tooltip("Perubahan suhu jika membangun bangunan (positif = menambah)")]
    public float buildingTempOffset = +5f;

    // Internal: referensi ke dua tombol yang nanti di‐instantiate
    private GameObject currentTreeButton;
    private GameObject currentBuildingButton;

    // Jeda untuk animasi muncul (dari y = -3 ke y = original)
    private readonly float prefabRiseDuration = 0.5f; 

    private bool uiVisible = false;

    private void Start()
    {
        // Pastikan Collider ada agar bisa diklik. 
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning($"{name} butuh Collider agar bisa diklik.");
        }
    }

    /// <summary>
    /// Unity akan memanggil OnMouseDown ketika user klik GameObject ini (dengan Collider dan layer raycast).
    /// Jika di VR pake XR Interactor, Anda bisa ganti dengan event handler XR Interaction.
    /// </summary>
    private void OnMouseDown()
    {
        ToggleActionUI();
    }

    /// <summary>
    /// Menampilkan / menyembunyikan UI Buttons (Tree & Building) di dekat hotspot.
    /// </summary>
    private void ToggleActionUI()
    {
        if (!uiVisible)
        {
            // Instantiate tombol Tree
            if (treeButtonPrefab != null)
            {
                currentTreeButton = Instantiate(
                    treeButtonPrefab,
                    transform.position + (transform.forward * 0.3f) + Vector3.up * 0.2f,
                    Quaternion.identity
                );
                // Pastikan Button memanggil OnSelectTree
                var btnTree = currentTreeButton.GetComponentInChildren<Button>();
                if (btnTree != null)
                    btnTree.onClick.AddListener(OnSelectTree);
            }

            // Instantiate tombol Building
            if (buildingButtonPrefab != null)
            {
                currentBuildingButton = Instantiate(
                    buildingButtonPrefab,
                    transform.position + (transform.forward * 0.3f) + Vector3.up * 0.2f + Vector3.right * 0.2f,
                    Quaternion.identity
                );
                var btnBuilding = currentBuildingButton.GetComponentInChildren<Button>();
                if (btnBuilding != null)
                    btnBuilding.onClick.AddListener(OnSelectBuilding);
            }

            uiVisible = true;
        }
        else
        {
            // Hapus UI tombol jika sudah muncul
            if (currentTreeButton != null)
                Destroy(currentTreeButton);
            if (currentBuildingButton != null)
                Destroy(currentBuildingButton);
            uiVisible = false;
        }
    }

    /// <summary>
    /// Dipanggil ketika user memilih opsi 'Tree'.
    /// </summary>
    private void OnSelectTree()
    {
        // 1. Buat animasi muncul prefab pohon dari bawah (y = -3 relatif ke hotspot)
        if (treePrefab != null)
        {
            StartCoroutine(SpawnAndRisePrefab(treePrefab, treeTempOffset));
        }
        CleanupUI();
    }

    /// <summary>
    /// Dipanggil ketika user memilih opsi 'Building'.
    /// </summary>
    private void OnSelectBuilding()
    {
        // 1. Buat animasi muncul prefab bangunan dari bawah (y = -3 relatif ke hotspot)
        if (buildingPrefab != null)
        {
            StartCoroutine(SpawnAndRisePrefab(buildingPrefab, buildingTempOffset));
        }
        CleanupUI();
    }

    /// <summary>
    /// Hapus UI tombol Tree & Building.
    /// </summary>
    private void CleanupUI()
    {
        if (currentTreeButton != null)
            Destroy(currentTreeButton);
        if (currentBuildingButton != null)
            Destroy(currentBuildingButton);
        uiVisible = false;
    }

    /// <summary>
    /// Coroutine untuk spawn prefab (pohon/bangunan) di y = baseY - 3, lalu naik ke posisi lokal y = 0 selama prefabRiseDuration.
    /// Setelah selesai, update suhu marker dan panggil ulang perhitungan UHI.
    /// </summary>
    /// <param name="prefab">Prefab yang dilambungkan (Tree atau Building)</param>
    /// <param name="tempOffset">Offset suhu (±5)</param>
    private IEnumerator SpawnAndRisePrefab(GameObject prefab, float tempOffset)
    {
        Vector3 basePos = transform.position;
        Vector3 spawnPos = new Vector3(basePos.x, basePos.y - 3f, basePos.z);
        GameObject inst = Instantiate(prefab, spawnPos, Quaternion.identity);

        Vector3 targetPos = new Vector3(basePos.x, basePos.y, basePos.z);
        float elapsed = 0f;

        // Naikkan prefab dari y = -3 ke y = 0 sepanjang prefabRiseDuration
        while (elapsed < prefabRiseDuration)
        {
            float t = elapsed / prefabRiseDuration;
            inst.transform.position = Vector3.Lerp(spawnPos, targetPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        inst.transform.position = targetPos;

        // Setelah muncul, update suhu marker (TextMeshPro) langsung
        UpdateMarkerTemperature(tempOffset);

        // Hitung ulang UHI lewat fuzzyCalculator
        if (fuzzyCalculator != null)
        {
            // Kita asumsikan fuzzyCalculator punya method ComputeUhiFromRaw yang menerima csv path atau source data
            // Jika hanya ingin recalc UHI, panggil ComputeUhiFromRaw(rawCsvPath);
            // Atau panggil ComputeUhiFromRaw dengan parameter yang di‐update secara manual.
            fuzzyCalculator.ComputeUhiFromRaw(rawCsvPath);
        }
    }

    /// <summary>
    /// Mengubah nilai suhu (Text) di targetMarker sebanyak tempOffset.
    /// Misal: jika tree (-5), kurangi suhu saat ini, lalu tampilkan kembali text "20°C" → "15°C".
    /// </summary>
    /// <param name="tempOffset">Offset (misal -5 atau +5)</param>
    private void UpdateMarkerTemperature(float tempOffset)
    {
        if (targetMarker == null)
        {
            Debug.LogWarning("HotspotAction: targetMarker belum di‐assign.");
            return;
        }

        // Anggap marker punya TextMeshProUGUI di child
        TextMeshProUGUI tmp = targetMarker.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp == null)
        {
            Debug.LogWarning("HotspotAction: tidak menemukan TextMeshProUGUI di targetMarker.");
            return;
        }

        // Parsing teks saat ini, misal "25.3°C"
        string currentText = tmp.text.Replace("°C", "").Trim();
        if (float.TryParse(currentText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float currentTemp))
        {
            float newTemp = currentTemp + tempOffset;
            tmp.text = $"{newTemp.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}°C";
        }
        else
        {
            Debug.LogWarning($"HotspotAction: gagal parse suhu '{tmp.text}'.");
        }
    }
}
