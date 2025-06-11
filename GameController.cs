using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameController : MonoBehaviour
{
    [Header("Core Components")]
    public DataManager dataManager;
    public OpenMeteoFetcher fetcher;
    public FuzzyCalculator fuzzyCalculator;
    public MarkerUpdater markerUpdater;
    public HotspotManager hotspotManager;
    public UIManager uiManager;
    public HeatmapGenerator heatmapGenerator;

    [Header("Game State")]
    public bool autoStartFetching = true;
    public bool isInGameMode = false;

    private float currentUHIIntensity = 0f;
    private int mitigatedHotspots = 0;
    private const int totalHotspots = 5;

    public static GameController Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeGame();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeGame()
    {
        // Subscribe to component events
        if (fetcher != null)
        {
            fetcher.OnFetchStarted += HandleFetchStarted;
            fetcher.OnFetchCompleted += HandleFetchCompleted;
            fetcher.OnFetchFailed += HandleFetchFailed;
        }

        if (fuzzyCalculator != null)
        {
            fuzzyCalculator.OnFuzzyStarted += HandleFuzzyStarted;
            fuzzyCalculator.OnFuzzyCompleted += HandleFuzzyCompleted;
            fuzzyCalculator.OnFuzzyFailed += HandleFuzzyFailed;
            fuzzyCalculator.OnProgressUpdate += HandleFuzzyProgress;
        }

        if (markerUpdater != null)
        {
            markerUpdater.OnFuzzyMarkersUpdated += HandleFuzzyMarkersUpdated;
        }

        if (uiManager != null)
        {
            uiManager.OnTryAgainClicked += StartDataFetching;
            uiManager.OnUsePrefetchedClicked += UsePrefetchedData;
        }

        if (heatmapGenerator != null)
        {
            heatmapGenerator.OnFuzzyHeatmapGenerated += HandleFuzzyHeatmapGenerated;
            heatmapGenerator.OnRawHeatmapGenerated += HandleRawHeatmapGenerated;
            Debug.Log("[GameController] HeatmapGenerator events subscribed");
        }
        else
        {
            Debug.LogWarning("[GameController] HeatmapGenerator is not assigned!");
        }

        // Auto-start fetching if enabled
        if (autoStartFetching)
        {
            StartCoroutine(DelayedAutoStart());
        }
    }

    private IEnumerator DelayedAutoStart()
    {
        yield return new WaitForSeconds(1f); // Small delay to ensure all components are ready
        StartDataFetching();
    }

    public void StartDataFetching()
    {
        if (fetcher != null)
        {
            if (uiManager != null)
                uiManager.HideErrorPopup();
            
            fetcher.StartFetching();
        }
    }

    public void UsePrefetchedData()
    {
        if (fetcher != null)
        {
            if (uiManager != null)
                uiManager.HideErrorPopup();
            
            fetcher.FetchWithPrefetchedData();
        }
    }

    // Event Handlers
    private void HandleFetchStarted(string message)
    {
        Debug.Log($"[GameController] Fetch started: {message}");
        if (uiManager != null)
            uiManager.OnFetchStarted();
    }

    private void HandleFetchCompleted(string csvPath)
    {
        Debug.Log($"[GameController] Fetch completed: {csvPath}");
        if (uiManager != null)
            uiManager.OnFetchCompleted();

        // Update raw markers
        if (markerUpdater != null)
            markerUpdater.UpdateRawMarkers(csvPath);

        // Start fuzzy calculation
        if (fuzzyCalculator != null)
            fuzzyCalculator.StartFuzzyCalculation(csvPath);
    }

    private void HandleFetchFailed(string error)
    {
        Debug.LogError($"[GameController] Fetch failed: {error}");
        if (uiManager != null)
            uiManager.OnFetchFailed(error);
    }

    private void HandleFuzzyStarted(string message)
    {
        Debug.Log($"[GameController] Fuzzy started: {message}");
        if (uiManager != null)
            uiManager.OnFuzzyStarted();
    }

    private void HandleFuzzyCompleted(string csvPath)
    {
        Debug.Log($"[GameController] Fuzzy completed: {csvPath}");
        if (uiManager != null)
            uiManager.OnFuzzyCompleted();

        // Update fuzzy markers
        if (markerUpdater != null)
            markerUpdater.UpdateFuzzyMarkers(csvPath);

        // Calculate UHI intensity
        CalculateAndUpdateUHI(csvPath);
    }

    private void HandleFuzzyFailed(string error)
    {
        Debug.LogError($"[GameController] Fuzzy failed: {error}");
        if (uiManager != null)
            uiManager.OnFuzzyFailed(error);
    }

    private void HandleFuzzyProgress(int current, int total)
    {
        if (uiManager != null)
            uiManager.OnFuzzyProgress(current, total);
    }

    private void HandleFuzzyMarkersUpdated(List<(Transform marker, float value)> markerData)
    {
        // Create hotspots only from fuzzy data
        if (hotspotManager != null)
        {
            hotspotManager.CreateTop5Hotspots(markerData, "fuzzy");
        }

        // Update hotspot mitigation UI
        if (uiManager != null)
            uiManager.UpdateHotspotMitigation(mitigatedHotspots, totalHotspots);
    }

    private void CalculateAndUpdateUHI(string fuzzyCsvPath)
    {
        if (fuzzyCalculator != null)
        {
            currentUHIIntensity = fuzzyCalculator.CalculateUHIIntensity(fuzzyCsvPath, true);
            
            if (uiManager != null)
                uiManager.UpdateUHIIntensity(currentUHIIntensity);
                
            Debug.Log($"[GameController] UHI Intensity calculated: {currentUHIIntensity:F1}°C");
        }
    }

    // Heatmap event handlers
    private void HandleFuzzyHeatmapGenerated(List<Transform> topHotspots)
    {
        Debug.Log($"[GameController] Fuzzy heatmap generated with {topHotspots.Count} top hotspots");
        // Optional: Create hotspots from heatmap data if needed
        // Currently hotspots are created from MarkerUpdater events, so this is for future use
    }

    private void HandleRawHeatmapGenerated(List<Transform> topHotspots)
    {
        Debug.Log($"[GameController] Raw heatmap generated with {topHotspots.Count} top hotspots");
        // Optional: Create hotspots from heatmap data if needed
        // Currently hotspots are created from MarkerUpdater events, so this is for future use
    }

    // Public methods for game state management
    public void TransitionToGameMode()
    {
        isInGameMode = true;
        if (uiManager != null)
            uiManager.TransitionToGameMode();
        
        // Notify AudioManager about game mode
        if (AudioManager.Instance != null)
            AudioManager.Instance.OnGameModeEntered();
    }

    public void TransitionToMainMenu()
    {
        isInGameMode = false;
        if (uiManager != null)
            uiManager.TransitionToMainMenu();
            
        // Notify AudioManager about main menu
        if (AudioManager.Instance != null)
            AudioManager.Instance.OnMainMenuEntered();
    }

    // Mitigation methods
    public void OnHotspotMitigated()
    {
        mitigatedHotspots++;
        if (uiManager != null)
            uiManager.UpdateHotspotMitigation(mitigatedHotspots, totalHotspots);
    }

    public void ResetMitigationCount()
    {
        mitigatedHotspots = 0;
        if (uiManager != null)
            uiManager.UpdateHotspotMitigation(mitigatedHotspots, totalHotspots);
    }

    // Getters for other scripts
    public float GetCurrentUHIIntensity()
    {
        return currentUHIIntensity;
    }

    public int GetMitigatedHotspotsCount()
    {
        return mitigatedHotspots;
    }

    public DataPackage GetCurrentDataPackage()
    {
        return dataManager?.GetCurrentDataPackage();
    }

    public DataPackage GetLatestCompleteDataPackage()
    {
        return dataManager?.GetLatestCompletePackage();
    }

    // Method to force refresh all data
    public void RefreshAllData()
    {
        StartDataFetching();
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (fetcher != null)
        {
            fetcher.OnFetchStarted -= HandleFetchStarted;
            fetcher.OnFetchCompleted -= HandleFetchCompleted;
            fetcher.OnFetchFailed -= HandleFetchFailed;
        }

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

        if (uiManager != null)
        {
            uiManager.OnTryAgainClicked -= StartDataFetching;
            uiManager.OnUsePrefetchedClicked -= UsePrefetchedData;
        }

        if (heatmapGenerator != null)
        {
            heatmapGenerator.OnFuzzyHeatmapGenerated -= HandleFuzzyHeatmapGenerated;
            heatmapGenerator.OnRawHeatmapGenerated -= HandleRawHeatmapGenerated;
        }
    }
} 