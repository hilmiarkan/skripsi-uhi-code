using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Event handler untuk menandakan proses fuzzy sudah selesai dan
/// untuk meng‐update marker fuzzy apabila file fuzzy_*.csv sudah ada.
/// </summary>
public class FuzzyRuntimeCalculator : MonoBehaviour
{
    [Header("UI Settings")]
    public Button fetchButton;

    [Header("Hotspots Display")]
    public TextMeshProUGUI hotspotsText;

    [Header("Markers Parent")]
    [Tooltip("Parent objek yang menampung semua marker (baik urban maupun rural) untuk fuzzy.")]
    public Transform markerParent;

    [Header("Fetched CSVs Folder (under Assets)")]
    public string fetchedDataFolder = "FetchedData"; // Folder raw

    [Header("UHI Result Display")]
    public TextMeshProUGUI uhiResultText;

    [Header("Prefabs for Hotspot Visualization")]
    [Tooltip("Prefab untuk ring effect (menempel di tanah)")]
    public GameObject hotspotRingPrefab;
    [Tooltip("Prefab untuk marker yang berotasi (sedikit di atas tanah)")]
    public GameObject hotspotMarkerPrefab;
    [Tooltip("Offset ketinggian untuk marker (agar 'mengambang' di atas tanah)")]
    public float markerHeightOffset = 2f;
    [Tooltip("Offset ketinggian untuk ring effect (agar menempel di tanah)")]
    public float ringHeightOffset = 0.1f;

    // Event yang di-trigger ketika proses fuzzy selesai
    public event Action OnFuzzyComplete;

    // Variables for fuzzy logic (must match CSV headers)
    private readonly string[] variables = new string[]
    {
        "temperature_2m", "dew_point_2m", "relative_humidity_2m",
        "wind_speed_10m", "vapour_pressure_deficit", "evapotranspiration"
    };

    // --- Parameter fuzzy output ---
    private const float OUT_MIN = 15f;
    private const float OUT_MAX = 45f;
    private const int OUT_RESOLUTION = 1000; // sama dengan x_out = np.linspace(15, 45, 1000)
    private float[] outUniverse = new float[OUT_RESOLUTION];
    private float[][] outputMfs = new float[5][]; // 5 MF output

    // Definisi aturan fuzzy (12 rules, masing-masing 7 int: 6 input terms + 1 output term)
    private int[,] rules = new int[12, 7]
    {
        {3, 0, 1, 1, 0, 0, 5},
        {3, 0, 1, 3, 0, 0, 4},
        {2, 2, 2, 0, 0, 0, 2},
        {1, 0, 0, 3, 0, 0, 1},
        {3, 0, 0, 0, 3, 3, 4},
        {0, 3, 3, 0, 0, 0, 3},
        {2, 0, 0, 3, 0, 0, 1},
        {0, 0, 3, 0, 1, 1, 1},
        {3, 3, 0, 3, 0, 0, 3},
        {2, 1, 1, 0, 0, 0, 1},
        {1, 0, 3, 0, 0, 0, 1},
        {3, 0, 0, 0, 0, 0, 4}
    };

    // Daftar 5 hotspot teratas (Transform) berdasarkan nilai fuzzy (Apparent Temperature)
    private List<Transform> topHotspotsFuzzy = new List<Transform>();

    void Awake()
    {
        // Inisialisasi universe output dan output MFs
        float step = (OUT_MAX - OUT_MIN) / (OUT_RESOLUTION - 1);
        for (int i = 0; i < OUT_RESOLUTION; i++)
        {
            outUniverse[i] = OUT_MIN + i * step;
        }

        // Alokasikan array untuk masing-masing MF output
        for (int k = 0; k < 5; k++)
        {
            outputMfs[k] = new float[OUT_RESOLUTION];
        }

        // MF1: 'Sejuk' = trapmf(x_out, 15,15,18,22)
        // MF2: 'Nyaman' = trimf(x_out, 18,24,30)
        // MF3: 'Hangat' = trimf(x_out, 25,30,35)
        // MF4: 'Panas' = trimf(x_out, 30,35,40)
        // MF5: 'Sangat Panas' = trapmf(x_out, 38,42,45,45)
        for (int i = 0; i < OUT_RESOLUTION; i++)
        {
            float x = outUniverse[i];
            outputMfs[0][i] = TrapMF(x, 15f, 15f, 18f, 22f);      // 'Sejuk'
            outputMfs[1][i] = TriMF(x, 18f, 24f, 30f);            // 'Nyaman'
            outputMfs[2][i] = TriMF(x, 25f, 30f, 35f);            // 'Hangat'
            outputMfs[3][i] = TriMF(x, 30f, 35f, 40f);            // 'Panas'
            outputMfs[4][i] = TrapMF(x, 38f, 42f, 45f, 45f);      // 'Sangat Panas'
        }
    }

    // Trapezoidal membership function
    private static float TrapMF(float x, float a, float b, float c, float d)
    {
        if (x <= a || x >= d) return 0f;
        float left = (b == a) ? 1f : Mathf.Clamp01((x - a) / (b - a));
        float right = (d == c) ? 1f : Mathf.Clamp01((d - x) / (d - c));
        return Mathf.Max(Mathf.Min(left, right), 0f);
    }

    // Triangular membership function
    private static float TriMF(float x, float a, float b, float c)
    {
        if (x <= a || x >= c) return 0f;
        if (Mathf.Approximately(x, b)) return 1f;
        if (x > a && x < b) return (x - a) / (b - a);
        return (c - x) / (c - b);
    }

    /// <summary>
    /// Dipanggil dari luar (OpenMeteoCsvGenerator) untuk memulai proses fuzzy
    /// berdasarkan path CSV raw yang diberikan.
    /// Namun sebelum benar-benar menjalankan, dicek dulu apakah file fuzzy_*.csv sudah ada.
    /// </summary>
    public void ProcessCsv(string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            Debug.LogError($"Fuzzy: CSV raw tidak ditemukan: {csvPath}");
            return;
        }

        // Tentukan nama file fuzzy yang seharusnya berpasangan
        string meteoName = Path.GetFileNameWithoutExtension(csvPath); // ex: "meteo_20250604_140000"
        string ts = meteoName.Replace("meteo_", "");                  // ex: "20250604_140000"
        string fuzzyFolder = Path.Combine(Application.dataPath, "FuzzyResults");
        Directory.CreateDirectory(fuzzyFolder);
        string expectedFuzzyName = $"fuzzy_{ts}.csv";
        string expectedFuzzyFullPath = Path.Combine(fuzzyFolder, expectedFuzzyName);

        if (File.Exists(expectedFuzzyFullPath))
        {
            // Jika sudah ada, skip proses fuzzy, langsung update marker fuzzy & instantiate hotspot
            Debug.Log($"[Fuzzy] File fuzzy '{expectedFuzzyName}' sudah ada, skip proses.");
            UpdateExistingFuzzy(expectedFuzzyFullPath);
            OnFuzzyComplete?.Invoke();
            return;
        }

        // Jika belum ada, disable tombol dan mulai coroutine
        if (fetchButton != null)
            fetchButton.interactable = false;

        StartCoroutine(ProcessCsvRoutine(csvPath));
    }

    /// <summary>
    /// Routine untuk menghitung fuzzy (Mamdani) dari file CSV raw, lalu menyimpan hasil ke fuzzy_*.csv.
    /// Setelah selesai, update label markerParent dan instantiate hotspot fuzzy.
    /// </summary>
    private IEnumerator ProcessCsvRoutine(string csvPath)
    {
        // Prepare fuzzy results output (header)
        List<string> fuzzyLines = new List<string> { "longitude,latitude,apparent_temperature" };
        // Sekarang kita simpan data (lon, lat, at) untuk setiap titik
        List<(float lon, float lat, float at)> atDataList = new List<(float, float, float)>();

        string[] lines = File.ReadAllLines(csvPath);
        int totalPoints = Mathf.Max(0, lines.Length - 1);
        int processed = 0;
        if (lines.Length < 2)
        {
            yield break;
        }

        // Ambil indeks kolom pada header
        var header = lines[0].Split(',');
        int lonIdx = Array.IndexOf(header, "longitude");
        int latIdx = Array.IndexOf(header, "latitude");
        int[] varIdx = variables.Select(v => Array.IndexOf(header, v)).ToArray();

        // --- Mulai looping baris ---
        for (int r = 1; r < lines.Length; r++)
        {
            processed++;
            // Update tombol text dengan progress (misal "23/1331 titik")
            if (fetchButton != null)
            {
                var btnText = fetchButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                    btnText.text = $"{processed}/{totalPoints} titik";
            }

            var cols = lines[r].Split(',');
            float lon = float.Parse(cols[lonIdx], CultureInfo.InvariantCulture);
            float lat = float.Parse(cols[latIdx], CultureInfo.InvariantCulture);
            float[] inputs = new float[variables.Length];
            for (int j = 0; j < variables.Length; j++)
            {
                inputs[j] = float.Parse(cols[varIdx[j]], CultureInfo.InvariantCulture);
            }

            // Hitung membership input
            float[][] m_vals = ComputeInputMfs(inputs);

            // Lakukan inferensi Mamdani -> defuzzifikasi
            float at = InferAt(m_vals);

            if (!float.IsNaN(at))
                atDataList.Add((lon, lat, at));

            // Update marker label di scene (jika ada)
            foreach (Transform child in markerParent)
            {
                var info = child.GetComponent<MarkerInfo>();
                if (info != null &&
                    Mathf.Abs(info.longitude - lon) < 1e-4f &&
                    Mathf.Abs(info.latitude - lat) < 1e-4f)
                {
                    var label = child.GetComponentInChildren<TextMeshProUGUI>();
                    if (label != null)
                        label.text = float.IsNaN(at) ? "--" : at.ToString("F1") + "°C";
                    break;
                }
            }

            // Simpan hasil fuzzy untuk CSV
            fuzzyLines.Add(
                $"{lon.ToString(CultureInfo.InvariantCulture)}," +
                $"{lat.ToString(CultureInfo.InvariantCulture)}," +
                $"{(float.IsNaN(at) ? -1f : at).ToString("F1", CultureInfo.InvariantCulture)}"
            );

            yield return null; // bisa dioptimasi dengan yield setiap beberapa iterasi saja
        }

        // Write fuzzy results ke folder Assets/FuzzyResults
        string outDir = Path.Combine(Application.dataPath, "FuzzyResults");
        Directory.CreateDirectory(outDir);

        // File name berdasarkan timestamp yang sama dengan CSV meteo
        string meteoName = Path.GetFileNameWithoutExtension(csvPath); // ex: "meteo_20250604_140000"
        string ts2 = meteoName.Replace("meteo_", "");
        string fname = $"fuzzy_{ts2}.csv";
        string fullOut = Path.Combine(outDir, fname);

        File.WriteAllLines(fullOut, fuzzyLines);
        Debug.Log($"[Fuzzy] Results saved to: {fullOut}");

    #if UNITY_EDITOR
        AssetDatabase.Refresh();
    #endif

        // Setelah selesai looping, panggil event OnFuzzyComplete
        OnFuzzyComplete?.Invoke();

        // --- Hitung UHI & tampilkan 5 hotspot, serta instantiate prefab untuk fuzzy ---
        DisplayHotspotsFromFuzzyList(atDataList);
    }

    /// <summary>
    /// Metode untuk langsung meng‐update markerParent dan instantiate hotspot fuzzy
    /// dari file CSV fuzzy_*.csv yang sudah ada (tanpa generate ulang).
    /// </summary>
    /// <param name="fuzzyCsvPath">Path lengkap ke file fuzzy_YYYYMMDD_HHMMSS.csv</param>
    public void UpdateExistingFuzzy(string fuzzyCsvPath)
    {
        if (!File.Exists(fuzzyCsvPath))
        {
            Debug.LogError($"Fuzzy: CSV fuzzy tidak ditemukan: {fuzzyCsvPath}");
            return;
        }

        // Baca semua baris CSV
        string[] allLines;
        try
        {
            allLines = File.ReadAllLines(fuzzyCsvPath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Gagal membaca file CSV fuzzy '{fuzzyCsvPath}': {ex.Message}");
            return;
        }

        if (allLines.Length <= 1)
        {
            Debug.LogWarning($"File CSV fuzzy '{fuzzyCsvPath}' hanya memiliki header atau kosong.");
            return;
        }

        // Siapkan list (lon, lat, at) untuk setiap titik urban
        var urbanAtData = new List<(float lon, float lat, float at)>();

        // Ambil indeks kolom di header
        var headerF = allLines[0].Split(',');
        int lonIdxF = Array.IndexOf(headerF, "longitude");
        int latIdxF = Array.IndexOf(headerF, "latitude");
        int atIdxF = Array.IndexOf(headerF, "apparent_temperature");

        // Loop tiap baris (skip header)
        for (int i = 1; i < allLines.Length; i++)
        {
            var cols = allLines[i].Split(',');
            if (cols.Length < 3) continue;

            if (float.TryParse(cols[lonIdxF], NumberStyles.Float, CultureInfo.InvariantCulture, out float lon) &&
                float.TryParse(cols[latIdxF], NumberStyles.Float, CultureInfo.InvariantCulture, out float lat) &&
                float.TryParse(cols[atIdxF], NumberStyles.Float, CultureInfo.InvariantCulture, out float at))
            {
                // Update label markerParent (jika markerParent di‐design berisi objek child dengan komponen MarkerInfo)
                foreach (Transform child in markerParent)
                {
                    var info = child.GetComponent<MarkerInfo>();
                    if (info != null &&
                        Mathf.Abs(info.longitude - lon) < 1e-4f &&
                        Mathf.Abs(info.latitude - lat) < 1e-4f)
                    {
                        var label = child.GetComponentInChildren<TextMeshProUGUI>();
                        if (label != null)
                            label.text = at.ToString("F1") + "°C";
                        if (!info.isRural)
                            urbanAtData.Add((lon, lat, at));
                        break;
                    }
                }
            }
        }

        // Tampilkan hotspot fuzzy: ambil 5 nilai AT tertinggi dari urbanAtData
        var top5 = urbanAtData
            .OrderByDescending(item => item.at)
            .Take(5)
            .ToList();

        // Tampilkan teks di UI
        if (hotspotsText != null)
        {
            string hotspotStr = "5 Hotspots (Urban - Fuzzy):\n";
            for (int i = 0; i < top5.Count; i++)
            {
                hotspotStr += $"{i + 1}. {top5[i].at:F1}°C (Lon: {top5[i].lon:F4}, Lat: {top5[i].lat:F4})\n";
            }
            if (top5.Count < 5)
                hotspotStr += "(data tidak cukup)\n";
            hotspotStr += "\nObjective: 5/5 Hotspot belum dimitigasi";
            hotspotsText.text = hotspotStr;
        }

        // Instantiate prefab hotspot fuzzy—menggunakan rotasi prefab asli
        foreach (var data in top5)
        {
            foreach (Transform child in markerParent)
            {
                var info = child.GetComponent<MarkerInfo>();
                if (info != null &&
                    !info.isRural &&
                    Mathf.Abs(info.longitude - data.lon) < 1e-4f &&
                    Mathf.Abs(info.latitude - data.lat) < 1e-4f)
                {
                    Vector3 groundPos = child.position;

                    // 1) Instantiate ring effect dengan rotasi prefab aslinya
                    if (hotspotRingPrefab != null)
                    {
                        Vector3 ringPos = new Vector3(
                            groundPos.x,
                            groundPos.y + ringHeightOffset,
                            groundPos.z
                        );
                        GameObject ring = Instantiate(
                            hotspotRingPrefab,
                            ringPos,
                            hotspotRingPrefab.transform.rotation  // gunakan rotasi prefab aslinya
                        );
                        ring.name = $"HotspotRing_Fuzzy_{data.lon:F4}_{data.lat:F4}";
                    }

                    // 2) Instantiate rotating marker di atas tanah + offset dengan rotasi prefab aslinya
                    if (hotspotMarkerPrefab != null)
                    {
                        Vector3 markerPos = new Vector3(
                            groundPos.x,
                            groundPos.y + markerHeightOffset,
                            groundPos.z
                        );
                        GameObject marker = Instantiate(
                            hotspotMarkerPrefab,
                            markerPos,
                            hotspotMarkerPrefab.transform.rotation  // gunakan rotasi prefab aslinya
                        );
                        marker.name = $"HotspotMarker_Fuzzy_{data.lon:F4}_{data.lat:F4}";
                    }

                    break; // Setelah ketemu, keluar loop child
                }
            }
        }
    }

    /// <summary>
    /// Setelah fuzzy selesai (baik dari ProcessCsvRoutine maupun UpdateExistingFuzzy), 
    /// metode ini men‐instantiate hotspot berdasarkan data atDataList yang sudah ada.
    /// </summary>
    private void DisplayHotspotsFromFuzzyList(List<(float lon, float lat, float at)> atDataList)
    {
        // Compute mean rural AT
        float sumRural = 0f;
        int countRural = 0;
        foreach (Transform child in markerParent)
        {
            var info = child.GetComponent<MarkerInfo>();
            var label = child.GetComponentInChildren<TextMeshProUGUI>();
            if (info != null && label != null && info.isRural)
            {
                string txt = label.text.Replace("°C", "");
                if (float.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out float val) &&
                    !float.IsNaN(val))
                {
                    sumRural += val;
                    countRural++;
                }
            }
        }
        float meanRural = countRural > 0 ? (sumRural / countRural) : 0f;

        // Compute UHI differences untuk urban points
        float sumDiff = 0f;
        int countUrban = 0;
        foreach (Transform child in markerParent)
        {
            var info = child.GetComponent<MarkerInfo>();
            var label = child.GetComponentInChildren<TextMeshProUGUI>();
            if (info != null && label != null && !info.isRural)
            {
                string txt = label.text.Replace("°C", "");
                if (float.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out float val) &&
                    !float.IsNaN(val))
                {
                    sumDiff += (val - meanRural);
                    countUrban++;
                }
            }
        }
        float uhiIntensity = (countUrban > 0) ? (sumDiff / countUrban) : 0f;
        if (uhiResultText != null)
            uhiResultText.text = $"UHI Intensity (fuzzy): {uhiIntensity:F1}°C";

        // Display top 5 hotspots dan instantiate prefab fuzzy
        if (hotspotsText != null)
        {
            // Urutkan atDataList secara descending berdasarkan at hanya untuk urban
            var urbanAtData = atDataList
                .Where(data =>
                {
                    // Cari child di markerParent yang matching lon/lat untuk cek isRural
                    foreach (Transform child in markerParent)
                    {
                        var info = child.GetComponent<MarkerInfo>();
                        if (info != null &&
                            Mathf.Abs(info.longitude - data.lon) < 1e-4f &&
                            Mathf.Abs(info.latitude - data.lat) < 1e-4f)
                        {
                            return !info.isRural;
                        }
                    }
                    return false;
                })
                .OrderByDescending(item => item.at)
                .Take(5)
                .ToList();

            string hotspotStr = "5 Hotspots (Urban - Fuzzy):\n";
            for (int i = 0; i < urbanAtData.Count; i++)
            {
                hotspotStr += $"{i + 1}. {urbanAtData[i].at:F1}°C (Lon: {urbanAtData[i].lon:F4}, Lat: {urbanAtData[i].lat:F4})\n";
            }
            if (urbanAtData.Count < 5)
                hotspotStr += "(data tidak cukup)\n";
            hotspotStr += "\nObjective: 5/5 Hotspot belum dimitigasi";
            hotspotsText.text = hotspotStr;

            // Untuk setiap hotspot (urban), cari child di markerParent dan instantiate prefab
            foreach (var data in urbanAtData)
            {
                foreach (Transform child in markerParent)
                {
                    var info = child.GetComponent<MarkerInfo>();
                    if (info != null &&
                        !info.isRural &&
                        Mathf.Abs(info.longitude - data.lon) < 1e-4f &&
                        Mathf.Abs(info.latitude - data.lat) < 1e-4f)
                    {
                        // Posisi marker (child.position)
                        Vector3 groundPos = child.position;

                        // 1) Instantiate ring effect menggunakan rotasi prefab aslinya
                        if (hotspotRingPrefab != null)
                        {
                            Vector3 ringPos = new Vector3(
                                groundPos.x,
                                groundPos.y + ringHeightOffset,
                                groundPos.z
                            );
                            GameObject ring = Instantiate(
                                hotspotRingPrefab,
                                ringPos,
                                hotspotRingPrefab.transform.rotation  // rotasi aslinya
                            );
                            ring.name = $"HotspotRing_Fuzzy_{data.lon:F4}_{data.lat:F4}";
                        }

                        // 2) Instantiate rotating marker di atas tanah + offset menggunakan rotasi prefab aslinya
                        if (hotspotMarkerPrefab != null)
                        {
                            Vector3 markerPos = new Vector3(
                                groundPos.x,
                                groundPos.y + markerHeightOffset,
                                groundPos.z
                            );
                            GameObject marker = Instantiate(
                                hotspotMarkerPrefab,
                                markerPos,
                                hotspotMarkerPrefab.transform.rotation  // rotasi aslinya
                            );
                            marker.name = $"HotspotMarker_Fuzzy_{data.lon:F4}_{data.lat:F4}";
                        }

                        break; // Setelah ketemu, keluar loop child
                    }
                }
            }
        }
    }

    /// <summary>
    /// Menghitung derajat keanggotaan untuk setiap input (6 variabel), 
    /// menghasilkan array 6x3 (masing-masing input punya 3 MF).
    /// </summary>
    private float[][] ComputeInputMfs(float[] inputs)
    {
        // inputs: [temp, dew, rh, wind, vpd, et]
        float temp = inputs[0];
        float dew = inputs[1];
        float rh = inputs[2];
        float wind = inputs[3];
        float vpd = inputs[4];
        float et = inputs[5];

        float[] m1 = new float[3]
        {
            TrapMF(temp, 15f, 15f, 18f, 22f),    // Temperature 2m: 'Rendah'
            TriMF(temp, 18f, 25f, 32f),          // Temperature 2m: 'Sedang'
            TrapMF(temp, 28f, 32f, 35f, 35f)     // Temperature 2m: 'Tinggi'
        };

        float[] m2 = new float[3]
        {
            TrapMF(dew, 10f, 10f, 13f, 17f),     // Dew Point 2m: 'Rendah'
            TriMF(dew, 13f, 18f, 23f),           // Dew Point 2m: 'Sedang'
            TriMF(dew, 20f, 23f, 25f)            // Dew Point 2m: 'Tinggi'
        };

        float[] m3 = new float[3]
        {
            TrapMF(rh, 40f, 40f, 50f, 60f),      // Relative Humidity 2m: 'Rendah'
            TriMF(rh, 50f, 65f, 80f),            // Relative Humidity 2m: 'Sedang'
            TrapMF(rh, 75f, 85f, 100f, 100f)     // Relative Humidity 2m: 'Tinggi'
        };

        float[] m4 = new float[3]
        {
            TrapMF(wind, 0f, 0f, 3f, 7f),        // Wind Speed 10m: 'Tenang'
            TriMF(wind, 5f, 10f, 15f),           // Wind Speed 10m: 'Sedang'
            TrapMF(wind, 12f, 16f, 20f, 20f)     // Wind Speed 10m: 'Kencang'
        };

        float[] m5 = new float[3]
        {
            TrapMF(vpd, 0f, 0f, 0.5f, 1f),       // Vapour Pressure Deficit: 'Rendah'
            TriMF(vpd, 0.5f, 1.5f, 2.5f),        // Vapour Pressure Defisit: 'Sedang'
            TrapMF(vpd, 2f, 2.5f, 3f, 3f)        // Vapour Pressure Defisit: 'Tinggi'
        };

        float[] m6 = new float[3]
        {
            TrapMF(et, 0f, 0f, 1f, 2f),          // Evapotranspiration: 'Rendah'
            TriMF(et, 1.5f, 3f, 4.5f),           // Evapotranspiration: 'Sedang'
            TrapMF(et, 3.5f, 5f, 6f, 6f)         // Evapotranspiration: 'Tinggi'
        };

        return new float[][] { m1, m2, m3, m4, m5, m6 };
    }

    /// <summary>
    /// Melakukan inferensi Mamdani dan defuzzifikasi (centroid), 
    /// mengembalikan nilai crisp at_predicted.
    /// </summary>
    private float InferAt(float[][] m_vals)
    {
        // m_vals: array 6x3
        // Kumpulkan aktivasi untuk setiap rule
        List<(float alpha, int outTerm)> activations = new List<(float, int)>();

        for (int ri = 0; ri < 12; ri++)
        {
            List<float> degrees = new List<float>();
            for (int i = 0; i < 6; i++)
            {
                int termIdx = rules[ri, i];
                if (termIdx != 0)
                {
                    degrees.Add(m_vals[i][termIdx - 1]);
                }
            }
            float alpha = (degrees.Count > 0) ? degrees.Min() : 0f;
            int outputTerm = rules[ri, 6] - 1; // ubah jadi 0-based index
            activations.Add((alpha, outputTerm));
        }

        // Aggregate (clip) output MFs
        float[] aggregated = new float[OUT_RESOLUTION];
        for (int k = 0; k < OUT_RESOLUTION; k++)
            aggregated[k] = 0f;

        foreach (var (alpha, outTerm) in activations)
        {
            if (alpha > 0f)
            {
                float[] mf = outputMfs[outTerm];
                for (int i = 0; i < OUT_RESOLUTION; i++)
                {
                    float clipped = Mathf.Min(alpha, mf[i]);
                    if (clipped > aggregated[i])
                        aggregated[i] = clipped;
                }
            }
        }

        // Defuzzifikasi centroid
        float num = 0f, den = 0f;
        for (int i = 0; i < OUT_RESOLUTION; i++)
        {
            num += outUniverse[i] * aggregated[i];
            den += aggregated[i];
        }
        if (Mathf.Approximately(den, 0f)) return float.NaN;
        return num / den;
    }

    /// <summary>
    /// METODE BARU: Hitung UHI DARI RAW CSV
    /// Prinsip: 
    /// 1) Pisahkan baris "urban" & "rural" berdasarkan kolom "type"
    /// 2) Hitung rata-rata temperature_2m untuk urban dan untuk rural 
    /// 3) UHI = avgUrban - avgRural 
    /// 4) Tampilkan di uhiResultText.
    /// </summary>
    public void ComputeUhiFromRaw(string rawCsvPath)
    {
        if (!File.Exists(rawCsvPath))
        {
            Debug.LogError($"ComputeUhiFromRaw: File CSV raw tidak ditemukan: {rawCsvPath}");
            return;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(rawCsvPath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"ComputeUhiFromRaw: Gagal membaca file '{rawCsvPath}': {ex.Message}");
            return;
        }

        if (lines.Length <= 1)
        {
            Debug.LogWarning($"ComputeUhiFromRaw: File '{rawCsvPath}' kosong atau hanya berisi header.");
            if (uhiResultText != null)
                uhiResultText.text = "UHI: data tidak lengkap";
            return;
        }

        // Ambil indeks kolom 'type' dan 'temperature_2m' dari header
        var header = lines[0].Split(',');
        int idxType = Array.IndexOf(header, "type");            // biasanya kolom 0
        int idxTemp = Array.IndexOf(header, "temperature_2m");  // biasanya kolom 3

        if (idxType < 0 || idxTemp < 0)
        {
            Debug.LogError("ComputeUhiFromRaw: Kolom 'type' atau 'temperature_2m' tidak ditemukan di header CSV.");
            if (uhiResultText != null)
                uhiResultText.text = "UHI: header CSV tidak valid";
            return;
        }

        float sumUrban = 0f, sumRural = 0f;
        int countUrban = 0, countRural = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split(',');
            if (cols.Length <= Mathf.Max(idxType, idxTemp))
                continue;

            string tipe = cols[idxType].Trim().ToLowerInvariant();
            if (float.TryParse(cols[idxTemp], NumberStyles.Float, CultureInfo.InvariantCulture, out float t2m))
            {
                if (tipe == "urban")
                {
                    sumUrban += t2m;
                    countUrban++;
                }
                else if (tipe == "rural")
                {
                    sumRural += t2m;
                    countRural++;
                }
            }
        }

        if (countUrban == 0 || countRural == 0)
        {
            Debug.LogWarning("ComputeUhiFromRaw: Data urban atau rural tidak lengkap (jumlah = 0).");
            if (uhiResultText != null)
                uhiResultText.text = "UHI: data tidak lengkap";
            return;
        }

        float avgUrban = sumUrban / countUrban;
        float avgRural = sumRural / countRural;
        float uhiIntensity = avgUrban - avgRural;

        // Tampilkan ke UI
        if (uhiResultText != null)
            uhiResultText.text = $"UHI Intensity (raw): {uhiIntensity:F1}°C";
        Debug.Log($"ComputeUhiFromRaw: avgUrban={avgUrban:F2}, avgRural={avgRural:F2}, UHI={uhiIntensity:F2}");
    }
}
