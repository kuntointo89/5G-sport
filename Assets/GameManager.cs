
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public GameObject playerPrefab;
    public Text statsText;
    public GameObject statsPanelInstance;
    

    void Start()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("match_data");
    Root data = JsonUtility.FromJson<Root>(jsonFile.text);

    // 1. Gather all GNSS points for center calculation
    List<GNSSPoint> allPoints = new List<GNSSPoint>();
    foreach (var p in data.players) {
        foreach (var s in p.sensors) {
            if (s.GNSS != null)
                allPoints.AddRange(s.GNSS);
        }
    }

    Vector2 centerLatLon = GPSUtils.GetCenter(allPoints);
    double baseLat = centerLatLon.x;
    double baseLon = centerLatLon.y;

        foreach (var playerData in data.players)
        {
            Sensor sensor = playerData.sensors.Find(s => s.GNSS != null && s.GNSS.Count > 0);
            if (sensor == null) continue;

            List<Vector2> rawPath = new List<Vector2>();
            foreach (var point in sensor.GNSS)
                rawPath.Add(GPSUtils.GPSToMeters(point.Latitude, point.Longitude, baseLat, baseLon));

            List<Vector2> normalizedPath = GPSUtils.NormalizePath(rawPath, maxPathLength: 30f);

            GameObject player = Instantiate(playerPrefab, normalizedPath[0], Quaternion.identity);
            PlayerController pc = player.GetComponent<PlayerController>();
            pc.path = normalizedPath;
            pc.heartData = sensor.HR;
            pc.statsText = statsText;
            pc.statsPanel = statsPanelInstance;
        }
    }
}
        
    
