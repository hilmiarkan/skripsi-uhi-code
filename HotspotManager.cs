using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class HotspotManager : MonoBehaviour
{
    [Header("Hotspot Prefabs")]
    [Tooltip("Prefab untuk ring effect (menempel di tanah)")]
    public GameObject hotspotRingPrefab;
    [Tooltip("Prefab untuk marker yang berotasi (sedikit di atas tanah)")]
    public GameObject hotspotMarkerPrefab;
    [Tooltip("Prefab ActionMitigasi (diletakkan di bawah, sama seperti ring)")]
    public GameObject actionMitigasiPrefab;
    [Tooltip("Prefab tambahan untuk di-instantiate pada setiap hotspot")]
    public GameObject additionalPrefab;

    [Header("Hotspot Settings")]
    [Tooltip("Offset ketinggian untuk marker (agar 'mengambang' di atas tanah)")]
    public float markerHeightOffset = 200f;
    [Tooltip("Offset ketinggian untuk ring effect (agar menempel di tanah)")]
    public float ringHeightOffset = 0.1f;
    [Tooltip("Offset ketinggian untuk additional prefab")]
    public float additionalHeightOffset = 100f;

    [Header("Hotspot Parent")]
    [Tooltip("Parent Transform di mana semua hotspot akan di-Instantiate")]
    public Transform hotspotParent;

    private List<GameObject> spawnedHotspots = new List<GameObject>();

    /// <summary>
    /// Menghapus semua hotspot yang sudah ada
    /// </summary>
    public void ClearHotspots()
    {
        for (int i = spawnedHotspots.Count - 1; i >= 0; i--)
        {
            if (spawnedHotspots[i] != null)
                Destroy(spawnedHotspots[i]);
        }
        spawnedHotspots.Clear();
    }

    /// <summary>
    /// Membuat hotspot di posisi tertentu dengan tipe tertentu (raw/fuzzy)
    /// </summary>
    public void CreateHotspot(Vector3 position, string type, float lon, float lat)
    {
        // 1) Instantiate ring (menempel di tanah)
        if (hotspotRingPrefab != null && hotspotParent != null)
        {
            Vector3 ringPos = new Vector3(
                position.x,
                position.y + ringHeightOffset,
                position.z
            );
            GameObject ring = Instantiate(
                hotspotRingPrefab,
                ringPos,
                hotspotRingPrefab.transform.rotation,
                hotspotParent
            );
            ring.name = $"HotspotRing_{type}_{lon:F4}_{lat:F4}";
            spawnedHotspots.Add(ring);
        }

        // 2) Instantiate marker (tinggi di atas tanah)
        if (hotspotMarkerPrefab != null && hotspotParent != null)
        {
            Vector3 markerPos = new Vector3(
                position.x,
                position.y + markerHeightOffset,
                position.z
            );
            GameObject hm = Instantiate(
                hotspotMarkerPrefab,
                markerPos,
                hotspotMarkerPrefab.transform.rotation,
                hotspotParent
            );
            hm.name = $"HotspotMarker_{type}_{lon:F4}_{lat:F4}";
            spawnedHotspots.Add(hm);
        }

        // 3) Instantiate ActionMitigasi di bawah (sama seperti ring)
        if (actionMitigasiPrefab != null && hotspotParent != null)
        {
            Vector3 amPos = new Vector3(
                position.x,
                position.y + ringHeightOffset,
                position.z
            );
            GameObject am = Instantiate(
                actionMitigasiPrefab,
                amPos,
                actionMitigasiPrefab.transform.rotation,
                hotspotParent
            );
            am.name = $"ActionMitigasi_{type}_{lon:F4}_{lat:F4}";
            spawnedHotspots.Add(am);
        }

        // 4) Instantiate additionalPrefab
        if (additionalPrefab != null && hotspotParent != null)
        {
            Vector3 addPos = new Vector3(
                position.x,
                position.y + additionalHeightOffset,
                position.z
            );
            GameObject add = Instantiate(
                additionalPrefab,
                addPos,
                additionalPrefab.transform.rotation,
                hotspotParent
            );
            add.name = $"Additional_{type}_{lon:F4}_{lat:F4}";
            spawnedHotspots.Add(add);
        }
    }

    /// <summary>
    /// Membuat hotspot untuk 5 titik terpanas dari data yang diberikan
    /// </summary>
    public void CreateTop5Hotspots(List<(Transform marker, float value)> markers, string type)
    {
        ClearHotspots();

        // Pilih 5 marker dengan nilai tertinggi
        var top5 = markers
            .OrderByDescending(tuple => tuple.value)
            .Take(5)
            .ToList();

        // Buat hotspot untuk setiap marker
        foreach (var (marker, value) in top5)
        {
            var info = marker.GetComponent<MarkerInfo>();
            if (info != null)
            {
                CreateHotspot(marker.position, type, info.longitude, info.latitude);
            }
        }
    }
} 