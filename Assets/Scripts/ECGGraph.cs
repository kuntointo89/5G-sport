using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ECGGraph : MonoBehaviour
{
    public RawImage graphImage; // Assign this in the Inspector
    public Color graphColor = Color.green;
    public int width = 80;
    public int height = 80;
    public int maxLiveSamples = 300;

    private Texture2D texture;
    private List<int> liveSamples = new List<int>();

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

    if (texture == null || texture.width != width || texture.height != height)
    {
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        graphImage.texture = texture;
    }

    ClearTexture();

    // Find min and max samples dynamically
    int minSample = int.MaxValue;
    int maxSample = int.MinValue;
    foreach (var s in samples)
    {
        if (s < minSample) minSample = s;
        if (s > maxSample) maxSample = s;
    }

    // Small vertical padding
    float padding = 5f;
    float sampleRange = maxSample - minSample;
    if (sampleRange == 0) sampleRange = 1; // avoid division by zero

    for (int i = 1; i < samples.Count; i++)
    {
        int x0 = Mathf.FloorToInt((i - 1) / (float)(samples.Count - 1) * (width - 1));
        int x1 = Mathf.FloorToInt(i / (float)(samples.Count - 1) * (width - 1));

        // Normalize sample to 0..1
        float normY0 = (samples[i - 1] - minSample) / sampleRange;
        float normY1 = (samples[i] - minSample) / sampleRange;

        // Invert Y because texture Y=0 is bottom
        int y0 = Mathf.Clamp(Mathf.FloorToInt(normY0 * (height - 1 - padding * 2) + padding), 0, height - 1);
        int y1 = Mathf.Clamp(Mathf.FloorToInt(normY1 * (height - 1 - padding * 2) + padding), 0, height - 1);

        DrawLine(texture, x0, y0, x1, y1, graphColor);
    }

    texture.Apply();
}


    public void AddSample(int sample)
    {
        Debug.Log($"ECGGraph AddSample: {sample}");
        if (liveSamples.Count >= maxLiveSamples)
            liveSamples.RemoveAt(0);

        liveSamples.Add(sample);
        DrawECG(liveSamples);
    }

    void ClearTexture()
    {
        Color32[] clearPixels = new Color32[width * height];
        for (int i = 0; i < clearPixels.Length; i++)
            clearPixels[i] = Color.clear;

        texture.SetPixels32(clearPixels);
    }

    void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color col)
    {
        int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
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