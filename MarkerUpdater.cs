using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MarkerUpdater : MonoBehaviour
{
    [Header("Marker Parents")]
    public Transform rawMarkerParent;
    public Transform fuzzyMarkerParent;

    [Header("Events")]
    public System.Action<List<(Transform marker, float value)>> OnRawMarkersUpdated;
    public System.Action<List<(Transform marker, float value)>> OnFuzzyMarkersUpdated;

    public void UpdateRawMarkers(string csvPath)
    {
        if (rawMarkerParent == null)
        {
            Debug.LogWarning("[MarkerUpdater] rawMarkerParent not assigned");
            return;
        }

        var markerData = UpdateMarkersFromCSV(csvPath, rawMarkerParent, "temperature_2m");
        OnRawMarkersUpdated?.Invoke(markerData);
    }

    public void UpdateFuzzyMarkers(string csvPath)
    {
        if (fuzzyMarkerParent == null)
        {
            Debug.LogWarning("[MarkerUpdater] fuzzyMarkerParent not assigned");
            return;
        }

        var markerData = UpdateMarkersFromCSV(csvPath, fuzzyMarkerParent, "apparent_temperature");
        OnFuzzyMarkersUpdated?.Invoke(markerData);
    }

    private List<(Transform marker, float value)> UpdateMarkersFromCSV(string csvPath, Transform markerParent, string temperatureColumn)
    {
        var markerData = new List<(Transform marker, float value)>();

        if (!File.Exists(csvPath))
        {
            Debug.LogError($"[MarkerUpdater] CSV file not found: {csvPath}");
            return markerData;
        }

        try
        {
            string[] lines = File.ReadAllLines(csvPath);
            if (lines.Length < 2)
            {
                Debug.LogWarning($"[MarkerUpdater] CSV file is empty or has no data: {csvPath}");
                return markerData;
            }

            // Parse header
            var header = lines[0].Split(',');
            int lonIdx = Array.IndexOf(header, "longitude");
            int latIdx = Array.IndexOf(header, "latitude");
            int tempIdx = Array.IndexOf(header, temperatureColumn);

            if (lonIdx < 0 || latIdx < 0 || tempIdx < 0)
            {
                Debug.LogError($"[MarkerUpdater] Required columns not found. Looking for: longitude, latitude, {temperatureColumn}");
                return markerData;
            }

            // Update markers
            for (int i = 1; i < lines.Length; i++)
            {
                var cols = lines[i].Split(',');
                if (cols.Length <= Math.Max(lonIdx, Math.Max(latIdx, tempIdx)))
                    continue;

                try
                {
                    float lon = float.Parse(cols[lonIdx], CultureInfo.InvariantCulture);
                    float lat = float.Parse(cols[latIdx], CultureInfo.InvariantCulture);
                    float temp = float.Parse(cols[tempIdx], CultureInfo.InvariantCulture);

                    // Find matching marker
                    Transform marker = FindMarkerByCoordinates(markerParent, lon, lat);
                    if (marker != null)
                    {
                        UpdateMarkerText(marker, temp);
                        
                        // Only add urban markers to the data list
                        var markerInfo = marker.GetComponent<MarkerInfo>();
                        if (markerInfo != null && !markerInfo.isRural)
                        {
                            markerData.Add((marker, temp));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MarkerUpdater] Error processing line {i}: {ex.Message}");
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MarkerUpdater] Error reading CSV file: {ex.Message}");
        }

        return markerData;
    }

    private Transform FindMarkerByCoordinates(Transform parent, float lon, float lat)
    {
        foreach (Transform child in parent)
        {
            var markerInfo = child.GetComponent<MarkerInfo>();
            if (markerInfo != null)
            {
                // Check if coordinates match with some tolerance
                if (Mathf.Abs(markerInfo.longitude - lon) < 1e-4f &&
                    Mathf.Abs(markerInfo.latitude - lat) < 1e-4f)
                {
                    return child;
                }
            }
        }
        return null;
    }

    private void UpdateMarkerText(Transform marker, float temperature)
    {
        var textComponent = marker.GetComponentInChildren<TextMeshProUGUI>();
        if (textComponent != null)
        {
            textComponent.text = $"{temperature:F1}°C";
        }
        else
        {
            Debug.LogWarning($"[MarkerUpdater] No TextMeshProUGUI found on marker {marker.name}");
        }
    }

    // Public method to get top 5 hottest urban markers from a marker parent
    public List<(Transform marker, float value)> GetTop5HottestUrbanMarkers(Transform markerParent)
    {
        var urbanMarkers = new List<(Transform marker, float value)>();

        foreach (Transform child in markerParent)
        {
            var markerInfo = child.GetComponent<MarkerInfo>();
            if (markerInfo != null && !markerInfo.isRural)
            {
                var textComponent = child.GetComponentInChildren<TextMeshProUGUI>();
                if (textComponent != null)
                {
                    string tempText = textComponent.text.Replace("°C", "").Trim();
                    if (float.TryParse(tempText, NumberStyles.Float, CultureInfo.InvariantCulture, out float temp))
                    {
                        urbanMarkers.Add((child, temp));
                    }
                }
            }
        }

        // Sort by temperature (highest first) and take top 5
        urbanMarkers.Sort((a, b) => b.value.CompareTo(a.value));
        return urbanMarkers.Count > 5 ? urbanMarkers.GetRange(0, 5) : urbanMarkers;
    }

    // Method to update marker text directly (for mitigation effects)
    public void UpdateMarkerTemperature(Transform marker, float temperatureOffset)
    {
        var textComponent = marker.GetComponentInChildren<TextMeshProUGUI>();
        if (textComponent != null)
        {
            string currentText = textComponent.text.Replace("°C", "").Trim();
            if (float.TryParse(currentText, NumberStyles.Float, CultureInfo.InvariantCulture, out float currentTemp))
            {
                float newTemp = currentTemp + temperatureOffset;
                textComponent.text = $"{newTemp:F1}°C";
            }
        }
    }
} 