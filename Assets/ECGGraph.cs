using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;


// This code simulates ECGGraph from the given (mock)data (25.6.2025)

public class ECGGraph : MonoBehaviour
{
    public RawImage graphImage;                // UI element to display the ECG graph
    public int width = 300;                    // Width of the texture (in pixels)
    public int height = 100;                   // Height of the texture (in pixels)
    public Color graphColor = Color.green;     // Color used to draw the ECG line

    private Texture2D texture;                 // Texture that holds the ECG drawing

    public void DrawECG(List<int> samples)
    {
        if (graphImage == null)
        {
            Debug.LogWarning("ECGGraph: Graph RawImage not assigned.");
            return;
        }

        if (samples == null || samples.Count == 0)
        {
            Debug.LogWarning("ECGGraph: Sample data missing or empty.");
            return;
        }

        Debug.Log($"ECGGraph: Drawing {samples.Count} samples.");

        // Create a new texture if it doesn't exist or size changed
        if (texture == null || texture.width != width || texture.height != height)
        {
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            graphImage.texture = texture;
        }

        ClearTexture(); // Clear previous frame

        float maxSample = Mathf.Max(samples.ToArray());           // Get maximum amount of samples for scaling
        float scale = height / (maxSample > 0 ? maxSample : 1f);  // Avoiding divide-by-zero

        for (int i = 1; i < samples.Count; i++)
        {
            int x0 = Mathf.FloorToInt((i - 1) / (float)samples.Count * width);     // Previous x-axel
            int y0 = Mathf.FloorToInt(samples[i - 1] * scale);                     // Previous y-axel
            int x1 = Mathf.FloorToInt(i / (float)samples.Count * width);          // Current x-axel
            int y1 = Mathf.FloorToInt(samples[i] * scale);                         // Current y-axel

            DrawLine(texture, x0, y0, x1, y1, graphColor); // Drawing the line diagram for ECG
        }

        texture.Apply(); // Apply all pixel changes
    }

    void ClearTexture()
    {
        Color32[] clearPixels = new Color32[width * height];     // Create clear pixel buffer
        for (int i = 0; i < clearPixels.Length; i++) clearPixels[i] = Color.clear;
        texture.SetPixels32(clearPixels);                        // Clear texture
    }

    void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color col)
    {
        int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);    // Delta points
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;         // Step directions
        int err = dx - dy;                                       // Error term

        while (true)
        {
            // Set pixel if within bounds
            if (x0 >= 0 && x0 < tex.width && y0 >= 0 && y0 < tex.height)
                tex.SetPixel(x0, y0, col);

            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }               // Adjust x
            if (e2 < dx) { err += dx; y0 += sy; }                // Adjust y
        }
    }
}
