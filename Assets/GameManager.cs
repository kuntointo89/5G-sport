using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

//This script ensures that the data can be used in the application.

public class GameManager : MonoBehaviour
{
    public GameObject playerPrefab;
    public Text statsText;
    public GameObject statsPanelInstance;

    void Start()
    {
        // Load JSON data from Resources folder
        TextAsset jsonFile = Resources.Load<TextAsset>("match_data2");
        Root data = JsonUtility.FromJson<Root>(jsonFile.text);

        //  Gather all GNSS points for center calculation
        List<GNSSPoint> allPoints = new List<GNSSPoint>();
        foreach (var p in data.players)
        {
            foreach (var s in p.sensors)
            {
                if (s.GNSS != null)
                    allPoints.AddRange(s.GNSS);
            }
        }

        // Compute central lat/lon for relative positioning
        Vector2 centerLatLon = GPSUtils.GetCenter(allPoints);
        double baseLat = centerLatLon.x;
        double baseLon = centerLatLon.y;

        // Instantiate players based on data
        foreach (var playerData in data.players)
        {
            Sensor sensor = playerData.sensors.Find(s => s.GNSS != null && s.GNSS.Count > 0);
            if (sensor == null) continue;

            // Convert GNSS to 2D meters
            List<Vector2> rawPath = new List<Vector2>();
            foreach (var point in sensor.GNSS)
                rawPath.Add(GPSUtils.GPSToMeters(point.Latitude, point.Longitude, baseLat, baseLon));

            // Normalize movement path to specified max length
            List<Vector2> normalizedPath = GPSUtils.NormalizePath(rawPath, maxPathLength: 30f);

            // Instantiate player prefab and assign data
            GameObject player = Instantiate(playerPrefab, normalizedPath[0], Quaternion.identity);
            PlayerController pc = player.GetComponent<PlayerController>();
            pc.path = normalizedPath;
            pc.heartData = sensor.HR;
            pc.ecgData = sensor.ECG;
            pc.statsText = statsText;
            pc.statsPanel = statsPanelInstance;
        }
    }
}
