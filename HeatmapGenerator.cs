using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Versi ini menghasilkan DUA heatmap:
/// 1. Heatmap “Fuzzy” (menggunakan nilai fuzzy seperti Apparent Temperature).
/// 2. Heatmap “Raw” (menggunakan nilai Temperature_2m langsung dari fetchdata).
///
/// Selain heatmap, di lima titik terpanas akan di‐instansiasi dua prefab:
///  - hotspotRingPrefabXXX: untuk efek ring di atas tanah.
///  - hotspotMarkerPrefabXXX: untuk marker yang berotasi sedikit di atas ring.
///
/// Ketika tombol hide ditekan, semua hotspot akan dihapus.
/// </summary>
public class HeatmapGenerator : MonoBehaviour
{
    // ---------------------------------------------------------------------------------------------------------
    // 1) Heatmap Targets
    //    - Dua buah MeshRenderer: satu untuk heatmap “Fuzzy”, satu untuk heatmap “Raw”
    // ---------------------------------------------------------------------------------------------------------
    [Header("Heatmap Targets")]
    [Tooltip("MeshRenderer untuk heatmap yang berbasis nilai fuzzy (Apparent Temperature).")]
    public MeshRenderer targetMeshRendererFuzzy;
    [Tooltip("MeshRenderer untuk heatmap yang berbasis nilai raw (Temperature_2m).")]
    public MeshRenderer targetMeshRendererRaw;

    // ---------------------------------------------------------------------------------------------------------
    // 2) Marker Parents
    //    - Dua buah Transforms: satu parent marker untuk fuzzy, satu parent marker untuk raw
    // ---------------------------------------------------------------------------------------------------------
    [Header("Marker Parents")]
    [Tooltip("Parent Transform yang menampung marker-marker nilai fuzzy (Apparent Temperature).")]
    public Transform markerParentFuzzy;
    [Tooltip("Parent Transform yang menampung marker-marker nilai raw (Temperature_2m).")]
    public Transform markerParentRaw;

    // ---------------------------------------------------------------------------------------------------------
    // 3) Hotspot Prefabs (ditambahkan)
    // ---------------------------------------------------------------------------------------------------------
    [Header("Hotspot Prefabs - Fuzzy")]
    [Tooltip("Prefab untuk ring efek di titik terpanas (fuzzy).")]
    public GameObject hotspotRingPrefabFuzzy;
    [Tooltip("Prefab untuk marker berotasi di titik terpanas (fuzzy).")]
    public GameObject hotspotMarkerPrefabFuzzy;

    [Header("Hotspot Prefabs - Raw")]
    [Tooltip("Prefab untuk ring efek di titik terpanas (raw).")]
    public GameObject hotspotRingPrefabRaw;
    [Tooltip("Prefab untuk marker berotasi di titik terpanas (raw).")]
    public GameObject hotspotMarkerPrefabRaw;

    // ---------------------------------------------------------------------------------------------------------
    // 4) Heatmap Settings
    //    - Ukuran tekstur, radius default/diperbesar, gradient warna (tetap hard-coded)
    // ---------------------------------------------------------------------------------------------------------
    [Header("Heatmap Settings")]
    [Tooltip("Lebar (pixel) dari setiap tekstur heatmap.")]
    public int textureWidth = 512;
    [Tooltip("Tinggi (pixel) dari setiap tekstur heatmap.")]
    public int textureHeight = 512;
    [Tooltip("Radius interpolasi (world units) untuk sebagian besar marker (default).")]
    public float baseInterpolationRadius = 100f;
    [Tooltip("Radius interpolasi (world units) untuk top 5 hottest marker (driver heat spots).")]
    public float hotspotInterpolationRadius = 150f;

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

    // ---------------------------------------------------------------------------------------------------------
    // 5) UI Elements
    //    - Dua buah tombol untuk men-toggle masing-masing heatmap
    // ---------------------------------------------------------------------------------------------------------
    [Header("UI Elements")]
    [Tooltip("Button untuk toggle heatmap berbasis nilai fuzzy.")]
    public Button showFuzzyHeatmapButton;
    [Tooltip("Button untuk toggle heatmap berbasis nilai raw (Temperature_2m).")]
    public Button showRawHeatmapButton;

    // ---------------------------------------------------------------------------------------------------------
    // 6) Heatmap Material & Opacity
    //    - Satu material yang dipakai untuk kedua heatmap (Unlit/Transparent), di‐clone saat runtime
    // ---------------------------------------------------------------------------------------------------------
    [Header("Heatmap Material")]
    [Tooltip("Material Unlit/Transparent (contoh: Unlit/Transparent colored).")]
    public Material heatmapMaterial;

    [Header("Heatmap Opacity")]
    [Range(0f, 1f)]
    [Tooltip("Opacity (alpha) dari heatmap overlay.")]
    public float heatmapAlpha = 0.5f;

    // ---------------------------------------------------------------------------------------------------------
    // 7) Animation Settings
    //    - Durasi animasi naik/turun, posisi Y tersembunyi dan tampil
    // ---------------------------------------------------------------------------------------------------------
    [Header("Animation Settings")]
    [Tooltip("Durasi (detik) untuk animasi Y saat show/hide heatmap.")]
    public float animationDuration = 0.5f;
    [Tooltip("Posisi Y lokal saat heatmap tersembunyi.")]
    public float hiddenY = -1f;
    [Tooltip("Posisi Y lokal saat heatmap muncul.")]
    public float visibleY = 0.31f;

    // ---------------------------------------------------------------------------------------------------------
    // 8) Internal Textures & State Flags
    //    - Dua buah Texture2D (satu untuk fuzzy, satu untuk raw)
    //    - Dua flags untuk men-track apakah masing-masing heatmap sedang ditampilkan
    //    - Dua daftar transform untuk top hotspots
    //    - Dua list untuk menampung instance hotspot yang telah dibuat
    // ---------------------------------------------------------------------------------------------------------
    private Texture2D heatmapTextureFuzzy = null;
    private Texture2D heatmapTextureRaw   = null;

    private Material originalMaterialFuzzy = null;
    private Material originalMaterialRaw   = null;

    private bool isShowingFuzzyHeatmap = false;
    private bool isShowingRawHeatmap   = false;

    private Coroutine generateRoutineFuzzy = null;
    private Coroutine generateRoutineRaw   = null;

    // ––––– Daftar 5 hotspot terpanas (Transform) –––––
    private List<Transform> topHotspotsFuzzy = new List<Transform>();
    private List<Transform> topHotspotsRaw   = new List<Transform>();

    // ––––– Instance GameObject hotspot yang di‐spawn (agar bisa di‐destroy nanti) –––––
    private List<GameObject> spawnedHotspotsFuzzy = new List<GameObject>();
    private List<GameObject> spawnedHotspotsRaw   = new List<GameObject>();

    // ---------------------------------------------------------------------------------------------------------
    // 9) Unity Event: Start()
    //    - Simpan material asli, sembunyikan kedua mesh, atur listener tombol
    // ---------------------------------------------------------------------------------------------------------
    void Start()
    {
        //— Inisialisasi untuk Heatmap “Fuzzy” ——
        if (targetMeshRendererFuzzy != null)
        {
            originalMaterialFuzzy = targetMeshRendererFuzzy.sharedMaterial;
            // Set posisi Y tersembunyi dan disable
            Vector3 posFuzzy = targetMeshRendererFuzzy.transform.localPosition;
            posFuzzy.y = hiddenY;
            targetMeshRendererFuzzy.transform.localPosition = posFuzzy;
            targetMeshRendererFuzzy.enabled = false;
        }

        //— Inisialisasi untuk Heatmap “Raw” ——
        if (targetMeshRendererRaw != null)
        {
            originalMaterialRaw = targetMeshRendererRaw.sharedMaterial;
            // Set posisi Y tersembunyi dan disable
            Vector3 posRaw = targetMeshRendererRaw.transform.localPosition;
            posRaw.y = hiddenY;
            targetMeshRendererRaw.transform.localPosition = posRaw;
            targetMeshRendererRaw.enabled = false;
        }

        //— Atur listener tombol untuk Fuzzy Heatmap ——
        showFuzzyHeatmapButton.onClick.RemoveAllListeners();
        showFuzzyHeatmapButton.onClick.AddListener(OnFuzzyHeatmapButtonClicked);
        // Inisialisasi label tombol
        var btnTextFuzzy = showFuzzyHeatmapButton.GetComponentInChildren<TextMeshProUGUI>();
        if (btnTextFuzzy != null)
            btnTextFuzzy.text = "Show Fuzzy Heatmap";

        //— Atur listener tombol untuk Raw Heatmap ——
        showRawHeatmapButton.onClick.RemoveAllListeners();
        showRawHeatmapButton.onClick.AddListener(OnRawHeatmapButtonClicked);
        // Inisialisasi label tombol
        var btnTextRaw = showRawHeatmapButton.GetComponentInChildren<TextMeshProUGUI>();
        if (btnTextRaw != null)
            btnTextRaw.text = "Show Raw Heatmap";
    }

    // ---------------------------------------------------------------------------------------------------------
    // 10) Button Callbacks
    //    - Ketika tombol Fuzzy di-klik, panggil Show atau Hide sesuai state
    //    - Ketika tombol Raw di-klik, panggil Show atau Hide sesuai state
    // ---------------------------------------------------------------------------------------------------------
    private void OnFuzzyHeatmapButtonClicked()
    {
        if (!isShowingFuzzyHeatmap)
            StartCoroutine(ShowFuzzyHeatmapRoutine());
        else
            StartCoroutine(HideFuzzyHeatmapRoutine());
    }

    private void OnRawHeatmapButtonClicked()
    {
        if (!isShowingRawHeatmap)
            StartCoroutine(ShowRawHeatmapRoutine());
        else
            StartCoroutine(HideRawHeatmapRoutine());
    }

    // ---------------------------------------------------------------------------------------------------------
    // 11) Routine: Tampilkan Heatmap “Fuzzy”
    //     - Jika belum pernah generate, panggil GenerateFuzzyHeatmapAsync()
    //     - Apply material&texture, animasi naik, generate & tampilkan hotspot
    // ---------------------------------------------------------------------------------------------------------
    private IEnumerator ShowFuzzyHeatmapRoutine()
    {
        showFuzzyHeatmapButton.interactable = false;
        var btnTextFuzzy = showFuzzyHeatmapButton.GetComponentInChildren<TextMeshProUGUI>();
        if (btnTextFuzzy != null)
            btnTextFuzzy.text = "Loading...";

        // Jika belum ada tekstur, generate sekali
        if (heatmapTextureFuzzy == null)
        {
            if (generateRoutineFuzzy != null)
                StopCoroutine(generateRoutineFuzzy);
            generateRoutineFuzzy = StartCoroutine(GenerateFuzzyHeatmapAsync());

            // Tunggu sampai proses generate selesai
            while (generateRoutineFuzzy != null)
                yield return null;
        }

        // Terapkan tekstur & material, lalu animasi naik
        if (targetMeshRendererFuzzy != null && heatmapMaterial != null && heatmapTextureFuzzy != null)
        {
            targetMeshRendererFuzzy.enabled = true;

            // Set opacity
            Color matColor = heatmapMaterial.color;
            matColor.a = heatmapAlpha;
            heatmapMaterial.color = matColor;
            heatmapMaterial.mainTexture = heatmapTextureFuzzy;
            targetMeshRendererFuzzy.sharedMaterial = heatmapMaterial;

            // Animate Y dari hiddenY ke visibleY
            StartCoroutine(AnimateY(
                targetMeshRendererFuzzy.transform,
                hiddenY,
                visibleY,
                animationDuration
            ));

            // Setelah heatmap muncul, tampilkan hotspot prefab fuzzy
            DisplayHotspotsFuzzy();
        }

        isShowingFuzzyHeatmap = true;

        if (btnTextFuzzy != null)
            btnTextFuzzy.text = "Hide Fuzzy Heatmap";
        showFuzzyHeatmapButton.interactable = true;
    }

    // ---------------------------------------------------------------------------------------------------------
    // 12) Routine: Sembunyikan Heatmap “Fuzzy”
    //     - Animasi turun, disable mesh, kembalikan material asli, hapus hotspot
    // ---------------------------------------------------------------------------------------------------------
    private IEnumerator HideFuzzyHeatmapRoutine()
    {
        showFuzzyHeatmapButton.interactable = false;
        var btnTextFuzzy = showFuzzyHeatmapButton.GetComponentInChildren<TextMeshProUGUI>();
        if (btnTextFuzzy != null)
            btnTextFuzzy.text = "Hiding...";

        if (targetMeshRendererFuzzy != null)
            yield return AnimateY(
                targetMeshRendererFuzzy.transform,
                visibleY,
                hiddenY,
                animationDuration
            );

        if (targetMeshRendererFuzzy != null)
        {
            targetMeshRendererFuzzy.sharedMaterial = originalMaterialFuzzy;
            targetMeshRendererFuzzy.enabled = false;
        }

        // Hapus semua hotspot yang sudah dibuat (fuzzy)
        HideHotspotsFuzzy();

        isShowingFuzzyHeatmap = false;

        if (btnTextFuzzy != null)
            btnTextFuzzy.text = "Show Fuzzy Heatmap";
        showFuzzyHeatmapButton.interactable = true;
    }

    // ---------------------------------------------------------------------------------------------------------
    // 13) Routine: Tampilkan Heatmap “Raw”
    //     - Jika belum pernah generate, panggil GenerateRawHeatmapAsync()
    //     - Apply material&texture, animasi naik, generate & tampilkan hotspot
    // ---------------------------------------------------------------------------------------------------------
    private IEnumerator ShowRawHeatmapRoutine()
    {
        showRawHeatmapButton.interactable = false;
        var btnTextRaw = showRawHeatmapButton.GetComponentInChildren<TextMeshProUGUI>();
        if (btnTextRaw != null)
            btnTextRaw.text = "Loading...";

        // Jika belum ada tekstur raw, generate sekali
        if (heatmapTextureRaw == null)
        {
            if (generateRoutineRaw != null)
                StopCoroutine(generateRoutineRaw);
            generateRoutineRaw = StartCoroutine(GenerateRawHeatmapAsync());

            // Tunggu sampai proses generate selesai
            while (generateRoutineRaw != null)
                yield return null;
        }

        // Terapkan tekstur & material, lalu animasi naik
        if (targetMeshRendererRaw != null && heatmapMaterial != null && heatmapTextureRaw != null)
        {
            targetMeshRendererRaw.enabled = true;

            // Set opacity
            Color matColor = heatmapMaterial.color;
            matColor.a = heatmapAlpha;
            heatmapMaterial.color = matColor;
            heatmapMaterial.mainTexture = heatmapTextureRaw;
            targetMeshRendererRaw.sharedMaterial = heatmapMaterial;

            // Animate Y dari hiddenY ke visibleY
            StartCoroutine(AnimateY(
                targetMeshRendererRaw.transform,
                hiddenY,
                visibleY,
                animationDuration
            ));

            // Setelah heatmap muncul, tampilkan hotspot prefab raw
            DisplayHotspotsRaw();
        }

        isShowingRawHeatmap = true;

        if (btnTextRaw != null)
            btnTextRaw.text = "Hide Raw Heatmap";
        showRawHeatmapButton.interactable = true;
    }

    // ---------------------------------------------------------------------------------------------------------
    // 14) Routine: Sembunyikan Heatmap “Raw”
    //     - Animasi turun, disable mesh, kembalikan material asli, hapus hotspot
    // ---------------------------------------------------------------------------------------------------------
    private IEnumerator HideRawHeatmapRoutine()
    {
        showRawHeatmapButton.interactable = false;
        var btnTextRaw = showRawHeatmapButton.GetComponentInChildren<TextMeshProUGUI>();
        if (btnTextRaw != null)
            btnTextRaw.text = "Hiding...";

        if (targetMeshRendererRaw != null)
            yield return AnimateY(
                targetMeshRendererRaw.transform,
                visibleY,
                hiddenY,
                animationDuration
            );

        if (targetMeshRendererRaw != null)
        {
            targetMeshRendererRaw.sharedMaterial = originalMaterialRaw;
            targetMeshRendererRaw.enabled = false;
        }

        // Hapus semua hotspot yang sudah dibuat (raw)
        HideHotspotsRaw();

        isShowingRawHeatmap = false;

        if (btnTextRaw != null)
            btnTextRaw.text = "Show Raw Heatmap";
        showRawHeatmapButton.interactable = true;
    }

    // ---------------------------------------------------------------------------------------------------------
    // 15) Asynchronous Generator: Heatmap “Fuzzy”
    //     - Loop semua marker di markerParentFuzzy, baca label text → float (Apparent Temperature)
    //     - Hitung IDW per‐pixel, mapping ke gradient heatColors
    //     - Simpan hasil ke heatmapTextureFuzzy
    //     - Serta simpan daftar top 5 hotspot (Transform)
    // ---------------------------------------------------------------------------------------------------------
    private IEnumerator GenerateFuzzyHeatmapAsync()
    {
        // Validasi awal
        if (targetMeshRendererFuzzy == null || markerParentFuzzy == null || heatmapMaterial == null)
        {
            Debug.LogError("HeatmapGenerator (Fuzzy): Pastikan MeshRenderer, Marker Parent, dan Heatmap Material di‐assign.");
            generateRoutineFuzzy = null;
            yield break;
        }

        // Ambil bounds mesh untuk koordinat world→UV
        Bounds meshBounds = targetMeshRendererFuzzy.bounds;
        Vector3 meshMin = meshBounds.min;
        Vector3 meshSize = meshBounds.size;

        // Buat Texture2D baru
        heatmapTextureFuzzy = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp
        };

        // Kumpulkan data marker: (Transform, worldPos, floatTemp)
        var markers = new List<(Transform tf, Vector3 pos, float temp)>();
        foreach (Transform child in markerParentFuzzy)
        {
            var label = child.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                // Label text diasumsikan misal "25.3°C" atau "25.3" → hilangkan "°C" lalu parse
                string txt = label.text.Replace("°C", "").Trim();
                if (float.TryParse(txt, out float at))
                {
                    markers.Add((child, child.position, at));
                }
            }
        }

        // Jika tidak ada marker, buat seluruh pixel transparan dan keluar
        if (markers.Count == 0)
        {
            Debug.LogWarning("HeatmapGenerator (Fuzzy): Tidak ada marker di markerParentFuzzy.");
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

        // Cari top 5 hottest (berdasarkan temp) → untuk radius lebih besar dan daftar hotspot
        markers.Sort((a, b) => b.temp.CompareTo(a.temp));
        topHotspotsFuzzy.Clear();
        for (int i = 0; i < Mathf.Min(5, markers.Count); i++)
            topHotspotsFuzzy.Add(markers[i].tf);

        // Precompute per‐marker data: (uv, temp, radius)
        var markerData = new List<(Vector2 uv, float temp, float radius)>();
        foreach (var (tf, worldPos, temp) in markers)
        {
            float u = (worldPos.x - meshMin.x) / meshSize.x;
            float v = (worldPos.z - meshMin.z) / meshSize.z;
            float radius = topHotspotsFuzzy.Contains(tf) ? hotspotInterpolationRadius : baseInterpolationRadius;
            markerData.Add((new Vector2(u, v), temp, radius));
        }

        // Cari min & max temperatur
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

        // Loop setiap pixel di texture
        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                // Inversi agar heatmap tidak ter-rotate 180°
                float u = 1f - (x / (float)(textureWidth - 1));
                float v = 1f - (y / (float)(textureHeight - 1));
                Vector2 uv = new Vector2(u, v);

                // Inverse‐Distance Weighting (IDW)
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

                // Jika tidak ada berat, gunakan dataMin
                float value = (sumW > 0f) ? (sumT / sumW) : dataMin;
                float tNorm = Mathf.InverseLerp(dataMin, dataMax, value);
                tNorm = Mathf.Clamp01(tNorm);

                // Interpolasi warna secara linear di gradient
                float scaled = tNorm * (gradientCount - 1);
                int indexLow = Mathf.FloorToInt(scaled);
                int indexHigh = Mathf.Clamp(indexLow + 1, 0, gradientCount - 1);
                float fract = scaled - indexLow;

                Color cLow = heatColors[indexLow];
                Color cHigh = heatColors[indexHigh];
                Color c = Color.Lerp(cLow, cHigh, fract);

                heatmapTextureFuzzy.SetPixel(x, y, c);
            }
            // Beri kesempatan Unity untuk tetap responsif
            yield return null;
        }

        heatmapTextureFuzzy.Apply();
        generateRoutineFuzzy = null;
    }

    // ---------------------------------------------------------------------------------------------------------
    // 16) Asynchronous Generator: Heatmap “Raw”
    //     - Prinsip sama seperti GenerateFuzzy, tetapi:
    //       • Gunakan markerParentRaw
    //       • Baca nilai Temperature_2m dari label
    //       • Simpan daftar top 5 hotspot (Transform)
    // ---------------------------------------------------------------------------------------------------------
    private IEnumerator GenerateRawHeatmapAsync()
    {
        // Validasi awal
        if (targetMeshRendererRaw == null || markerParentRaw == null || heatmapMaterial == null)
        {
            Debug.LogError("HeatmapGenerator (Raw): Pastikan MeshRenderer, Marker Parent, dan Heatmap Material di‐assign.");
            generateRoutineRaw = null;
            yield break;
        }

        // Ambil bounds mesh
        Bounds meshBounds = targetMeshRendererRaw.bounds;
        Vector3 meshMin = meshBounds.min;
        Vector3 meshSize = meshBounds.size;

        // Buat Texture2D baru
        heatmapTextureRaw = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp
        };

        // Kumpulkan data marker raw: (Transform, worldPos, floatTemp2m)
        var markers = new List<(Transform tf, Vector3 pos, float temp)>();
        foreach (Transform child in markerParentRaw)
        {
            var label = child.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                // Label diasumsikan menunjukkan nilai temperature_2m, contoh: "24.8°C"
                string txt = label.text.Replace("°C", "").Trim();
                if (float.TryParse(txt, out float t2m))
                {
                    markers.Add((child, child.position, t2m));
                }
            }
        }

        // Jika tidak ada marker, fill transparan dan keluar
        if (markers.Count == 0)
        {
            Debug.LogWarning("HeatmapGenerator (Raw): Tidak ada marker di markerParentRaw.");
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

        // Cari top 5 tertinggi berdasarkan t2m → untuk daftar hotspot
        markers.Sort((a, b) => b.temp.CompareTo(a.temp));
        topHotspotsRaw.Clear();
        for (int i = 0; i < Mathf.Min(5, markers.Count); i++)
            topHotspotsRaw.Add(markers[i].tf);

        // Precompute per‐marker data
        var markerData = new List<(Vector2 uv, float temp, float radius)>();
        foreach (var (tf, worldPos, temp) in markers)
        {
            float u = (worldPos.x - meshMin.x) / meshSize.x;
            float v = (worldPos.z - meshMin.z) / meshSize.z;
            float radius = topHotspotsRaw.Contains(tf) ? hotspotInterpolationRadius : baseInterpolationRadius;
            markerData.Add((new Vector2(u, v), temp, radius));
        }

        // Cari min & max t2m
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

        // Loop pixel
        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                float u = 1f - (x / (float)(textureWidth - 1));
                float v = 1f - (y / (float)(textureHeight - 1));
                Vector2 uv = new Vector2(u, v);

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

                float scaled = tNorm * (gradientCount - 1);
                int indexLow = Mathf.FloorToInt(scaled);
                int indexHigh = Mathf.Clamp(indexLow + 1, 0, gradientCount - 1);
                float fract = scaled - indexLow;

                Color cLow = heatColors[indexLow];
                Color cHigh = heatColors[indexHigh];
                Color c = Color.Lerp(cLow, cHigh, fract);

                heatmapTextureRaw.SetPixel(x, y, c);
            }
            yield return null;
        }

        heatmapTextureRaw.Apply();
        generateRoutineRaw = null;
    }

    // ---------------------------------------------------------------------------------------------------------
    // 17) Utility: AnimateY
    //     - Animasi perpindahan localPosition.y dari startY ke endY selama duration
    // ---------------------------------------------------------------------------------------------------------
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

    // ---------------------------------------------------------------------------------------------------------
    // 18) Display Hotspots “Fuzzy”
    //     - Instansiasi hotspotRingPrefabFuzzy dan hotspotMarkerPrefabFuzzy di lima titik terpanas
    // ---------------------------------------------------------------------------------------------------------
    private void DisplayHotspotsFuzzy()
    {
        // Hapus dulu kalau ada sisa instansi
        HideHotspotsFuzzy();

        foreach (var tf in topHotspotsFuzzy)
        {
            // 1) Instantiate ring tepat di tanah (posisi y mengikuti tf.position.y)
            if (hotspotRingPrefabFuzzy != null)
            {
                Vector3 ringPos = tf.position;
                // ring dipasang menempel ke tanah → y sama dengan marker
                GameObject ring = Instantiate(hotspotRingPrefabFuzzy, ringPos, Quaternion.identity, markerParentFuzzy);
                spawnedHotspotsFuzzy.Add(ring);
            }

            // 2) Instantiate marker sedikit di atas tanah (offset di sumbu Y)
            if (hotspotMarkerPrefabFuzzy != null)
            {
                Vector3 markerPos = tf.position;
                // marker diangkat, misalnya 1 unit di atas ring
                markerPos.y += 1f;
                GameObject marker = Instantiate(hotspotMarkerPrefabFuzzy, markerPos, Quaternion.identity, markerParentFuzzy);
                spawnedHotspotsFuzzy.Add(marker);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // 19) Hapus semua Hotspots “Fuzzy”
    // ---------------------------------------------------------------------------------------------------------
    private void HideHotspotsFuzzy()
    {
        for (int i = spawnedHotspotsFuzzy.Count - 1; i >= 0; i--)
        {
            if (spawnedHotspotsFuzzy[i] != null)
                Destroy(spawnedHotspotsFuzzy[i]);
        }
        spawnedHotspotsFuzzy.Clear();
    }

    // ---------------------------------------------------------------------------------------------------------
    // 20) Display Hotspots “Raw”
    //     - Instansiasi hotspotRingPrefabRaw dan hotspotMarkerPrefabRaw di lima titik terpanas raw
    // ---------------------------------------------------------------------------------------------------------
    private void DisplayHotspotsRaw()
    {
        // Hapus dulu kalau ada sisa instansi
        HideHotspotsRaw();

        foreach (var tf in topHotspotsRaw)
        {
            // 1) Instantiate ring tepat di tanah
            if (hotspotRingPrefabRaw != null)
            {
                Vector3 ringPos = tf.position;
                GameObject ring = Instantiate(hotspotRingPrefabRaw, ringPos, Quaternion.identity, markerParentRaw);
                spawnedHotspotsRaw.Add(ring);
            }

            // 2) Instantiate marker sedikit di atas tanah
            if (hotspotMarkerPrefabRaw != null)
            {
                Vector3 markerPos = tf.position;
                markerPos.y += 1f;
                GameObject marker = Instantiate(hotspotMarkerPrefabRaw, markerPos, Quaternion.identity, markerParentRaw);
                spawnedHotspotsRaw.Add(marker);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // 21) Hapus semua Hotspots “Raw”
    // ---------------------------------------------------------------------------------------------------------
    private void HideHotspotsRaw()
    {
        for (int i = spawnedHotspotsRaw.Count - 1; i >= 0; i--)
        {
            if (spawnedHotspotsRaw[i] != null)
                Destroy(spawnedHotspotsRaw[i]);
        }
        spawnedHotspotsRaw.Clear();
    }
}
