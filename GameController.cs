using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameController : MonoBehaviour
{
    [Header("Core Components")]
    public FuzzyCalculator fuzzyCalculator;
    public MarkerUpdater markerUpdater;
    public HotspotManager hotspotManager;
    public UIManager uiManager;
    public HeatmapGenerator heatmapGenerator;

    public static GameController Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void StartWithPrefetchedData()
    {
        Debug.Log("[GameController] Starting with prefetched data");
        
        // Get prefetched data path
        string prefetchedPath = DataManager.Instance.GetPrefetchedDataPath();
        if (string.IsNullOrEmpty(prefetchedPath))
        {
            Debug.LogError("[GameController] No prefetched data available!");
            if (uiManager != null)
                uiManager.UpdateProgressText("No prefetched data available!");
            return;
        }

        Debug.Log($"[GameController] Using prefetched data: {prefetchedPath}");
        if (uiManager != null)
            uiManager.UpdateProgressText("Using prefetched data...");

        // Update raw markers first
        if (markerUpdater != null)
        {
            markerUpdater.UpdateRawMarkers(prefetchedPath);
            Debug.Log("[GameController] Raw markers updated");
        }

        // Start fuzzy calculation
        if (fuzzyCalculator != null)
        {
            StartCoroutine(StartFuzzyWithDelay(prefetchedPath));
        }
    }

    private IEnumerator StartFuzzyWithDelay(string csvPath)
    {
        yield return new WaitForSeconds(1f); // Small delay to ensure markers are updated
        
        if (uiManager != null)
            uiManager.UpdateProgressText("Starting fuzzy calculation...");
            
        Debug.Log("[GameController] Starting fuzzy calculation");
        
        // Subscribe to fuzzy events
        fuzzyCalculator.OnFuzzyStarted += HandleFuzzyStarted;
        fuzzyCalculator.OnFuzzyCompleted += HandleFuzzyCompleted;
        fuzzyCalculator.OnFuzzyFailed += HandleFuzzyFailed;
        fuzzyCalculator.OnProgressUpdate += HandleFuzzyProgress;
        
        fuzzyCalculator.StartFuzzyCalculation(csvPath);
    }

    private void HandleFuzzyStarted(string message)
    {
        Debug.Log($"[GameController] Fuzzy started: {message}");
        if (uiManager != null)
            uiManager.UpdateProgressText("Fuzzy calculation started");
    }

    private void HandleFuzzyCompleted(string csvPath)
    {
        Debug.Log($"[GameController] Fuzzy completed: {csvPath}");
        if (uiManager != null)
            uiManager.UpdateProgressText("Fuzzy calculation completed!");

        // Update fuzzy markers
        if (markerUpdater != null)
        {
            markerUpdater.OnFuzzyMarkersUpdated += HandleFuzzyMarkersUpdated;
            markerUpdater.UpdateFuzzyMarkers(csvPath);
        }

        // Calculate simple UHI intensity
        CalculateSimpleUHI();
        
        // Clear progress text after delay
        StartCoroutine(ClearProgressAfterDelay());
    }

    private void HandleFuzzyFailed(string error)
    {
        Debug.LogError($"[GameController] Fuzzy failed: {error}");
        if (uiManager != null)
            uiManager.UpdateProgressText($"Fuzzy calculation failed: {error}");
    }

    private void HandleFuzzyProgress(int current, int total)
    {
        if (uiManager != null)
            uiManager.UpdateProgressText($"Fuzzy calculation: {current}/{total}");
    }

    private void HandleFuzzyMarkersUpdated(List<(Transform marker, float value)> markerData)
    {
        Debug.Log($"[GameController] Fuzzy markers updated: {markerData.Count} markers");
        
        // Create hotspots from fuzzy data
        if (hotspotManager != null)
        {
            hotspotManager.CreateTop5Hotspots(markerData, "fuzzy");
        }
    }

    private void CalculateSimpleUHI()
    {
        // Simple UHI calculation: highest urban temperature - lowest rural temperature
        float uhiIntensity = CalculateSimpleUHIFromMarkers();
        
        if (uiManager != null)
            uiManager.UpdateUHIIntensity(uhiIntensity);
            
        Debug.Log($"[GameController] Simple UHI Intensity: {uhiIntensity:F1}°C");
    }

    private float CalculateSimpleUHIFromMarkers()
    {
        float highestUrban = float.MinValue;
        float lowestRural = float.MaxValue;
        
        // Check raw markers for temperature values
        if (markerUpdater != null && markerUpdater.rawMarkerParent != null)
        {
            foreach (Transform child in markerUpdater.rawMarkerParent)
            {
                var markerInfo = child.GetComponent<MarkerInfo>();
                var textComponent = child.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                
                if (markerInfo != null && textComponent != null)
                {
                    string tempText = textComponent.text.Replace("°C", "").Trim();
                    if (float.TryParse(tempText, out float temp))
                    {
                        if (markerInfo.isRural)
                        {
                            if (temp < lowestRural)
                                lowestRural = temp;
                        }
                        else // urban
                        {
                            if (temp > highestUrban)
                                highestUrban = temp;
                        }
                    }
                }
            }
        }
        
        // Return difference if we found both values
        if (highestUrban != float.MinValue && lowestRural != float.MaxValue)
        {
            return highestUrban - lowestRural;
        }
        
        return 0f;
    }

    private IEnumerator ClearProgressAfterDelay()
    {
        yield return new WaitForSeconds(3f);
        if (uiManager != null)
            uiManager.UpdateProgressText("");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (fuzzyCalculator != null)
        {
            fuzzyCalculator.OnFuzzyStarted -= HandleFuzzyStarted;
            fuzzyCalculator.OnFuzzyCompleted -= HandleFuzzyCompleted;
            fuzzyCalculator.OnFuzzyFailed -= HandleFuzzyFailed;
            fuzzyCalculator.OnProgressUpdate -= HandleFuzzyProgress;
        }

        if (markerUpdater != null)
        {
            markerUpdater.OnFuzzyMarkersUpdated -= HandleFuzzyMarkersUpdated;
        }
    }
} 