using System;
using System.Collections.Generic;
using UnityEngine;

public static class GPSUtils
{
    /// <summary>
    /// Converts latitude/longitude to Unity meters relative to a base GPS coordinate.
    /// </summary>
    public static Vector2 GPSToMeters(double lat, double lon, double baseLat, double baseLon)
    {
        const double metersPerDegreeLat = 111_320; // constant meters per degree of latitude
        double latDiff = lat - baseLat;
        double lonDiff = lon - baseLon;

        // Longitude varies with latitude
        double metersPerDegreeLon = 40075000 * Math.Cos(baseLat * Math.PI / 180) / 360.0;

        float x = (float)(lonDiff * metersPerDegreeLon);
        float y = (float)(latDiff * metersPerDegreeLat);

        return new Vector2(x, y);
    }
    public static void LogGPSPosition(string label, double lat, double lon, double baseLat, double baseLon)
{
    Vector2 meters = GPSToMeters(lat, lon, baseLat, baseLon);
    Debug.Log($"{label}: GPS ({lat:F6}, {lon:F6}) → Unity meters {meters}");
}


    /// <summary>
    /// Gets the average center point (lat, lon) of a collection of GNSS points.
    /// </summary>
    public static Vector2 GetCenter(List<GNSSPoint> points)
    {
        if (points == null || points.Count == 0)
            throw new ArgumentException("GNSS point list is empty or null.");

        double sumLat = 0;
        double sumLon = 0;

        foreach (var p in points)
        {
            sumLat += p.Latitude;
            sumLon += p.Longitude;
        }

        return new Vector2((float)(sumLat / points.Count), (float)(sumLon / points.Count));
    }

    /// <summary>
    /// Scales a movement path to fit within a defined max length.
    /// </summary>
    public static List<Vector2> NormalizePath(List<Vector2> path, float maxPathLength)
    {
        if (path == null || path.Count < 2) return path;

        float totalLength = 0f;
        for (int i = 1; i < path.Count; i++)
            totalLength += Vector2.Distance(path[i - 1], path[i]);

        if (totalLength == 0f) return path;

        float scale = maxPathLength / totalLength;
        List<Vector2> scaledPath = new List<Vector2>(path.Count);

        foreach (var point in path)
            scaledPath.Add(point * scale);

        return scaledPath;
    }
}
