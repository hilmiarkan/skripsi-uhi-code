// FuzzyRuntimeCalculator.cs
// MonoBehaviour to process the latest Open-Meteo CSV and compute apparent temperature via fuzzy logic
// Updates TextMeshProUGUI labels on markers that have a MarkerInfo component.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using TMPro;
using Debug = UnityEngine.Debug;
using UnityEngine.UI;

[RequireComponent(typeof(MarkerInfo))]
public class MarkerInfo : MonoBehaviour
{
    public float longitude;
    public float latitude;
    public bool isRural;
}

public class FuzzyRuntimeCalculator : MonoBehaviour
{
    [Header("UI Settings")]
    public Button fetchButton;

    [Header("Markers Parent")]
    public Transform markerParent;

    [Header("Fetched CSVs Folder (persistentDataPath)")]
    public string fetchedDataFolder = "FetchedData";

    [Header("UHI Result Display")]
    public TextMeshProUGUI uhiResultText;

    // Variables for fuzzy logic
    private readonly string[] variables = new string[]
    {
        "temperature_2m", "dew_point_2m", "relative_humidity_2m",
        "wind_speed_10m", "vapour_pressure_deficit", "evapotranspiration"
    };

    // Fuzzy membership definitions
    private float TrapMF(float x, float a, float b, float c, float d)
    {
        if (x <= a || x >= d) return 0f;
        else if (x >= b && x <= c) return 1f;
        else if (x > a && x < b) return (x - a) / (b - a);
        else /* if (x > c && x < d) */ return (d - x) / (d - c);
    }

    private float TriMF(float x, float a, float b, float c)
    {
        if (x <= a || x >= c) return 0f;
        else if (x == b) return 1f;
        else if (x > a && x < b) return (x - a) / (b - a);
        else /* if (x > b && x < c) */ return (c - x) / (c - b);
    }

    public void ProcessLatestCsv()
    {
        string dir = Path.Combine(Application.persistentDataPath, fetchedDataFolder);
        if (!Directory.Exists(dir))
        {
            Debug.LogError($"Fuzzy: folder not found: {dir}");
            return;
        }
        var files = Directory.GetFiles(dir, "meteo_*.csv");
        if (files.Length == 0)
        {
            Debug.LogWarning("Fuzzy: no CSV files found.");
            return;
        }
        string latest = files.OrderByDescending(f => File.GetLastWriteTimeUtc(f)).First();
        StartCoroutine(ProcessCsvRoutine(latest));
    }

    public void ProcessCsv(string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            Debug.LogError($"Fuzzy: CSV not found: {csvPath}");
            return;
        }
        if (fetchButton != null) fetchButton.interactable = false;
        StartCoroutine(ProcessCsvRoutine(csvPath));
    }

    private IEnumerator ProcessCsvRoutine(string csvPath)
    {
        // Prepare fuzzy results output
        List<string> fuzzyLines = new List<string> { "longitude,latitude,apparent_temperature" };

        string[] lines = File.ReadAllLines(csvPath);
        int totalPoints = Mathf.Max(0, lines.Length - 1);
        int processed = 0;
        if (lines.Length < 2) yield break;
        // header indices
        var header = lines[0].Split(',');
        int lonIdx = Array.IndexOf(header, "longitude");
        int latIdx = Array.IndexOf(header, "latitude");
        int[] varIdx = variables.Select(v => Array.IndexOf(header, v)).ToArray();

        // Universe for defuzzification
        int outMin = 15, outMax = 45;
        int resolution = outMax - outMin + 1;
        float[] outUniverse = new float[resolution];
        for (int i = 0; i < resolution; i++) outUniverse[i] = outMin + i;

        // Pre-calculate output MFs
        // sejuk: trap(15,15,18,22)
        // nyaman: tri(18,24,30)
        // hangat: tri(25,30,35)
        // panas: tri(30,35,40)
        // sangat_panas: trap(38,42,45,45)
        float[][] outMfs = new float[5][];
        for (int i = 0; i < resolution; i++)
        {
            float x = outUniverse[i];
            outMfs[0] ??= new float[resolution]; outMfs[0][i] = TrapMF(x, 15,15,18,22);
            outMfs[1] ??= new float[resolution]; outMfs[1][i] = TriMF(x,18,24,30);
            outMfs[2] ??= new float[resolution]; outMfs[2][i] = TriMF(x,25,30,35);
            outMfs[3] ??= new float[resolution]; outMfs[3][i] = TriMF(x,30,35,40);
            outMfs[4] ??= new float[resolution]; outMfs[4][i] = TrapMF(x,38,42,45,45);
        }

        // Loop rows
        for (int r = 1; r < lines.Length; r++)
        {
            processed++;
            if (fetchButton != null)
            {
                var btnText = fetchButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                    btnText.text = $"{processed}/{totalPoints} titik";
            }

            var cols = lines[r].Split(',');
            float lon = float.Parse(cols[lonIdx], CultureInfo.InvariantCulture);
            float lat = float.Parse(cols[latIdx], CultureInfo.InvariantCulture);
            float[] inputs = new float[variables.Length];
            for (int j = 0; j < variables.Length; j++)
                inputs[j] = float.Parse(cols[varIdx[j]], CultureInfo.InvariantCulture);

            // Evaluate membership for inputs
            float temp = inputs[0]; float dew = inputs[1]; float hum = inputs[2];
            float wind = inputs[3]; float vpd = inputs[4]; float et = inputs[5];
            // input MFs for each term
            var temp_rendah = TrapMF(temp,15,15,18,22);
            var temp_sedang = TriMF(temp,18,25,32);
            var temp_tinggi = TrapMF(temp,28,32,35,35);
            var dew_rendah = TrapMF(dew,10,10,13,17);
            var dew_sedang = TriMF(dew,13,18,23);
            var dew_tinggi = TriMF(dew,20,23,25);
            var hum_rendah = TrapMF(hum,40,40,50,60);
            var hum_sedang = TriMF(hum,50,65,80);
            var hum_tinggi = TrapMF(hum,75,85,100,100);
            var wind_tenang = TrapMF(wind,0,0,3,7);
            var wind_sedang = TriMF(wind,5,10,15);
            var wind_kencang = TrapMF(wind,12,16,20,20);
            var vpd_rendah = TrapMF(vpd,0f,0f,0.5f,1f);
            var vpd_sedang = TriMF(vpd,0.5f,1.5f,2.5f);
            var vpd_tinggi = TrapMF(vpd,2f,2.5f,3f,3f);
            var et_rendah = TrapMF(et,0f,0f,1f,2f);
            var et_sedang = TriMF(et,1.5f,3f,4.5f);
            var et_tinggi = TrapMF(et,3.5f,5f,6f,6f);

            // Apply rules and aggregate
            float[] aggregated = new float[resolution];
            void ApplyRule(float degree, float[] outMf)
            {
                for (int i = 0; i < resolution; i++)
                    aggregated[i] = Math.Max(aggregated[i], Math.Min(degree, outMf[i]));
            }
            // Rule 1
            ApplyRule(Math.Min(Math.Min(temp_tinggi, hum_rendah), wind_tenang), outMfs[4]);
            // Rule 2
            ApplyRule(Math.Min(Math.Min(temp_tinggi, hum_rendah), wind_kencang), outMfs[3]);
            // Rule 3
            ApplyRule(Math.Min(Math.Min(temp_sedang, dew_tinggi), hum_sedang), outMfs[1]);
            // Rule 4
            ApplyRule(Math.Min(temp_rendah, wind_kencang), outMfs[0]);
            // Rule 5
            ApplyRule(Math.Min(Math.Min(temp_tinggi, vpd_tinggi), et_tinggi), outMfs[3]);
            // Rule 6
            ApplyRule(Math.Min(dew_tinggi, hum_tinggi), outMfs[2]);
            // Rule 7
            ApplyRule(Math.Min(temp_sedang, wind_kencang), outMfs[0]);
            // Rule 8
            ApplyRule(Math.Min(Math.Min(hum_tinggi, vpd_rendah), et_rendah), outMfs[0]);
            // Rule 9
            ApplyRule(Math.Min(Math.Min(temp_tinggi, dew_tinggi), wind_kencang), outMfs[2]);
            // Rule 10
            ApplyRule(Math.Min(Math.Min(temp_sedang, dew_rendah), hum_rendah), outMfs[0]);
            // Rule 11
            ApplyRule(Math.Min(temp_rendah, hum_tinggi), outMfs[0]);
            // Rule 12
            ApplyRule(temp_tinggi, outMfs[3]);

            // Defuzzify (centroid)
            float num = 0f, den = 0f;
            for (int i = 0; i < resolution; i++)
            {
                num += outUniverse[i] * aggregated[i];
                den += aggregated[i];
            }
            float at = den > 0f ? num / den : float.NaN;

            // Update marker label
            foreach (Transform child in markerParent)
            {
                var info = child.GetComponent<MarkerInfo>();
                if (info != null && Math.Abs(info.longitude - lon) < 1e-4f && Math.Abs(info.latitude - lat) < 1e-4f)
                {
                    var label = child.GetComponentInChildren<TextMeshProUGUI>();
                    if (label != null) label.text = float.IsNaN(at) ? "--" : at.ToString("F1") + "°C";
                    break;
                }
            }

            // Record fuzzy result
            fuzzyLines.Add(
                $"{lon.ToString(CultureInfo.InvariantCulture)}," +
                $"{lat.ToString(CultureInfo.InvariantCulture)}," +
                $"{(float.IsNaN(at) ? -1f : at).ToString("F1", CultureInfo.InvariantCulture)}"
            );

            yield return null;
        }

        // Write fuzzy results to file
        string outDir = Path.Combine(Application.persistentDataPath, "FuzzyResults");
        Directory.CreateDirectory(outDir);
        string ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string fname = $"fuzzy_{ts}.csv";
        string fullOut = Path.Combine(outDir, fname);
        File.WriteAllLines(fullOut, fuzzyLines);
        Debug.Log($"[Fuzzy] Results saved to: {fullOut}");

        // Restore button
        if (fetchButton != null)
        {
            fetchButton.interactable = true;
            var btnText = fetchButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
                btnText.text = "Fetch Data";
        }

        // Calculate and display UHI intensity
        // Calculate mean rural AT
        float sumRural = 0f;
        int countRural = 0;
        float sumDiff = 0f;
        int countUrban = 0;
        foreach (Transform child in markerParent)
        {
            var info = child.GetComponent<MarkerInfo>();
            var label = child.GetComponentInChildren<TextMeshProUGUI>();
            if (info != null && label != null)
            {
                string txt = label.text.Replace("°C", "");
                if (float.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                {
                    if (info.isRural)
                    {
                        sumRural += val;
                        countRural++;
                    }
                }
            }
        }
        float meanRural = countRural > 0 ? (sumRural / countRural) : 0f;
        // Compute UHI differences for non-rural (urban) points
        foreach (Transform child in markerParent)
        {
            var info = child.GetComponent<MarkerInfo>();
            var label = child.GetComponentInChildren<TextMeshProUGUI>();
            if (info != null && label != null && !info.isRural)
            {
                string txt = label.text.Replace("°C", "");
                if (float.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                {
                    sumDiff += (val - meanRural);
                    countUrban++;
                }
            }
        }
        float uhiIntensity = countUrban > 0 ? (sumDiff / countUrban) : 0f;
        if (uhiResultText != null)
            uhiResultText.text = $"UHI Intensity: {uhiIntensity:F1}°C";
    }
}
