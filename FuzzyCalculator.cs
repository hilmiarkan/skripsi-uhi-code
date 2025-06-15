using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class FuzzyCalculator : MonoBehaviour
{
    [Header("Events")]
    public System.Action<string> OnFuzzyStarted;
    public System.Action<string> OnFuzzyCompleted;
    public System.Action<string> OnFuzzyFailed;
    public System.Action<int, int> OnProgressUpdate; // (current, total)

    // Variables for fuzzy logic (must match CSV headers)
    private readonly string[] variables = new string[]
    {
        "temperature_2m", "dew_point_2m", "relative_humidity_2m",
        "wind_speed_10m", "vapour_pressure_deficit", "evapotranspiration"
    };

    // Improved fuzzy output parameters (calibrated for Malang climate)
    private const float OUT_MIN = 22f;
    private const float OUT_MAX = 32f;
    private const int OUT_RESOLUTION = 1000;
    private float[] outUniverse = new float[OUT_RESOLUTION];
    private float[][] outputMfs = new float[5][];

    // Enhanced fuzzy rules (15 rules, each with 7 integers: 6 input terms + 1 output term)
    private int[,] rules = new int[15, 7]
    {
        {3, 0, 1, 1, 0, 0, 5},   // High temp + Low RH + Calm wind → Very Hot
        {3, 3, 2, 0, 3, 3, 4},   // High temp + High dew + Med RH + High VPD + High ET → Hot
        {3, 2, 3, 3, 0, 0, 4},   // High temp + Med dew + High RH + Strong wind → Hot
        {3, 0, 2, 2, 2, 0, 3},   // High temp + Med RH + Med wind + Med VPD → Warm
        {2, 2, 2, 1, 1, 1, 2},   // Med temp + Med dew + Med RH + Calm wind + Low VPD + Low ET → Comfortable
        {2, 1, 3, 3, 1, 2, 2},   // Med temp + Low dew + High RH + Strong wind + Low VPD → Comfortable
        {2, 3, 1, 0, 3, 0, 3},   // Med temp + High dew + Low RH + High VPD → Warm
        {1, 1, 3, 3, 1, 1, 1},   // Low temp + Low dew + High RH + Strong wind + Low VPD + Low ET → Cool
        {1, 0, 2, 2, 0, 0, 1},   // Low temp + Med RH + Med wind → Cool
        {1, 2, 1, 1, 2, 2, 2},   // Low temp + Med dew + Low RH + Calm wind + Med VPD + Med ET → Comfortable
        {0, 3, 3, 1, 1, 0, 3},   // High dew + High RH + Calm wind + Low VPD → Warm (humid)
        {0, 1, 1, 3, 3, 3, 1},   // Low dew + Low RH + Strong wind + High VPD + High ET → Cool (dry windy)
        {2, 0, 1, 2, 2, 3, 4},   // Med temp + Low RH + Med wind + Med VPD + High ET → Hot
        {1, 3, 2, 0, 1, 1, 2},   // Low temp + High dew + Med RH + Low VPD + Low ET → Comfortable
        {3, 1, 3, 0, 1, 2, 3}    // High temp + Low dew + High RH + Low VPD + Med ET → Warm
    };

    void Awake()
    {
        InitializeFuzzySystem();
    }

    private void InitializeFuzzySystem()
    {
        // Initialize output universe
        float step = (OUT_MAX - OUT_MIN) / (OUT_RESOLUTION - 1);
        for (int i = 0; i < OUT_RESOLUTION; i++)
        {
            outUniverse[i] = OUT_MIN + i * step;
        }

        // Initialize output membership functions
        for (int k = 0; k < 5; k++)
        {
            outputMfs[k] = new float[OUT_RESOLUTION];
        }

        // Define improved output MFs (calibrated for Malang climate)
        for (int i = 0; i < OUT_RESOLUTION; i++)
        {
            float x = outUniverse[i];
            outputMfs[0][i] = TrapMF(x, 22f, 22f, 24f, 25f);      // 'Sejuk' (Cool)
            outputMfs[1][i] = TriMF(x, 24f, 25.5f, 27f);          // 'Nyaman' (Comfortable)
            outputMfs[2][i] = TriMF(x, 26f, 27.5f, 29f);          // 'Hangat' (Warm)
            outputMfs[3][i] = TriMF(x, 28f, 29.5f, 31f);          // 'Panas' (Hot)
            outputMfs[4][i] = TrapMF(x, 30f, 31f, 32f, 32f);      // 'Sangat Panas' (Very Hot)
        }
    }

    public void StartFuzzyCalculation(string rawCsvPath)
    {
        if (string.IsNullOrEmpty(rawCsvPath) || !File.Exists(rawCsvPath))
        {
            OnFuzzyFailed?.Invoke("Raw CSV file not found");
            return;
        }

        StartCoroutine(ProcessFuzzyCoroutine(rawCsvPath));
    }

    private IEnumerator ProcessFuzzyCoroutine(string rawCsvPath)
    {
        OnFuzzyStarted?.Invoke("Starting fuzzy calculation...");

        DataPackage package = DataManager.Instance.GetCurrentDataPackage();
        if (package == null)
        {
            OnFuzzyFailed?.Invoke("No current data package found");
            yield break;
        }

        // Read raw CSV
        string[] lines;
        try
        {
            lines = File.ReadAllLines(rawCsvPath);
        }
        catch (Exception ex)
        {
            OnFuzzyFailed?.Invoke($"Failed to read raw CSV: {ex.Message}");
            yield break;
        }

        if (lines.Length < 2)
        {
            OnFuzzyFailed?.Invoke("Raw CSV file is empty or invalid");
            yield break;
        }

        // Parse header to find column indices
        var header = lines[0].Split(',');
        int lonIdx = Array.IndexOf(header, "longitude");
        int latIdx = Array.IndexOf(header, "latitude");
        int[] varIdx = variables.Select(v => Array.IndexOf(header, v)).ToArray();

        if (lonIdx < 0 || latIdx < 0 || varIdx.Any(idx => idx < 0))
        {
            OnFuzzyFailed?.Invoke("Required columns not found in raw CSV");
            yield break;
        }

        // Prepare fuzzy results
        List<string> fuzzyLines = new List<string> { "longitude,latitude,apparent_temperature" };
        int totalPoints = lines.Length - 1;
        int processed = 0;

        // Process each data point
        for (int r = 1; r < lines.Length; r++)
        {
            processed++;
            OnProgressUpdate?.Invoke(processed, totalPoints);

            var cols = lines[r].Split(',');
            if (cols.Length <= Math.Max(lonIdx, Math.Max(latIdx, varIdx.Max())))
                continue;

            try
            {
                float lon = float.Parse(cols[lonIdx], CultureInfo.InvariantCulture);
                float lat = float.Parse(cols[latIdx], CultureInfo.InvariantCulture);
                
                float[] inputs = new float[variables.Length];
                for (int j = 0; j < variables.Length; j++)
                {
                    inputs[j] = float.Parse(cols[varIdx[j]], CultureInfo.InvariantCulture);
                }

                // Calculate fuzzy inference
                float[][] membershipValues = ComputeInputMemberships(inputs);
                float apparentTemperature = InferApparentTemperature(membershipValues);

                // Add to results
                fuzzyLines.Add(
                    $"{lon.ToString(CultureInfo.InvariantCulture)}," +
                    $"{lat.ToString(CultureInfo.InvariantCulture)}," +
                    $"{(float.IsNaN(apparentTemperature) ? -1f : apparentTemperature).ToString("F1", CultureInfo.InvariantCulture)}"
                );
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FuzzyCalculator] Error processing line {r}: {ex.Message}");
                continue;
            }

            // Yield periodically to maintain UI responsiveness
            if (processed % 10 == 0)
                yield return null;
        }

        // Save fuzzy results
        string fuzzyPath = SaveFuzzyResults(fuzzyLines, package.timestamp);
        if (!string.IsNullOrEmpty(fuzzyPath))
        {
            DataManager.Instance.SetFuzzyDataPath(package, fuzzyPath);
            OnFuzzyCompleted?.Invoke(fuzzyPath);
        }
        else
        {
            OnFuzzyFailed?.Invoke("Failed to save fuzzy results");
        }
    }

    private string SaveFuzzyResults(List<string> lines, string timestamp)
    {
        try
        {
            // Gunakan persistentDataPath untuk kompatibilitas dengan Meta Quest 2
            string outDir = Path.Combine(Application.persistentDataPath, DataManager.Instance.fuzzyDataFolder);
            string fileName = $"fuzzy_{timestamp}.csv";
            string fullPath = Path.Combine(outDir, fileName);

            File.WriteAllLines(fullPath, lines);
            Debug.Log($"[FuzzyCalculator] Fuzzy results saved to: {fullPath}");

#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
            return fullPath;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FuzzyCalculator] Failed to save fuzzy results: {ex.Message}");
            return null;
        }
    }

    private float[][] ComputeInputMemberships(float[] inputs)
    {
        // inputs: [temp, dew, rh, wind, vpd, et]
        float temp = inputs[0];
        float dew = inputs[1];
        float rh = inputs[2];
        float wind = inputs[3];
        float vpd = inputs[4];
        float et = inputs[5];

        // Temperature 2m: Improved ranges for Malang climate (21.1-27.1°C)
        float[] m1 = new float[3]
        {
            TrapMF(temp, 20f, 20f, 22f, 23.5f),     // Temperature 2m: 'Rendah' (Low)
            TriMF(temp, 22f, 24f, 26f),             // Temperature 2m: 'Sedang' (Medium)
            TrapMF(temp, 25f, 26.5f, 28f, 28f)      // Temperature 2m: 'Tinggi' (High)
        };

        // Dew Point 2m: Improved ranges for Malang climate (18.6-21.8°C)
        float[] m2 = new float[3]
        {
            TrapMF(dew, 17f, 17f, 19f, 20f),        // Dew Point 2m: 'Rendah' (Low)
            TriMF(dew, 19f, 20f, 21f),              // Dew Point 2m: 'Sedang' (Medium)
            TrapMF(dew, 20.5f, 21.5f, 23f, 23f)     // Dew Point 2m: 'Tinggi' (High)
        };

        // Relative Humidity 2m: Improved ranges for Malang climate (67-87%)
        float[] m3 = new float[3]
        {
            TrapMF(rh, 60f, 60f, 70f, 75f),         // Relative Humidity 2m: 'Rendah' (Low)
            TriMF(rh, 70f, 78f, 85f),               // Relative Humidity 2m: 'Sedang' (Medium)
            TrapMF(rh, 82f, 86f, 90f, 90f)          // Relative Humidity 2m: 'Tinggi' (High)
        };

        // Wind Speed 10m: Improved ranges for Malang climate (4.5-15.8 m/s)
        float[] m4 = new float[3]
        {
            TrapMF(wind, 3f, 3f, 6f, 8f),           // Wind Speed 10m: 'Tenang' (Calm)
            TriMF(wind, 6f, 9f, 12f),               // Wind Speed 10m: 'Sedang' (Medium)
            TrapMF(wind, 10f, 14f, 18f, 18f)        // Wind Speed 10m: 'Kencang' (Strong)
        };

        // Vapour Pressure Deficit: Improved ranges for Malang climate (0.32-1.16)
        float[] m5 = new float[3]
        {
            TrapMF(vpd, 0.2f, 0.2f, 0.4f, 0.6f),    // Vapour Pressure Deficit: 'Rendah' (Low)
            TriMF(vpd, 0.5f, 0.7f, 0.9f),           // Vapour Pressure Deficit: 'Sedang' (Medium)
            TrapMF(vpd, 0.8f, 1.0f, 1.3f, 1.3f)     // Vapour Pressure Deficit: 'Tinggi' (High)
        };

        // Evapotranspiration: Improved ranges for Malang climate (0.178-0.238)
        float[] m6 = new float[3]
        {
            TrapMF(et, 0.15f, 0.15f, 0.18f, 0.20f), // Evapotranspiration: 'Rendah' (Low)
            TriMF(et, 0.19f, 0.21f, 0.23f),         // Evapotranspiration: 'Sedang' (Medium)
            TrapMF(et, 0.22f, 0.24f, 0.26f, 0.26f)  // Evapotranspiration: 'Tinggi' (High)
        };

        return new float[][] { m1, m2, m3, m4, m5, m6 };
    }

    private float InferApparentTemperature(float[][] membershipValues)
    {
        // Collect rule activations
        List<(float alpha, int outTerm)> activations = new List<(float, int)>();

        for (int ri = 0; ri < 15; ri++)
        {
            List<float> degrees = new List<float>();
            for (int i = 0; i < 6; i++)
            {
                int termIdx = rules[ri, i];
                if (termIdx != 0)
                {
                    degrees.Add(membershipValues[i][termIdx - 1]);
                }
            }
            float alpha = (degrees.Count > 0) ? degrees.Min() : 0f;
            int outputTerm = rules[ri, 6] - 1; // Convert to 0-based index
            activations.Add((alpha, outputTerm));
        }

        // Aggregate output membership functions
        float[] aggregated = new float[OUT_RESOLUTION];
        for (int k = 0; k < OUT_RESOLUTION; k++)
            aggregated[k] = 0f;

        foreach (var (alpha, outTerm) in activations)
        {
            if (alpha > 0f)
            {
                float[] mf = outputMfs[outTerm];
                for (int i = 0; i < OUT_RESOLUTION; i++)
                {
                    float clipped = Mathf.Min(alpha, mf[i]);
                    if (clipped > aggregated[i])
                        aggregated[i] = clipped;
                }
            }
        }

        // Defuzzification using centroid method
        float numerator = 0f, denominator = 0f;
        for (int i = 0; i < OUT_RESOLUTION; i++)
        {
            numerator += outUniverse[i] * aggregated[i];
            denominator += aggregated[i];
        }

        if (Mathf.Approximately(denominator, 0f))
            return 27f;  // Default to middle of improved output range (22-32°C)
        
        return numerator / denominator;
    }

    // Trapezoidal membership function
    private static float TrapMF(float x, float a, float b, float c, float d)
    {
        if (x <= a || x >= d) return 0f;
        float left = (b == a) ? 1f : Mathf.Clamp01((x - a) / (b - a));
        float right = (d == c) ? 1f : Mathf.Clamp01((d - x) / (d - c));
        return Mathf.Max(Mathf.Min(left, right), 0f);
    }

    // Triangular membership function
    private static float TriMF(float x, float a, float b, float c)
    {
        if (x <= a || x >= c) return 0f;
        if (Mathf.Approximately(x, b)) return 1f;
        if (x > a && x < b) return (x - a) / (b - a);
        return (c - x) / (c - b);
    }

    // Public method to calculate UHI intensity from CSV data
    public float CalculateUHIIntensity(string csvPath, bool isFuzzyData = false)
    {
        if (!File.Exists(csvPath))
            return 0f;

        try
        {
            string[] lines = File.ReadAllLines(csvPath);
            if (lines.Length < 2) return 0f;

            var header = lines[0].Split(',');
            int typeIdx = Array.IndexOf(header, "type");
            int tempIdx = isFuzzyData ? Array.IndexOf(header, "apparent_temperature") : Array.IndexOf(header, "temperature_2m");

            if (typeIdx < 0 || tempIdx < 0) return 0f;

            float maxUrbanTemp = float.MinValue;
            float minRuralTemp = float.MaxValue;
            bool hasUrbanData = false;
            bool hasRuralData = false;

            for (int i = 1; i < lines.Length; i++)
            {
                var cols = lines[i].Split(',');
                if (cols.Length <= Math.Max(typeIdx, tempIdx)) continue;

                string type = cols[typeIdx].Trim().ToLowerInvariant();
                if (float.TryParse(cols[tempIdx], NumberStyles.Float, CultureInfo.InvariantCulture, out float temp))
                {
                    if (type == "urban")
                    {
                        if (temp > maxUrbanTemp)
                        {
                            maxUrbanTemp = temp;
                            hasUrbanData = true;
                        }
                    }
                    else if (type == "rural")
                    {
                        if (temp < minRuralTemp)
                        {
                            minRuralTemp = temp;
                            hasRuralData = true;
                        }
                    }
                }
            }

            if (!hasUrbanData || !hasRuralData) 
            {
                Debug.LogWarning("[FuzzyCalculator] Insufficient data for UHI calculation - need both urban and rural data");
                return 0f;
            }

            float uhiIntensity = maxUrbanTemp - minRuralTemp;
            Debug.Log($"[FuzzyCalculator] UHI Intensity: Max Urban ({maxUrbanTemp:F1}°C) - Min Rural ({minRuralTemp:F1}°C) = {uhiIntensity:F1}°C");
            return uhiIntensity;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FuzzyCalculator] Error calculating UHI: {ex.Message}");
            return 0f;
        }
    }
} 