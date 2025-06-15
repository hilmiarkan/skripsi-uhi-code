using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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
        // Gunakan persistentDataPath untuk kompatibilitas dengan Meta Quest 2
        string basePath = Application.persistentDataPath;
        Debug.Log($"[DataManager] Base path: {basePath}");
        
        Directory.CreateDirectory(Path.Combine(basePath, rawDataFolder));
        Directory.CreateDirectory(Path.Combine(basePath, fuzzyDataFolder));
        Directory.CreateDirectory(Path.Combine(basePath, heatmapFolder));
        Directory.CreateDirectory(Path.Combine(basePath, prefetchedDataFolder));
        
        Debug.Log($"[DataManager] Folders initialized in: {basePath}");
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
        Debug.Log("[DataManager] Starting search for prefetched data...");
        
        // Prioritas 1: Gunakan hasil fetch terbaru jika ada
        DataPackage latestPackage = GetLatestCompletePackage();
        
        if (latestPackage != null && !string.IsNullOrEmpty(latestPackage.rawCsvPath) && File.Exists(latestPackage.rawCsvPath))
        {
            Debug.Log($"[DataManager] Using latest fetch result: {latestPackage.rawCsvPath}");
            return latestPackage.rawCsvPath;
        }
        else
        {
            Debug.Log("[DataManager] No latest complete package found or file doesn't exist");
        }
        
        // Gunakan persistentDataPath untuk kompatibilitas dengan Meta Quest 2
        string basePath = Application.persistentDataPath;
        
        // Prioritas 2: Cari file CSV apapun di folder FetchedData
        string fetchedDataPath = Path.Combine(basePath, rawDataFolder);
        Debug.Log($"[DataManager] Checking FetchedData folder: {fetchedDataPath}");
        
        if (Directory.Exists(fetchedDataPath))
        {
            string[] csvFiles = Directory.GetFiles(fetchedDataPath, "*.csv");
            Debug.Log($"[DataManager] Found {csvFiles.Length} CSV files in FetchedData");
            
            if (csvFiles.Length > 0)
            {
                // Ambil file yang paling baru berdasarkan waktu modifikasi
                string newestFile = csvFiles.OrderByDescending(f => File.GetLastWriteTime(f)).First();
                Debug.Log($"[DataManager] Using newest file from FetchedData: {newestFile}");
                return newestFile;
            }
        }
        else
        {
            Debug.Log($"[DataManager] FetchedData directory does not exist: {fetchedDataPath}");
        }
        
        // Prioritas 3: Cari file CSV apapun di folder FuzzyResults (sebagai backup)
        string fuzzyDataPath = Path.Combine(basePath, fuzzyDataFolder);
        Debug.Log($"[DataManager] Checking FuzzyResults folder: {fuzzyDataPath}");
        
        if (Directory.Exists(fuzzyDataPath))
        {
            string[] csvFiles = Directory.GetFiles(fuzzyDataPath, "*.csv");
            Debug.Log($"[DataManager] Found {csvFiles.Length} CSV files in FuzzyResults");
            
            if (csvFiles.Length > 0)
            {
                string newestFile = csvFiles.OrderByDescending(f => File.GetLastWriteTime(f)).First();
                Debug.Log($"[DataManager] Using newest file from FuzzyResults as backup: {newestFile}");
                return newestFile;
            }
        }
        else
        {
            Debug.Log($"[DataManager] FuzzyResults directory does not exist: {fuzzyDataPath}");
        }
        
        // Prioritas 4: Fallback ke folder prefetched jika ada
        string prefetchedPath = Path.Combine(basePath, prefetchedDataFolder);
        Debug.Log($"[DataManager] Checking PrefetchedData folder: {prefetchedPath}");
        
        if (Directory.Exists(prefetchedPath))
        {
            string[] csvFiles = Directory.GetFiles(prefetchedPath, "*.csv");
            Debug.Log($"[DataManager] Found {csvFiles.Length} CSV files in PrefetchedData");
            
            if (csvFiles.Length > 0)
            {
                Debug.Log($"[DataManager] Using fallback prefetched data: {csvFiles[0]}");
                return csvFiles[0];
            }
        }
        else
        {
            Debug.Log($"[DataManager] PrefetchedData directory does not exist: {prefetchedPath}");
        }
        
        Debug.LogWarning("[DataManager] No CSV data available anywhere");
        
        // Prioritas 5: Buat data dummy sebagai fallback terakhir
        Debug.Log("[DataManager] Creating dummy data as last resort...");
        return CreateDummyData();
    }

    private string CreateDummyData()
    {
        try
        {
            // Gunakan persistentDataPath untuk kompatibilitas dengan Meta Quest 2
            string dummyDataPath = Path.Combine(Application.persistentDataPath, rawDataFolder);
            Directory.CreateDirectory(dummyDataPath);
            
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string fileName = $"dummy_{timestamp}.csv";
            string filePath = Path.Combine(dummyDataPath, fileName);
            
            // Buat data dummy dengan beberapa titik di Malang
            var dummyLines = new List<string>
            {
                "type,longitude,latitude,temperature_2m,dew_point_2m,relative_humidity_2m,wind_speed_10m,vapour_pressure_deficit,evapotranspiration,wind_knots,apparent_temperature",
                "urban,112.6304,-7.9797,26.5,20.1,75.2,8.3,0.65,0.21,16.1,28.2",
                "urban,112.6144,-7.9666,27.1,20.8,73.5,7.9,0.72,0.22,15.3,29.1",
                "urban,112.6234,-7.9856,26.8,20.5,74.1,8.1,0.68,0.21,15.7,28.7",
                "rural,112.5823,-7.9234,24.2,19.2,82.1,9.2,0.45,0.18,17.9,25.8",
                "rural,112.6789,-8.0123,23.8,18.9,83.5,9.8,0.42,0.17,19.0,25.3",
                "rural,112.5456,-7.8967,24.5,19.5,81.2,8.7,0.48,0.19,16.9,26.1"
            };
            
            File.WriteAllLines(filePath, dummyLines);
            Debug.Log($"[DataManager] Created dummy data file: {filePath}");
            
            return filePath;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DataManager] Failed to create dummy data: {ex.Message}");
            return null;
        }
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