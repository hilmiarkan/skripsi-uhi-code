using System;
using System.IO;
using System.Text;
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

    [Header("Events")]
    public System.Action<string> OnFetchStarted;
    public System.Action<string> OnFetchCompleted;
    public System.Action<string> OnFetchFailed;
    public System.Action<float> OnProgressUpdate; // Progress 0-1

    private readonly TimeZoneInfo jakartaTz = TimeZoneInfo.FindSystemTimeZoneById(
    #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            "SE Asia Standard Time"
    #else
            "Asia/Jakarta"
    #endif
    );

    // Batch processing settings
    private const int BATCH_SIZE = 10;
    private const float BATCH_DELAY = 1f;

    public void StartFetching()
    {
        StartCoroutine(FetchDataCoroutine());
    }

    public void FetchWithPrefetchedData()
    {
        string prefetchedPath = DataManager.Instance.GetPrefetchedDataPath();
        if (!string.IsNullOrEmpty(prefetchedPath))
        {
            // Copy prefetched data to current package
            DataPackage package = DataManager.Instance.CreateNewDataPackage();
            string newPath = CopyPrefetchedData(prefetchedPath, package.timestamp);
            
            if (!string.IsNullOrEmpty(newPath))
            {
                DataManager.Instance.SetRawDataPath(package, newPath);
                OnFetchCompleted?.Invoke(newPath);
                return;
            }
        }
        
        OnFetchFailed?.Invoke("No previous data available. Please try fetching again or check your internet connection.");
    }

    private string CopyPrefetchedData(string sourcePath, string timestamp)
    {
        try
        {
            string destDir = Path.Combine(Application.dataPath, DataManager.Instance.rawDataFolder);
            string destPath = Path.Combine(destDir, $"meteo_{timestamp}.csv");
            File.Copy(sourcePath, destPath, true);
            
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
            return destPath;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[OpenMeteoFetcher] Failed to copy prefetched data: {ex.Message}");
            return null;
        }
    }

    private IEnumerator FetchDataCoroutine()
    {
        DataPackage package = DataManager.Instance.CreateNewDataPackage();
        OnFetchStarted?.Invoke("Starting data fetch...");

        // Calculate target time (current hour)
        DateTime nowUtc = DateTime.UtcNow;
        DateTime nowJakarta = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, jakartaTz);
        DateTime targetJakarta = new DateTime(
            nowJakarta.Year, nowJakarta.Month, nowJakarta.Day,
            nowJakarta.Hour, 0, 0, DateTimeKind.Unspecified);
        DateTime targetUtc = TimeZoneInfo.ConvertTimeToUtc(targetJakarta, jakartaTz);
        string targetTimeString = targetUtc.ToString("yyyy-MM-dd'T'HH:00", CultureInfo.InvariantCulture);

        Debug.Log($"[OpenMeteoFetcher] Fetching data for: {targetTimeString}");

        // Prepare CSV lines
        List<string> lines = new List<string>();
        lines.Add("type,longitude,latitude,temperature_2m,dew_point_2m,relative_humidity_2m,wind_speed_10m,vapour_pressure_deficit,evapotranspiration,wind_knots,apparent_temperature");

        // Collect points from CSV files
        var points = new List<(string type, float lon, float lat)>();
        CollectPointsFromCSV(urbanCsv, "urban", points);
        CollectPointsFromCSV(ruralCsv, "rural", points);

        if (points.Count == 0)
        {
            OnFetchFailed?.Invoke("No coordinate points found in CSV files");
            yield break;
        }

        Debug.Log($"[OpenMeteoFetcher] Total coordinates: {points.Count}");

        // Process points in batches
        int totalBatches = Mathf.CeilToInt((float)points.Count / BATCH_SIZE);
        int processedPoints = 0;

        for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
        {
            int startIndex = batchIndex * BATCH_SIZE;
            int endIndex = Mathf.Min(startIndex + BATCH_SIZE, points.Count);
            var batch = points.GetRange(startIndex, endIndex - startIndex);

            Debug.Log($"[OpenMeteoFetcher] Processing batch {batchIndex + 1}/{totalBatches}...");

            // Fetch batch data
            List<string> batchResults = null;
            yield return StartCoroutine(FetchBatch(batch, targetTimeString, (results) => batchResults = results));

            if (batchResults != null)
            {
                lines.AddRange(batchResults);
                processedPoints += batchResults.Count;
            }

            // Update progress
            OnProgressUpdate?.Invoke((float)(batchIndex + 1) / totalBatches);

            // Add delay between batches (except for the last one)
            if (batchIndex < totalBatches - 1)
            {
                yield return new WaitForSeconds(BATCH_DELAY);
            }
        }

        Debug.Log($"[OpenMeteoFetcher] Successfully processed {processedPoints}/{points.Count} points");

        // Validate fetch results
        if (processedPoints == 0)
        {
            OnFetchFailed?.Invoke("No data points were successfully fetched. Please check your internet connection and try again.");
            yield break;
        }
        
        // Check if we have significantly fewer points than expected (less than 80% of total)
        float successRate = (float)processedPoints / points.Count;
        if (successRate < 0.8f)
        {
            OnFetchFailed?.Invoke($"Only {processedPoints}/{points.Count} points fetched ({successRate:P1}). This may indicate API rate limiting or network issues. Try using previous data or try again later.");
            yield break;
        }

        // Check for daily API limit (Open-Meteo typically returns specific errors)
        if (processedPoints < points.Count / 2) // Less than 50% success might indicate rate limiting
        {
            OnFetchFailed?.Invoke($"Possible API daily limit reached. Only {processedPoints}/{points.Count} points fetched. Please try using previous data or try again tomorrow.");
            yield break;
        }

        // Save CSV file
        string csvPath = SaveCSVFile(lines, package.timestamp);
        if (!string.IsNullOrEmpty(csvPath))
        {
            DataManager.Instance.SetRawDataPath(package, csvPath);
            OnFetchCompleted?.Invoke(csvPath);
        }
        else
        {
            OnFetchFailed?.Invoke("Failed to save CSV file");
        }
    }

    private IEnumerator FetchBatch(List<(string type, float lon, float lat)> batch, string targetTimeString, System.Action<List<string>> onComplete)
    {
        if (batch.Count == 0)
        {
            onComplete?.Invoke(new List<string>());
            yield break;
        }

        // Prepare URL with multiple coordinates
        var latitudes = batch.Select(p => p.lat.ToString(CultureInfo.InvariantCulture)).ToArray();
        var longitudes = batch.Select(p => p.lon.ToString(CultureInfo.InvariantCulture)).ToArray();

        string url = "https://api.open-meteo.com/v1/forecast?" +
                     $"latitude={string.Join(",", latitudes)}&" +
                     $"longitude={string.Join(",", longitudes)}&" +
                     "hourly=temperature_2m,relative_humidity_2m,apparent_temperature,dew_point_2m,et0_fao_evapotranspiration,vapour_pressure_deficit,wind_speed_10m&" +
                     $"start_hour={targetTimeString}&" +
                     $"end_hour={targetTimeString}";

        using (UnityWebRequest uwr = UnityWebRequest.Get(url))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[OpenMeteoFetcher] Batch request failed: {uwr.error}");
                onComplete?.Invoke(new List<string>());
                yield break;
            }

            try
            {
                JArray jsonArray = JArray.Parse(uwr.downloadHandler.text);
                List<string> results = new List<string>();

                for (int i = 0; i < jsonArray.Count && i < batch.Count; i++)
                {
                    JObject response = jsonArray[i] as JObject;
                    if (response == null) continue;

                    JObject hourly = response["hourly"] as JObject;
                    if (hourly == null) continue;

                    // Since we're using start_hour and end_hour, we should only get one data point
                    JArray temperatureArray = hourly["temperature_2m"] as JArray;
                    if (temperatureArray == null || temperatureArray.Count == 0) continue;

                    var point = batch[i];
                    
                    float temperature = temperatureArray[0].Value<float>();
                    float relHum = (hourly["relative_humidity_2m"] as JArray)?[0]?.Value<float>() ?? 0f;
                    float appTemp = (hourly["apparent_temperature"] as JArray)?[0]?.Value<float>() ?? 0f;
                    float dewPoint = (hourly["dew_point_2m"] as JArray)?[0]?.Value<float>() ?? 0f;
                    float evap = (hourly["et0_fao_evapotranspiration"] as JArray)?[0]?.Value<float>() ?? 0f;
                    float vpd = (hourly["vapour_pressure_deficit"] as JArray)?[0]?.Value<float>() ?? 0f;
                    float windSpeed = (hourly["wind_speed_10m"] as JArray)?[0]?.Value<float>() ?? 0f;
                    float windKnots = windSpeed * 1.94384f;

                    string csvLine = string.Join(",",
                        point.type,
                        point.lon.ToString(CultureInfo.InvariantCulture),
                        point.lat.ToString(CultureInfo.InvariantCulture),
                        temperature.ToString(CultureInfo.InvariantCulture),
                        dewPoint.ToString(CultureInfo.InvariantCulture),
                        relHum.ToString(CultureInfo.InvariantCulture),
                        windSpeed.ToString(CultureInfo.InvariantCulture),
                        vpd.ToString(CultureInfo.InvariantCulture),
                        evap.ToString(CultureInfo.InvariantCulture),
                        windKnots.ToString(CultureInfo.InvariantCulture),
                        appTemp.ToString(CultureInfo.InvariantCulture)
                    );

                    results.Add(csvLine);
                }

                onComplete?.Invoke(results);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OpenMeteoFetcher] JSON parsing error: {ex.Message}");
                onComplete?.Invoke(new List<string>());
            }
        }
    }

    private void CollectPointsFromCSV(TextAsset csvAsset, string type, List<(string, float, float)> points)
    {
        if (csvAsset == null) return;

        var lines = csvAsset.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split(',');
            if (cols.Length < 2) continue;
            
            if (float.TryParse(cols[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float lon) &&
                float.TryParse(cols[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float lat))
            {
                points.Add((type, lon, lat));
            }
        }
    }

    private string SaveCSVFile(List<string> lines, string timestamp)
    {
        try
        {
            string outDir = Path.Combine(Application.dataPath, DataManager.Instance.rawDataFolder);
            string fileName = $"meteo_{timestamp}.csv";
            string fullPath = Path.Combine(outDir, fileName);

            File.WriteAllLines(fullPath, lines, Encoding.UTF8);
            Debug.Log($"[OpenMeteoFetcher] CSV saved to: {fullPath}");

#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
            return fullPath;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[OpenMeteoFetcher] Failed to save CSV: {ex.Message}");
            return null;
        }
    }
} 