using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
    
[System.Serializable]
public class DataPackage
{
    public string timestamp;
    public string rawCsvPath;
    public string fuzzyCsvPath;
    public string heatmapTexturePath;
    public DateTime createdAt;
    public bool isComplete;
    
    public DataPackage(string timestamp)
    {
        this.timestamp = timestamp;
        this.createdAt = DateTime.UtcNow;
        this.isComplete = false;
    }
}

public class DataManager : MonoBehaviour
{
    [Header("Data Folders")]
    public string rawDataFolder = "FetchedData";
    public string fuzzyDataFolder = "FuzzyResults";
    public string heatmapFolder = "HeatmapTextures";
    public string prefetchedDataFolder = "PrefetchedData";

    private Dictionary<string, DataPackage> dataPackages = new Dictionary<string, DataPackage>();
    private DataPackage currentDataPackage;

    public static DataManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeFolders();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeFolders()
    {
        string basePath = Application.dataPath;
        Directory.CreateDirectory(Path.Combine(basePath, rawDataFolder));
        Directory.CreateDirectory(Path.Combine(basePath, fuzzyDataFolder));
        Directory.CreateDirectory(Path.Combine(basePath, heatmapFolder));
        Directory.CreateDirectory(Path.Combine(basePath, prefetchedDataFolder));
    }

    public DataPackage CreateNewDataPackage()
    {
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        currentDataPackage = new DataPackage(timestamp);
        dataPackages[timestamp] = currentDataPackage;
        return currentDataPackage;
    }

    public void SetRawDataPath(DataPackage package, string path)
    {
        package.rawCsvPath = path;
        CheckPackageCompletion(package);
    }

    public void SetFuzzyDataPath(DataPackage package, string path)
    {
        package.fuzzyCsvPath = path;
        CheckPackageCompletion(package);
    }

    public void SetHeatmapPath(DataPackage package, string path)
    {
        package.heatmapTexturePath = path;
        CheckPackageCompletion(package);
    }

    private void CheckPackageCompletion(DataPackage package)
    {
        if (!string.IsNullOrEmpty(package.rawCsvPath) && 
            !string.IsNullOrEmpty(package.fuzzyCsvPath))
        {
            package.isComplete = true;
            Debug.Log($"[DataManager] Package {package.timestamp} completed!");
        }
    }

    public DataPackage GetCurrentDataPackage()
    {
        return currentDataPackage;
    }

    public DataPackage GetLatestCompletePackage()
    {
        DataPackage latest = null;
        DateTime latestTime = DateTime.MinValue;

        foreach (var package in dataPackages.Values)
        {
            if (package.isComplete && package.createdAt > latestTime)
            {
                latest = package;
                latestTime = package.createdAt;
            }
        }

        return latest;
    }

    public string GetPrefetchedDataPath()
    {
        // Instead of using prefetched folder, use the latest completed package's raw CSV
        DataPackage latestPackage = GetLatestCompletePackage();
        
        if (latestPackage != null && !string.IsNullOrEmpty(latestPackage.rawCsvPath) && File.Exists(latestPackage.rawCsvPath))
        {
            Debug.Log($"[DataManager] Using latest fetch result as prefetched data: {latestPackage.rawCsvPath}");
            return latestPackage.rawCsvPath;
        }
        
        // Fallback to prefetched folder if no completed packages exist
        string prefetchedPath = Path.Combine(Application.dataPath, prefetchedDataFolder);
        if (Directory.Exists(prefetchedPath))
        {
            string[] csvFiles = Directory.GetFiles(prefetchedPath, "*.csv");
            if (csvFiles.Length > 0)
            {
                Debug.Log($"[DataManager] Using fallback prefetched data: {csvFiles[0]}");
                return csvFiles[0];
            }
        }
        
        Debug.LogWarning("[DataManager] No prefetched data available");
        return null;
    }

    public List<DataPackage> GetAllPackages()
    {
        return new List<DataPackage>(dataPackages.Values);
    }

    public string FormatTimestampForDisplay(string timestamp)
    {
        if (DateTime.TryParseExact(timestamp, "yyyyMMdd_HHmmss", null, 
            System.Globalization.DateTimeStyles.None, out DateTime dateTime))
        {
            return dateTime.ToString("dd MMM yyyy, HH:mm");
        }
        return timestamp;
    }
} 