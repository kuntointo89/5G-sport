using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ECGGraph : MonoBehaviour
{
    public RawImage graphImage;
    public Color lineColor = Color.green;
    public int textureWidth = 512;
    public int textureHeight = 128;

    private Texture2D ecgTexture;

    public void DrawECG(List<int> samples)
    {
        if (graphImage == null || samples == null || samples.Count < 1)
        {
            Debug.LogWarning("ECGGraph: Graph image or sample data missing.");
            return;
        }

        // Create texture
        if (ecgTexture == null || ecgTexture.width != textureWidth || ecgTexture.height != textureHeight)
        {
            ecgTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            ecgTexture.filterMode = FilterMode.Point;
        }

        // Clear texture
        Color clearColor = new Color(0, 0, 0, 0);
        Color[] clearPixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < clearPixels.Length; i++)
            clearPixels[i] = clearColor;
        ecgTexture.SetPixels(clearPixels);

        // Normalize and draw ECG waveform
        float maxSample = Mathf.Max(Mathf.Abs((float)System.Linq.Enumerable.Max(samples)), 1f);
        int sampleCount = samples.Count;
        float xStep = (float)textureWidth / sampleCount;

        for (int i = 1; i < sampleCount; i++)
        {
            float x0 = (i - 1) * xStep;
            float x1 = i * xStep;

            float y0 = (samples[i - 1] / maxSample) * textureHeight / 2f + textureHeight / 2f;
            float y1 = (samples[i] / maxSample) * textureHeight / 2f + textureHeight / 2f;

            DrawLine((int)x0, (int)y0, (int)x1, (int)y1, lineColor);
        }

        ecgTexture.Apply();
        graphImage.texture = ecgTexture;
    }

    void DrawLine(int x0, int y0, int x1, int y1, Color color)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            if (x0 >= 0 && x0 < textureWidth && y0 >= 0 && y0 < textureHeight)
                ecgTexture.SetPixel(x0, y0, color);

            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }
}
