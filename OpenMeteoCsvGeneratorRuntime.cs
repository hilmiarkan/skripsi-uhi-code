// OpenMeteoCsvGeneratorRuntime.cs
// Runtime MonoBehaviour to fetch Open-Meteo data and generate timestamped CSVs under persistentDataPath/FetchedData/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Debug = UnityEngine.Debug;

public class OpenMeteoCsvGeneratorRuntime : MonoBehaviour
{
    [Header("Csv TextAssets (place under Resources)")]
    public TextAsset urbanCsv;
    public TextAsset ruralCsv;

    [Header("UI Elements")]
    public Button fetchButton;
    public TextMeshProUGUI statusText;

    [Header("Generated CSVs Folder")]
    public string fetchedDataFolder = "FetchedData";

    [Header("Fuzzy Calculator (optional)")]
    public FuzzyRuntimeCalculator fuzzyCalculator;

    private Coroutine fetchTimerCoroutine;

    private IEnumerator FuzzyTimer()
    {
        int sec = 0;
        while (true)
        {
            if (fetchButton != null)
            {
                var btnText = fetchButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                    btnText.text = $"Fuzzyficating... {sec}s";
            }
            sec++;
            yield return new WaitForSeconds(1f);
        }
    }
    private Coroutine fuzzyTimerCoroutine;

    private DateTime fetchStartTime;

    void Awake()
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
                statusText.text = $"Fetching data.. {sec}s";
            sec++;
            yield return new WaitForSeconds(1f);
        }
    }

    private void OnFetchButtonClicked() {
        fetchStartTime = DateTime.UtcNow;
        fetchTimerCoroutine = StartCoroutine(FetchTimer());

        // Path to fetched files
        string dir = Path.Combine(Application.persistentDataPath, fetchedDataFolder);
        if (Directory.Exists(dir)) {
            var files = Directory.GetFiles(dir, "meteo_*.csv");
            if (files.Length > 0) {
                string latest = files.OrderByDescending(f => File.GetLastWriteTimeUtc(f)).First();
                DateTime lastTime = File.GetLastWriteTimeUtc(latest);
                if ((DateTime.UtcNow - lastTime).TotalHours < 3) {
                    // Direct fuzzy within 3h
                    if (fuzzyCalculator != null)
                    {
                        // disable button
                        if (fetchButton != null) fetchButton.interactable = false;
                        // start fuzzy timer
                        fuzzyTimerCoroutine = StartCoroutine(FuzzyTimer());
                        fuzzyCalculator.ProcessCsv(latest);
                    }
                    return;
                }
            }
        }
        StartCoroutine(FetchRoutine());
    }

    private IEnumerator FetchRoutine()
    {
        // Disable UI
        if (fetchButton != null) fetchButton.interactable = false;
        if (statusText != null) statusText.text = "Fetching data...";

        DateTime now = DateTime.UtcNow;
        DateTime target = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc)
                          .AddHours(-1);

        // Prepare CSV lines
        List<string> lines = new List<string>();
        lines.Add("type,longitude,latitude," + string.Join(",", variables));

        // Fetch for urban and rural
        yield return FetchPoints("urban", urbanCsv, target, lines);
        yield return FetchPoints("rural", ruralCsv, target, lines);

        // Determine output directory in persistentDataPath
        string basePath = Application.persistentDataPath;
        string outDir = Path.Combine(basePath, fetchedDataFolder);
        Directory.CreateDirectory(outDir);

        // Unique filename
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string fileName = $"meteo_{timestamp}.csv";
        string fullPath = Path.Combine(outDir, fileName);

        // Write CSV
        File.WriteAllLines(fullPath, lines);
        Debug.Log($"[OpenMeteo] CSV saved to: {fullPath}");

        #if UNITY_EDITOR
        AssetDatabase.Refresh();
        #endif

        // Re-enable UI
        if (statusText != null) statusText.text = "Fetch Complete";
        if (fetchButton != null) fetchButton.interactable = true;

        // Stop timer and show elapsed
        if (fetchTimerCoroutine != null)
            StopCoroutine(fetchTimerCoroutine);
        TimeSpan elapsed = DateTime.UtcNow - fetchStartTime;
        if (statusText != null)
            statusText.text = $"Fetching done in {elapsed.Minutes}m {elapsed.Seconds}s";

        // Begin fuzzy
        if (fetchButton != null)
        {
            var btnText = fetchButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = "Fuzzyficating..";
        }

        // Trigger fuzzy calculation if assigned
        if (fuzzyCalculator != null)
            fuzzyCalculator.ProcessCsv(fullPath);
    }

    private IEnumerator FetchPoints(string kind, TextAsset csv, DateTime target, List<string> lines)
    {
        if (csv == null)
        {
            Debug.LogWarning($"{kind} CSV not assigned.");
            yield break;
        }

        string[] rows = csv.text.Split(new[] {'\n', '\r'}, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < rows.Length; i++)
        {
            string[] cols = rows[i].Split(',');
            if (cols.Length < 2) continue;
            if (!float.TryParse(cols[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float lon)) continue;
            if (!float.TryParse(cols[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float lat)) continue;

            string hourlyParam = string.Join("%2C", variables);
            string url = $"{apiUrl}?latitude={lat.ToString(CultureInfo.InvariantCulture)}&longitude={lon.ToString(CultureInfo.InvariantCulture)}&hourly={hourlyParam}&past_days=1&timezone=UTC";

            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Error fetching {kind} @({lat},{lon}): {req.error}");
                    continue;
                }

                OpenMeteoResponse resp = JsonUtility.FromJson<OpenMeteoResponse>(req.downloadHandler.text);
                if (resp == null || resp.hourly == null)
                {
                    Debug.LogWarning($"Invalid JSON for {kind} @({lat},{lon})");
                    continue;
                }

                int idx = Array.FindIndex(resp.hourly.time, t =>
                    DateTime.TryParse(t, null, DateTimeStyles.AdjustToUniversal, out DateTime dt) && dt == target
                );
                if (idx < 0)
                {
                    Debug.LogWarning($"No data for {kind} @({lat},{lon}) at {target:O}");
                    continue;
                }

                List<string> vals = new List<string> { kind, lon.ToString(CultureInfo.InvariantCulture), lat.ToString(CultureInfo.InvariantCulture) };
                foreach (string v in variables)
                {
                    var field = typeof(HourlyData).GetField(v);
                    if (field != null)
                    {
                        float[] values = (float[])field.GetValue(resp.hourly);
                        vals.Add(values[idx].ToString(CultureInfo.InvariantCulture));
                    }
                }
                lines.Add(string.Join(",", vals));
            }
        }
    }

    private readonly string apiUrl = "https://api.open-meteo.com/v1/forecast";
    private readonly string[] variables = new string[]
    {
        "temperature_2m", "dew_point_2m", "relative_humidity_2m",
        "wind_speed_10m", "vapour_pressure_deficit", "evapotranspiration"
    };
}

[Serializable]
public class HourlyData
{
    public string[] time;
    public float[] temperature_2m;
    public float[] dew_point_2m;
    public float[] relative_humidity_2m;
    public float[] wind_speed_10m;
    public float[] vapour_pressure_deficit;
    public float[] evapotranspiration;
}

[Serializable]
public class OpenMeteoResponse
{
    public HourlyData hourly;
}
