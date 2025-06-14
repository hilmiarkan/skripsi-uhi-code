using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class OpenMeteoFetcher : MonoBehaviour
{
    [Header("Events")]
    public Action<string> OnFetchStarted;
    public Action<string> OnFetchCompleted;
    public Action<string> OnFetchFailed;

    public void StartFetching()
    {
        Debug.Log("[OpenMeteoFetcher] Using prefetched data only");
        FetchWithPrefetchedData();
    }

    public void FetchWithPrefetchedData()
    {
        OnFetchStarted?.Invoke("Using prefetched data...");
        
        string prefetchedPath = DataManager.Instance.GetPrefetchedDataPath();
        if (!string.IsNullOrEmpty(prefetchedPath))
        {
            var pkg = DataManager.Instance.CreateNewDataPackage();
            var newPath = CopyPrefetchedData(prefetchedPath, pkg.timestamp);
            if (!string.IsNullOrEmpty(newPath))
            {
                DataManager.Instance.SetRawDataPath(pkg, newPath);
                OnFetchCompleted?.Invoke(newPath);
                Debug.Log($"[OpenMeteoFetcher] Prefetched data copied to: {newPath}");
                return;
            }
        }
        OnFetchFailed?.Invoke("No prefetched data available.");
    }

    private string CopyPrefetchedData(string src, string timestamp)
    {
        try
        {
            string dstDir = Path.Combine(Application.dataPath, DataManager.Instance.rawDataFolder);
            string dstPath = Path.Combine(dstDir, $"meteo_{timestamp}.csv");
            File.Copy(src, dstPath, true);
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
            return dstPath;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[OpenMeteoFetcher] Copy failed: {ex.Message}");
            return null;
        }
    }
}
