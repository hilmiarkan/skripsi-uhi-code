using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class OpenMeteoFetcher : MonoBehaviour
{
    [Header("CSV TextAssets")]
    public TextAsset urbanCsv;
    public TextAsset ruralCsv;

    [Header("Fetch Settings")]
    [Tooltip("If checked, always use previous fetched data (skip fetching new data)")]
    public bool usePreviousData = false;

    [Header("Events")]
    public Action<string> OnFetchStarted;
    public Action<string> OnFetchCompleted;
    public Action<string> OnFetchFailed;
    public Action<float>  OnProgressUpdate; // 0–1

    private readonly TimeZoneInfo jakartaTz = TimeZoneInfo.FindSystemTimeZoneById(
    #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        "SE Asia Standard Time"
    #else
        "Asia/Jakarta"
    #endif
    );

    private const int   BATCH_SIZE  = 10;
    private const float BATCH_DELAY = 1f;

    public void StartFetching()
    {
        if (usePreviousData)
        {
            Debug.Log("[OpenMeteoFetcher] usePreviousData is enabled. Using previous data.");
            FetchWithPrefetchedData();
            return;
        }
        
        // Selalu gunakan prefetched data jika tersedia, tanpa pengecekan waktu
        string prefetchedPath = DataManager.Instance.GetPrefetchedDataPath();
        if (!string.IsNullOrEmpty(prefetchedPath))
        {
            Debug.Log($"[OpenMeteoFetcher] Using existing data for immediate startup: {prefetchedPath}");
            FetchWithPrefetchedData();
            return;
        }
        
        // Hanya fetch data baru jika benar-benar tidak ada data sama sekali
        Debug.Log("[OpenMeteoFetcher] No existing data found, fetching new data...");
        StartCoroutine(FetchDataCoroutine());
    }

    public void FetchWithPrefetchedData()
    {
        string prefetchedPath = DataManager.Instance.GetPrefetchedDataPath();
        if (!string.IsNullOrEmpty(prefetchedPath))
        {
            var pkg     = DataManager.Instance.CreateNewDataPackage();
            var newPath = CopyPrefetchedData(prefetchedPath, pkg.timestamp);
            if (!string.IsNullOrEmpty(newPath))
            {
                DataManager.Instance.SetRawDataPath(pkg, newPath);
                OnFetchCompleted?.Invoke(newPath);
                return;
            }
        }
        OnFetchFailed?.Invoke("No previous data available.");
    }

    private string CopyPrefetchedData(string src, string timestamp)
    {
        try
        {
            // Gunakan persistentDataPath untuk kompatibilitas dengan Meta Quest 2
            string dstDir  = Path.Combine(Application.persistentDataPath, DataManager.Instance.rawDataFolder);
            string dstPath = Path.Combine(dstDir, $"meteo_{timestamp}.csv");
            File.Copy(src, dstPath, true);
    #if UNITY_EDITOR
            AssetDatabase.Refresh();
    #endif
            Debug.Log($"[OpenMeteoFetcher] Copied data to: {dstPath}");
            return dstPath;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[OpenMeteoFetcher] Copy failed: {ex.Message}");
            return null;
        }
    }

    private IEnumerator FetchDataCoroutine()
    {
        var pkg = DataManager.Instance.CreateNewDataPackage();
        OnFetchStarted?.Invoke("Starting fetch…");

        // hitung target jam berdasarkan waktu Jakarta
        DateTime nowUtc = DateTime.UtcNow;
        DateTime nowJkt = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, jakartaTz);
        var    targetJkt = new DateTime(nowJkt.Year, nowJkt.Month, nowJkt.Day, nowJkt.Hour, 0, 0, DateTimeKind.Unspecified);
        string targetHour = TimeZoneInfo.ConvertTimeToUtc(targetJkt, jakartaTz)
                                 .ToString("yyyy-MM-dd'T'HH:00", CultureInfo.InvariantCulture);

        Debug.Log($"[OpenMeteoFetcher] Fetching data for: {targetHour}");

        // buat header CSV
        var lines = new List<string> {
            "type,longitude,latitude,temperature_2m,dew_point_2m,relative_humidity_2m,wind_speed_10m,vapour_pressure_deficit,evapotranspiration,wind_knots,apparent_temperature"
        };

        // kumpulkan titik koordinat (tanpa kehilangan presisi)
        var points = new List<(string type, string lon, string lat)>();
        CollectCSV(urbanCsv, "urban", points);
        CollectCSV(ruralCsv, "rural", points);

        if (points.Count == 0)
        {
            OnFetchFailed?.Invoke("No coordinate points found in CSV files");
            yield break;
        }

        int totalBatches    = Mathf.CeilToInt(points.Count / (float)BATCH_SIZE);
        int processedPoints = 0;

        for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
        {
            int start = batchIndex * BATCH_SIZE;
            int count = Mathf.Min(BATCH_SIZE, points.Count - start);
            var batch = points.GetRange(start, count);

            List<string> batchLines = null;
            yield return StartCoroutine(FetchBatch(batch, targetHour, result => batchLines = result));

            if (batchLines != null && batchLines.Count > 0)
            {
                lines.AddRange(batchLines);
                processedPoints += batchLines.Count;
            }

            // update dan tampilkan progress
            float progressFrac = processedPoints / (float)points.Count;
            OnProgressUpdate?.Invoke(progressFrac);
            Debug.Log($"[OpenMeteoFetcher] Progress: {processedPoints}/{points.Count} " +
                      $"(batch {batchIndex+1}/{totalBatches})");

            // delay antar batch jika bukan batch terakhir
            if (batchIndex < totalBatches - 1)
                yield return new WaitForSeconds(BATCH_DELAY);
        }

        Debug.Log($"[OpenMeteoFetcher] Finished processing {processedPoints}/{points.Count} points");

        if (processedPoints == 0)
        {
            OnFetchFailed?.Invoke("No data fetched.");
            yield break;
        }

        string csvPath = SaveCSV(lines, pkg.timestamp);
        if (!string.IsNullOrEmpty(csvPath))
        {
            DataManager.Instance.SetRawDataPath(pkg, csvPath);
            OnFetchCompleted?.Invoke(csvPath);
        }
        else
        {
            OnFetchFailed?.Invoke("Failed to save CSV file");
        }
    }

 private IEnumerator FetchBatch(
    List<(string type, string lon, string lat)> batch,
    string targetHour,
    Action<List<string>> onComplete)
{
    if (batch.Count == 0)
    {
        onComplete(new List<string>());
        yield break;
    }

    string url =
        "https://api.open-meteo.com/v1/forecast?" +
        $"latitude={ string.Join(",", batch.Select(p => p.lat)) }&" +
        $"longitude={ string.Join(",", batch.Select(p => p.lon)) }&" +
        "hourly=temperature_2m,relative_humidity_2m,apparent_temperature,dew_point_2m,et0_fao_evapotranspiration,vapour_pressure_deficit,wind_speed_10m&" +
        $"start_hour={targetHour}&end_hour={targetHour}";

    using var uwr = UnityWebRequest.Get(url);
    yield return uwr.SendWebRequest();

    if (uwr.result != UnityWebRequest.Result.Success)
    {
        Debug.LogError($"[OpenMeteoFetcher] HTTP Error: {uwr.error}");
        onComplete(new List<string>());
        yield break;
    }

    try
    {
        // parse sebagai array JSON
        var rootArray = JArray.Parse(uwr.downloadHandler.text);
        var results   = new List<string>();

        for (int i = 0; i < batch.Count && i < rootArray.Count; i++)
        {
            var resp   = (JObject)rootArray[i];
            var hourly = (JObject)resp["hourly"];

            // Ambil nilai sebagai string langsung dari JSON untuk mempertahankan presisi
            var tempToken = ((JArray)hourly["temperature_2m"])[0];
            var dewToken = ((JArray)hourly["dew_point_2m"])[0];
            var humToken = ((JArray)hourly["relative_humidity_2m"])[0];
            var evapToken = ((JArray)hourly["et0_fao_evapotranspiration"])[0];
            var vpdToken = ((JArray)hourly["vapour_pressure_deficit"])[0];
            var windToken = ((JArray)hourly["wind_speed_10m"])[0];
            var appToken = ((JArray)hourly["apparent_temperature"])[0];

            string tempStr = tempToken.ToString();
            string dewStr = dewToken.ToString();
            string humStr = humToken.ToString();
            string evapStr = evapToken.ToString();
            string vpdStr = vpdToken.ToString();
            string windStr = windToken.ToString();
            string appStr = appToken.ToString();
            
            // Konversi wind speed ke knots dengan presisi tinggi
            string knotsStr = "0";
            if (decimal.TryParse(windStr, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal windDecimal))
            {
                decimal knotsDecimal = windDecimal * 1.94384m;
                knotsStr = knotsDecimal.ToString(CultureInfo.InvariantCulture);
            }
            
            // bangun baris CSV
            string line = string.Join(",",
                batch[i].type,
                batch[i].lon,
                batch[i].lat,
                tempStr,
                dewStr,
                humStr,
                windStr,
                vpdStr,
                evapStr,
                knotsStr,
                appStr
            );
            results.Add(line);
        }

        onComplete(results);
    }
    catch (Exception ex)
    {
        Debug.LogError($"[OpenMeteoFetcher] JSON parse error: {ex.Message}");
        onComplete(new List<string>());
    }
}

    private void CollectCSV(TextAsset csv, string type, List<(string,string,string)> pts)
    {
        if (csv == null) return;
        var lines = csv.text
                      .Split(new[] { '\r','\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split(',');
            if (cols.Length < 2) continue;
            string lon = cols[0].Trim(), lat = cols[1].Trim();
            if (double.TryParse(lon, NumberStyles.Any, CultureInfo.InvariantCulture, out _) &&
                double.TryParse(lat, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                pts.Add((type, lon, lat));
            }
        }
    }

    private string SaveCSV(List<string> lines, string timestamp)
    {
        try
        {
            // Gunakan persistentDataPath untuk kompatibilitas dengan Meta Quest 2
            string dir  = Path.Combine(Application.persistentDataPath, DataManager.Instance.rawDataFolder);
            string file = $"meteo_{timestamp}.csv";
            string path = Path.Combine(dir, file);
            File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);
    #if UNITY_EDITOR
            AssetDatabase.Refresh();
    #endif
            Debug.Log($"[OpenMeteoFetcher] Saved CSV to {path}");
            return path;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[OpenMeteoFetcher] Save failed: {ex.Message}");
            return null;
        }
    }
}
