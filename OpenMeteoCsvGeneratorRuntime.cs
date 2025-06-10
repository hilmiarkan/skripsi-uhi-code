using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Newtonsoft.Json.Linq;  // Pastikan Newtonsoft.Json.dll sudah di‐import ke Plugins

public class OpenMeteoCsvGeneratorRuntime : MonoBehaviour
{
    [Header("Csv TextAssets (place under Resources)")]
    public TextAsset urbanCsv;   // harus berisi kolom: X (lon), Y (lat)
    public TextAsset ruralCsv;   // harus berisi kolom: X (lon), Y (lat), VALUE (IGNORE VALUE)

    [Header("UI Elements")]
    public Button fetchButton;
    public TextMeshProUGUI statusText;

    [Header("Generated CSVs Folder (relative to Assets)")]
    public string fetchedDataFolder = "FetchedData"; // di dalam Assets/FetchedData

    [Header("References")]
    public FuzzyRuntimeCalculator fuzzyCalculator;
    public HotspotManager hotspotManager;

    private Coroutine fetchTimerCoroutine;
    private DateTime fetchStartTime;

    // TimeZone Jakarta:
    // di Windows gunakan "SE Asia Standard Time", di macOS/Linux gunakan "Asia/Jakarta"
    private readonly TimeZoneInfo jakartaTz = TimeZoneInfo.FindSystemTimeZoneById(
    #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            "SE Asia Standard Time"
    #else
            "Asia/Jakarta"
    #endif
    );

    private void Awake()
    {
        if (fetchButton != null)
            fetchButton.onClick.AddListener(OnFetchButtonClicked);
    }

    private IEnumerator FetchTimer()
    {
        int sec = 0;
        while (true)
        {
            if (statusText != null)
                statusText.text = $"Fetching data... {sec}s";
            sec++;
            yield return new WaitForSeconds(1f);
        }
    }

    private void OnFetchButtonClicked()
    {
        fetchStartTime = DateTime.UtcNow;
        fetchTimerCoroutine = StartCoroutine(FetchTimer());
        StartCoroutine(FetchRoutine());
    }

    private IEnumerator FetchRoutine()
    {
        // Disable UI dan mulai proses fetching
        if (fetchButton != null) fetchButton.interactable = false;
        if (statusText != null) statusText.text = "Fetching data...";

        // 1. Hitung waktu target (dibulatkan ke jam penuh) di Jakarta
        DateTime nowUtc = DateTime.UtcNow;
        DateTime nowJakarta = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, jakartaTz);
        DateTime targetJakarta = new DateTime(
            nowJakarta.Year, nowJakarta.Month, nowJakarta.Day,
            nowJakarta.Hour, 0, 0,
            DateTimeKind.Unspecified
        );
        DateTime targetUtc = TimeZoneInfo.ConvertTimeToUtc(targetJakarta, jakartaTz);
        string targetTimeString = targetUtc.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

        // 2. Siapkan StringBuilder untuk CSV
        List<string> lines = new List<string>();
        lines.Add("type,longitude,latitude,temperature_2m,dew_point_2m,relative_humidity_2m,wind_speed_10m,vapour_pressure_deficit,evapotranspiration,wind_knots,apparent_temperature");

        // 3. Kumpulkan daftar titik (type, lon, lat) dari urbanCsv dan ruralCsv
        var points = new List<(string type, float lon, float lat)>();

        // 3a. Baca urbanCsv
        if (urbanCsv != null)
        {
            var urbanLines = urbanCsv.text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < urbanLines.Length; i++)
            {
                var cols = urbanLines[i].Split(',');
                if (cols.Length < 2) continue;
                if (float.TryParse(cols[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float lon) &&
                    float.TryParse(cols[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float lat))
                {
                    points.Add(("urban", lon, lat));
                }
            }
        }
        else
        {
            Debug.LogWarning("urbanCsv belum di-assign di Inspector.");
        }

        // 3b. Baca ruralCsv
        if (ruralCsv != null)
        {
            var ruralLines = ruralCsv.text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < ruralLines.Length; i++)
            {
                var cols = ruralLines[i].Split(',');
                if (cols.Length < 2) continue;
                if (float.TryParse(cols[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float lon) &&
                    float.TryParse(cols[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float lat))
                {
                    points.Add(("rural", lon, lat));
                }
            }
        }
        else
        {
            Debug.LogWarning("ruralCsv belum di-assign di Inspector.");
        }

        // 4. Loop setiap titik, kirim request ke Open-Meteo
        int total = points.Count;
        for (int i = 0; i < total; i++)
        {
            var (pointType, lon, lat) = points[i];
            string dateString = targetJakarta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string url = $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString(CultureInfo.InvariantCulture)}&longitude={lon.ToString(CultureInfo.InvariantCulture)}" +
                         $"&hourly=temperature_2m,dew_point_2m,relative_humidity_2m,wind_speed_10m,vapour_pressure_deficit,et0_fao_evapotranspiration,apparent_temperature" +
                         $"&start_date={dateString}&end_date={dateString}&timezone=UTC";

            using (UnityWebRequest uwr = UnityWebRequest.Get(url))
            {
                yield return uwr.SendWebRequest();

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Error fetching {pointType} @({lat},{lon}): {uwr.error}");
                    continue;
                }

                try
                {
                    JObject json = JObject.Parse(uwr.downloadHandler.text);
                    JObject hourly = json["hourly"] as JObject;
                    if (hourly == null)
                    {
                        Debug.LogWarning($"Field 'hourly' tidak ditemukan untuk titik ({lat},{lon}).");
                        continue;
                    }

                    JArray times = hourly["time"] as JArray;
                    int idx = -1;
                    for (int ti = 0; ti < times.Count; ti++)
                    {
                        string t = times[ti].ToString();
                        if (t.Equals(targetTimeString, StringComparison.Ordinal))
                        {
                            idx = ti;
                            break;
                        }
                    }
                    if (idx < 0)
                    {
                        Debug.LogWarning($"Timestamp '{targetTimeString}' tidak ada untuk titik ({lat},{lon}).");
                        continue;
                    }

                    float temperature  = hourly["temperature_2m"][idx].Value<float>();
                    float dewPoint     = hourly["dew_point_2m"][idx].Value<float>();
                    float relHum       = hourly["relative_humidity_2m"][idx].Value<float>();
                    float windSpeed    = hourly["wind_speed_10m"][idx].Value<float>();
                    float vpd          = hourly["vapour_pressure_deficit"][idx].Value<float>();
                    float evap         = hourly["et0_fao_evapotranspiration"][idx].Value<float>();
                    float appTemp      = hourly["apparent_temperature"][idx].Value<float>();
                    float windKnots    = windSpeed * 1.94384f;

                    string line = string.Join(",",
                        pointType,
                        lon.ToString(CultureInfo.InvariantCulture),
                        lat.ToString(CultureInfo.InvariantCulture),
                        temperature.ToString(CultureInfo.InvariantCulture),
                        dewPoint.ToString(CultureInfo.InvariantCulture),
                        relHum.ToString(CultureInfo.InvariantCulture),
                        windSpeed.ToString(CultureInfo.InvariantCulture),
                        vpd.ToString(CultureInfo.InvariantCulture),
                        evap.ToString(CultureInfo.InvariantCulture),
                        windKnots.ToString(CultureInfo.InvariantCulture),
                        appTemp.ToString(CultureInfo.InvariantCulture)
                    );
                    lines.Add(line);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error parsing JSON untuk titik ({lat},{lon}): {ex.Message}");
                    continue;
                }
            }

            yield return new WaitForSeconds(0.1f);
        }

        // 5. Tulis CSV ke folder Assets/FetchedData
        string basePath = Application.dataPath;
        string outDir = Path.Combine(basePath, fetchedDataFolder);
        Directory.CreateDirectory(outDir);

        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string fileName = $"meteo_{timestamp}.csv";
        string fullPath = Path.Combine(outDir, fileName);

        try
        {
            File.WriteAllLines(fullPath, lines, Encoding.UTF8);
            Debug.Log($"[OpenMeteo] CSV saved to: {fullPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Gagal menulis file CSV: {ex.Message}");
        }

    #if UNITY_EDITOR
        AssetDatabase.Refresh();
    #endif

        // 6. Stop fetch timer dan update UI
        if (fetchTimerCoroutine != null)
        {
            StopCoroutine(fetchTimerCoroutine);
            fetchTimerCoroutine = null;
        }

        TimeSpan elapsed = DateTime.UtcNow - fetchStartTime;
        if (statusText != null)
            statusText.text = $"Fetched in {elapsed.Minutes}m {elapsed.Seconds}s";

        if (fetchButton != null)
            fetchButton.interactable = true;

        // 7. Trigger fuzzy calculation if available
        if (fuzzyCalculator != null)
        {
            fuzzyCalculator.ProcessCsv(fullPath);
        }
    }
}
