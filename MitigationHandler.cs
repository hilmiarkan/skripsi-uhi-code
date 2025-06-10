using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MitigationHandler : MonoBehaviour
{
    [Header("Prefab Aksi Mitigasi")]
    [Tooltip("Prefab pohon yang akan di-Instantiate")]
    public GameObject treePrefab;
    [Tooltip("Prefab bangunan yang akan di-Instantiate")]
    public GameObject buildingPrefab;

    [Header("Tombol Pilihan Mitigasi (sudah ada di ActionMitigasi)")]
    public Button treeButton;
    public Button buildingButton;

    [Header("Informasi Hotspot")]
    [Tooltip("Transform titik hotspot dasar (untuk posisi spawn dan perhitungan)")]
    public Transform hotspotPosition;
    [Tooltip("Ring hotspot raw yang ingin dihapus saat mitigasi")]
    public GameObject rawHotspotRing;
    [Tooltip("Marker hotspot raw yang ingin dihapus saat mitigasi")]
    public GameObject rawHotspotMarker;
    [Tooltip("Ring hotspot fuzzy yang ingin dihapus saat mitigasi")]
    public GameObject fuzzyHotspotRing;
    [Tooltip("Marker hotspot fuzzy yang ingin dihapus saat mitigasi")]
    public GameObject fuzzyHotspotMarker;

    [Header("Label Suhu pada Marker")]
    [Tooltip("TextMeshPro pada marker raw yang menampilkan suhu raw")]
    public TextMeshProUGUI rawLabel;
    [Tooltip("TextMeshPro pada marker fuzzy yang menampilkan suhu fuzzy")]
    public TextMeshProUGUI fuzzyLabel;

    [Header("Penghitung UHI")]
    [Tooltip("Referensi ke skrip FuzzyRuntimeCalculator untuk menghitung ulang UHI")]
    public FuzzyRuntimeCalculator fuzzyCalculator;
    [Tooltip("Path lengkap ke file CSV raw terakhir (misal: Assets/FetchedData/meteo_YYYYMMDD_HHMMSS.csv)")]
    public string rawCsvPath;

    [Header("Pengaturan Animasi Spawn")]
    [Tooltip("Jarak vertikal (Y) dari bawah ke posisi hotspot saat muncul")]
    public float riseDistance = 3f;
    [Tooltip("Durasi animasi muncul (dalam detik)")]
    public float riseDuration = 1f;

    private void Start()
    {
        // Pastikan tombol sudah terhubung
        if (treeButton != null)
            treeButton.onClick.AddListener(OnPlantTreeClicked);
        if (buildingButton != null)
            buildingButton.onClick.AddListener(OnBuildBuildingClicked);
    }

    /// <summary>
    /// Dipanggil saat tombol 'Tanam Pohon' ditekan
    /// </summary>
    private void OnPlantTreeClicked()
    {
        ApplyMitigation(isPlantingTree: true);
    }

    /// <summary>
    /// Dipanggil saat tombol 'Bangun Bangunan' ditekan
    /// </summary>
    private void OnBuildBuildingClicked()
    {
        ApplyMitigation(isPlantingTree: false);
    }

    /// <summary>
    /// Proses utama mitigasi: meng-_spawn prefab, menghapus hotspot, mengubah suhu, dan menghitung ulang UHI
    /// </summary>
    /// <param name="isPlantingTree">true jika menanam pohon (mengurangi suhu), false jika bangun bangunan (menambah suhu)</param>
    private void ApplyMitigation(bool isPlantingTree)
    {
        // 1. Ubah dan tampilkan suhu baru pada label raw dan fuzzy (+/- 5°C)
        AdjustTemperatures(isPlantingTree);

        // 2. Hapus hotspot ring & marker (raw & fuzzy)
        DestroyHotspotVisuals();

        // 3. Spawn prefab pohon atau bangunan dengan animasi 'timbul dari bawah'
        GameObject prefabToSpawn = isPlantingTree ? treePrefab : buildingPrefab;
        if (prefabToSpawn != null && hotspotPosition != null)
        {
            StartCoroutine(SpawnAndRise(prefabToSpawn));
        }

        // 4. Hitung ulang intensitas UHI (jika fuzzyCalculator & rawCsvPath diisi)
        if (fuzzyCalculator != null && !string.IsNullOrEmpty(rawCsvPath))
        {
            fuzzyCalculator.ComputeUhiFromRaw(rawCsvPath);
        }

        // 5. Nonaktifkan tombol setelah mitigasi agar tidak bisa klik berkali-kali
        treeButton.interactable = false;
        buildingButton.interactable = false;
    }

    /// <summary>
    /// Memodifikasi nilai suhu pada label raw dan fuzzy sebesar ±5°C
    /// </summary>
    /// <param name="isPlantingTree">true untuk mengurangi 5°C, false untuk menambah 5°C</param>
    private void AdjustTemperatures(bool isPlantingTree)
    {
        float delta = isPlantingTree ? -5f : +5f;

        // Raw label
        if (rawLabel != null)
        {
            // Asumsi format rawLabel.text = "25.3°C"
            string textRaw = rawLabel.text.Replace("°C", "").Trim();
            if (float.TryParse(textRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float rawTemp))
            {
                float newRawTemp = rawTemp + delta;
                rawLabel.text = $"{newRawTemp:F1}°C";
            }
        }

        // Fuzzy label
        if (fuzzyLabel != null)
        {
            // Asumsi format fuzzyLabel.text = "23.8°C"
            string textFuzzy = fuzzyLabel.text.Replace("°C", "").Trim();
            if (float.TryParse(textFuzzy, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fuzzyTemp))
            {
                float newFuzzyTemp = fuzzyTemp + delta;
                fuzzyLabel.text = $"{newFuzzyTemp:F1}°C";
            }
        }
    }

    /// <summary>
    /// Menghapus GameObject hotspot ring & marker untuk raw dan fuzzy
    /// </summary>
    private void DestroyHotspotVisuals()
    {
        if (rawHotspotRing != null)
            Destroy(rawHotspotRing);
        if (rawHotspotMarker != null)
            Destroy(rawHotspotMarker);
        if (fuzzyHotspotRing != null)
            Destroy(fuzzyHotspotRing);
        if (fuzzyHotspotMarker != null)
            Destroy(fuzzyHotspotMarker);
    }

    /// <summary>
    /// Coroutine untuk spawn prefab dari bawah (Y = hotspotY - riseDistance) lalu naik ke Y hotspot
    /// </summary>
    /// <param name="prefab">Prefab pohon atau bangunan</param>
    private IEnumerator SpawnAndRise(GameObject prefab)
    {
        Vector3 startPos = hotspotPosition.position + Vector3.down * riseDistance;
        Vector3 endPos = hotspotPosition.position;
        GameObject instance = Instantiate(prefab, startPos, prefab.transform.rotation);
        float elapsed = 0f;

        while (elapsed < riseDuration)
        {
            float t = elapsed / riseDuration;
            instance.transform.position = Vector3.Lerp(startPos, endPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Pastikan posisi akhir tepat
        instance.transform.position = endPos;
    }
}
