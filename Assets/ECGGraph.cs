using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ECGGraph : MonoBehaviour
{
    public RawImage graphImage;
    public int width = 300;
    public int height = 100;
    public Color graphColor = Color.green;

    private Texture2D texture;

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

        if (texture == null || texture.width != width || texture.height != height)
        {
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            graphImage.texture = texture;
        }

        ClearTexture();

        float maxSample = Mathf.Max(samples.ToArray());
        float scale = height / (maxSample > 0 ? maxSample : 1f);

        for (int i = 1; i < samples.Count; i++)
        {
            int x0 = Mathf.FloorToInt((i - 1) / (float)samples.Count * width);
            int y0 = Mathf.FloorToInt(samples[i - 1] * scale);
            int x1 = Mathf.FloorToInt(i / (float)samples.Count * width);
            int y1 = Mathf.FloorToInt(samples[i] * scale);

            DrawLine(texture, x0, y0, x1, y1, graphColor);
        }

        texture.Apply();
    }

    void ClearTexture()
    {
        Color32[] clearPixels = new Color32[width * height];
        for (int i = 0; i < clearPixels.Length; i++) clearPixels[i] = Color.clear;
        texture.SetPixels32(clearPixels);
    }

    void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color col)
    {
        int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            if (x0 >= 0 && x0 < tex.width && y0 >= 0 && y0 < tex.height)
                tex.SetPixel(x0, y0, col);

            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }
}