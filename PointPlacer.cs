// PointPlacerEditor.cs - place markers in Edit Mode with type-based prefabs
// Save this script under Assets/Scripts/

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class PointPlacer : MonoBehaviour
{
    public TextAsset csvFile;                // assign your CSV in Inspector
    public GameObject defaultMarkerPrefab;   // fallback marker prefab
    public GameObject urbanMarkerPrefab;     // marker prefab for urban (e.g., red)
    public GameObject ruralMarkerPrefab;     // marker prefab for rural (e.g., green)
    public Terrain terrain;                  // assign your Terrain
    public bool autoPlaceOnValidate = true;  // toggle auto-placement

    // Bounding box of your data
    const float minLon = 112.56098f, maxLon = 112.70244f;
    const float minLat = -8.05652f, maxLat = -7.90348f;

    /// <summary>
    /// Clear existing markers and instantiate new ones based on CSV type
    /// </summary>
    public void PlacePoints()
    {
        // Remove old markers
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        if (csvFile == null || terrain == null)
        {
            Debug.LogWarning("PointPlacer: Assign csvFile and terrain.");
            return;
        }

        string[] lines = csvFile.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var f = lines[i].Split(',');
            if (f.Length < 3) continue;

            // Parse type, lon, lat
            string type = f[0].Trim().ToLower();
            if (!float.TryParse(f[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float lon)) continue;
            if (!float.TryParse(f[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float lat)) continue;

            // Select prefab based on type
            GameObject prefabToUse = defaultMarkerPrefab;
            if (type == "urban" && urbanMarkerPrefab != null) prefabToUse = urbanMarkerPrefab;
            else if (type == "rural" && ruralMarkerPrefab != null) prefabToUse = ruralMarkerPrefab;

            if (prefabToUse == null)
            {
                Debug.LogWarning($"PointPlacer: No prefab assigned for type '{type}' on CSV line {i + 1}.");
                continue;
            }

            // Normalize lon/lat to [0,1]
            float tX = (maxLon - lon) / (maxLon - minLon);
            float tZ = (maxLat - lat) / (maxLat - minLat);

            // Convert to terrain local coords
            float localX = tX * terrain.terrainData.size.x;
            float localZ = tZ * terrain.terrainData.size.z;
            Vector3 basePos = new Vector3(
                terrain.transform.position.x + localX,
                0,
                terrain.transform.position.z + localZ
            );

            // Sample terrain height and instantiate marker
            float h = terrain.SampleHeight(basePos) + terrain.transform.position.y;
            Vector3 spawnPos = new Vector3(basePos.x, h + 0.05f, basePos.z);

            // Instantiate marker and ensure MarkerInfo is added
            var instance = Instantiate(prefabToUse, spawnPos, Quaternion.identity, transform);
            var info = instance.GetComponent<MarkerInfo>() ?? instance.gameObject.AddComponent<MarkerInfo>();
            info.longitude = lon;
            info.latitude  = lat;
        }
    }

    void OnValidate()
    {
        #if UNITY_EDITOR
        if (autoPlaceOnValidate)
        {
            // Delay to avoid running during serialization
            EditorApplication.delayCall += () => { if (this) PlacePoints(); };
        }
        #endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(PointPlacer))]
public class PointPlacerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        PointPlacer placer = (PointPlacer)target;
        if (GUILayout.Button("Place Points Now"))
            placer.PlacePoints();
    }
}
#endif

/*
Usage:
1. Save this file under Assets/Scripts/.
2. Attach PointPlacer component to an empty GameObject.
3. In Inspector, assign:
   - CSV File
   - Default Marker Prefab (if needed)
   - Urban Marker Prefab (red)
   - Rural Marker Prefab (green)
   - Terrain
4. Click 'Place Points Now' or enable autoPlaceOnValidate to auto-run.
*/
