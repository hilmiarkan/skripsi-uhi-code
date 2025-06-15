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
    public bool autoStartGameProcesses = true;
    public bool isInGameMode = false;
    private bool isPrologRunning = false; // Flag untuk mengecek status prolog

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
        
        // Auto-start game processes if enabled (fuzzy + heatmap)
        if (autoStartGameProcesses)
        {
            StartCoroutine(DelayedGameProcessStart());
        }
        
        // Subscribe to MainMenuNavigation events untuk mendeteksi prolog
        MainMenuNavigation.OnGameReady += OnGameReadyAfterProlog;
    }

    private void OnGameReadyAfterProlog()
    {
        isPrologRunning = false;
        // Restart auto-start processes setelah prolog selesai jika diperlukan
        if (autoStartGameProcesses)
        {
            StartCoroutine(DelayedGameProcessStart());
        }
    }

    private IEnumerator DelayedAutoStart()
    {
        yield return new WaitForSeconds(1f); // Small delay to ensure all components are ready
        
        // Jangan start fetching jika prolog sedang berjalan
        if (!isPrologRunning)
        {
            StartDataFetching();
        }
    }

    private IEnumerator DelayedGameProcessStart()
    {
        // Tunggu lebih lama dan pastikan prolog tidak sedang berjalan
        yield return new WaitForSeconds(3f);
        
        Debug.Log("[GameController] DelayedGameProcessStart started");
        Debug.Log($"[GameController] Current platform: {Application.platform}");
        
        // Jangan mulai proses berat jika prolog sedang aktif
        if (isPrologRunning || !autoStartGameProcesses)
        {
            Debug.Log($"[GameController] Skipping auto-start game processes (prolog running: {isPrologRunning}, auto-start enabled: {autoStartGameProcesses})");
            yield break;
        }
        
        // Try to use existing data to start game processes immediately
        string existingDataPath = DataManager.Instance.GetPrefetchedDataPath();
        Debug.Log($"[GameController] Checking for existing data: {existingDataPath}");
        
        if (!string.IsNullOrEmpty(existingDataPath))
        {
            Debug.Log("[GameController] Starting game processes with existing data...");
            
            // Update UI to show we're starting
            if (uiManager != null)
            {
                uiManager.UpdateProgressText("Loading existing data...");
            }
            
            // Create a data package for the existing data
            var package = DataManager.Instance.CreateNewDataPackage();
            DataManager.Instance.SetRawDataPath(package, existingDataPath);
            
            // Update raw markers immediately
            if (markerUpdater != null)
            {
                markerUpdater.UpdateRawMarkers(existingDataPath);
                Debug.Log("[GameController] Raw markers updated with existing data");
            }
            
            // Start fuzzy calculation with existing data
            if (fuzzyCalculator != null)
            {
                fuzzyCalculator.StartFuzzyCalculation(existingDataPath);
                Debug.Log("[GameController] Fuzzy calculation started with existing data");
            }
        }
        else
        {
            Debug.LogWarning("[GameController] No existing data found for auto-start game processes");
            
            // Update UI to show no data found
            if (uiManager != null)
            {
                // Untuk platform Android (Meta Quest 2), langsung gunakan dummy data
                if (Application.platform == RuntimePlatform.Android)
                {
                    uiManager.UpdateProgressText("Creating dummy data for VR platform...");
                    Debug.Log("[GameController] Android platform detected, forcing dummy data creation");
                    
                    // Force create dummy data
                    yield return new WaitForSeconds(0.5f);
                    string dummyPath = DataManager.Instance.GetPrefetchedDataPath(); // This will create dummy data
                    
                    if (!string.IsNullOrEmpty(dummyPath))
                    {
                        Debug.Log($"[GameController] Dummy data created: {dummyPath}");
                        
                        var package = DataManager.Instance.CreateNewDataPackage();
                        DataManager.Instance.SetRawDataPath(package, dummyPath);
                        
                        if (markerUpdater != null)
                        {
                            markerUpdater.UpdateRawMarkers(dummyPath);
                            Debug.Log("[GameController] Raw markers updated with dummy data");
                        }
                        
                        if (fuzzyCalculator != null)
                        {
                            fuzzyCalculator.StartFuzzyCalculation(dummyPath);
                            Debug.Log("[GameController] Fuzzy calculation started with dummy data");
                        }
                        yield break; // Exit early, don't try to fetch
                    }
                }
                else
                {
                    uiManager.UpdateProgressText("No existing data found. Please fetch new data.");
                }
            }
            
            // Try to start fetching new data as fallback (only for non-Android platforms)
            if (Application.platform != RuntimePlatform.Android)
            {
                yield return new WaitForSeconds(1f);
                if (fetcher != null)
                {
                    Debug.Log("[GameController] Starting data fetch as fallback");
                    fetcher.StartFetching();
                }
            }
        }
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

        // Start fuzzy calculation - ini akan berjalan baik untuk data baru maupun existing
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
        
        // Auto-generate heatmaps after fuzzy completion
        StartCoroutine(AutoGenerateHeatmaps());
    }

    private IEnumerator AutoGenerateHeatmaps()
    {
        yield return new WaitForSeconds(1f); // Small delay to ensure markers are updated
        
        if (heatmapGenerator != null)
        {
            Debug.Log("[GameController] Auto-generating fuzzy heatmap...");
            heatmapGenerator.ToggleFuzzyHeatmap(); // This will generate and show the heatmap
            
            yield return new WaitForSeconds(0.5f); // Small delay between heatmaps
            
            Debug.Log("[GameController] Auto-generating raw heatmap...");
            heatmapGenerator.ToggleRawHeatmap(); // This will generate and show the raw heatmap
        }
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
        isPrologRunning = false; // Prolog selesai saat masuk game mode
        
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
        MainMenuNavigation.OnGameReady -= OnGameReadyAfterProlog;
        
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