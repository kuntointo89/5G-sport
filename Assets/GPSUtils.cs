using System;
using System.Collections.Generic;
using UnityEngine;

public static class GPSUtils {

    public static Vector2 GPSToMeters(double lat, double lon, double baseLat, double baseLon) {

        double latDiff = lat - baseLat;
        double lonDiff = lon - baseLon;

        double metersPerLat = 111320;
        double metersPerLon = 40075000 * Math.Cos(baseLat * Math.PI / 180) / 360;

        float x = (float)(lonDiff * metersPerLon);
        float y = (float)(latDiff * metersPerLat);
        return new Vector2(x, y);
    }

    // Calculate center of a collection of GPS points
    public static Vector2 GetCenter(List<GNSSPoint> points) {
        double sumLat = 0, sumLon = 0;
        foreach (var p in points) {
            sumLat += p.Latitude;
            sumLon += p.Longitude;
        }
        return new Vector2((float)(sumLat / points.Count), (float)(sumLon / points.Count));
    }

    // Normalize path so total movement fits within maxPathLength (Unity units)
    public static List<Vector2> NormalizePath(List<Vector2> path, float maxPathLength) {
        float totalLength = 0f;
        for (int i = 1; i < path.Count; i++)
            totalLength += Vector2.Distance(path[i - 1], path[i]);

        if (totalLength == 0) return path;

        float scale = maxPathLength / totalLength;
        for (int i = 0; i < path.Count; i++)
            path[i] *= scale;

        return path;
    }
}