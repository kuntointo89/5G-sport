using System;
using System.Collections.Generic;
using UnityEngine;


// This scripts converts data to meters and keeps the players inside the rink

public static class GPSUtils
{

    // Converts GPS coordinates to local 2D Unity meters based on a base point (origin)
    public static Vector2 GPSToMeters(double lat, double lon, double baseLat, double baseLon)
    {
        double latDiff = lat - baseLat;
        double lonDiff = lon - baseLon;

        // Approximate conversion constants
        double metersPerLat = 111320; // meters per degree latitude
        double metersPerLon = 40075000 * Math.Cos(baseLat * Math.PI / 180) / 360; // meters per degree longitude (adjusted for latitude)

        float x = (float)(lonDiff * metersPerLon);
        float y = (float)(latDiff * metersPerLat);
        return new Vector2(x, y);
    }

    // Computes the average (center) GPS coordinate from a list of points
    public static Vector2 GetCenter(List<GNSSPoint> points)
    {
        double sumLat = 0, sumLon = 0;
        foreach (var p in points)
        {
            sumLat += p.Latitude;
            sumLon += p.Longitude;
        }
        return new Vector2((float)(sumLat / points.Count), (float)(sumLon / points.Count));
    }

    // Scales a path to ensure the total length does not exceed maxPathLength, in other words, keeps the players inside the hockey rink
    public static List<Vector2> NormalizePath(List<Vector2> path, float maxPathLength)
    {
        float totalLength = 0f;
        for (int i = 1; i < path.Count; i++)
            totalLength += Vector2.Distance(path[i - 1], path[i]);

        if (totalLength == 0) return path; // avoid division by zero

        float scale = maxPathLength / totalLength;
        for (int i = 0; i < path.Count; i++)
            path[i] *= scale;

        return path;
    }
}
