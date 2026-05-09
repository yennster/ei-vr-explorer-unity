using System.Collections.Generic;
using UnityEngine;

namespace EI.VR
{
    /// <summary>
    /// Turns a FOMO model's raw output tensor into a list of bounding-box
    /// detections. FOMO outputs a per-cell heatmap shaped (1, H, W, C) where
    /// C = numClasses + 1 (the +1 channel is "background"). Each grid cell's
    /// non-background channel value is the probability of an object centred
    /// in that cell. We threshold and group adjacent active cells per class
    /// via a simple 4-connected flood-fill so spatially-coherent clusters
    /// become a single bounding box.
    /// </summary>
    public static class FomoOutputParser
    {
        public struct Detection
        {
            // Normalised image-space rect, all in [0, 1].
            public Rect rect;
            public int classIndex;
            public float score;
        }

        public static List<Detection> Parse(
            float[] grid,
            int gridH,
            int gridW,
            int channels,
            float threshold = 0.5f,
            int backgroundChannel = 0)
        {
            var detections = new List<Detection>();
            var visited = new bool[gridH * gridW];

            for (int y = 0; y < gridH; y++)
            {
                for (int x = 0; x < gridW; x++)
                {
                    int cellIdx = y * gridW + x;
                    if (visited[cellIdx]) continue;
                    int bestClass = ArgmaxClass(grid, x, y, gridW, channels, backgroundChannel);
                    if (bestClass < 0) { visited[cellIdx] = true; continue; }
                    float score = grid[cellIdx * channels + bestClass];
                    if (score < threshold) { visited[cellIdx] = true; continue; }

                    int minX = x, maxX = x, minY = y, maxY = y;
                    float maxScore = score;
                    var stack = new Stack<(int x, int y)>();
                    stack.Push((x, y));
                    while (stack.Count > 0)
                    {
                        var (cx, cy) = stack.Pop();
                        int idx = cy * gridW + cx;
                        if (visited[idx]) continue;
                        if (ArgmaxClass(grid, cx, cy, gridW, channels, backgroundChannel) != bestClass) continue;
                        float s = grid[idx * channels + bestClass];
                        if (s < threshold) continue;
                        visited[idx] = true;
                        if (s > maxScore) maxScore = s;
                        if (cx < minX) minX = cx; if (cx > maxX) maxX = cx;
                        if (cy < minY) minY = cy; if (cy > maxY) maxY = cy;
                        if (cx + 1 < gridW) stack.Push((cx + 1, cy));
                        if (cx - 1 >= 0) stack.Push((cx - 1, cy));
                        if (cy + 1 < gridH) stack.Push((cx, cy + 1));
                        if (cy - 1 >= 0) stack.Push((cx, cy - 1));
                    }

                    detections.Add(new Detection
                    {
                        rect = new Rect(
                            (float)minX / gridW,
                            (float)minY / gridH,
                            (maxX - minX + 1f) / gridW,
                            (maxY - minY + 1f) / gridH),
                        classIndex = bestClass,
                        score = maxScore,
                    });
                }
            }
            return detections;
        }

        private static int ArgmaxClass(float[] grid, int x, int y, int gridW, int channels, int backgroundChannel)
        {
            int idx = (y * gridW + x) * channels;
            int best = -1;
            float bestVal = float.NegativeInfinity;
            for (int c = 0; c < channels; c++)
            {
                if (c == backgroundChannel) continue;
                float v = grid[idx + c];
                if (v > bestVal) { bestVal = v; best = c; }
            }
            return best;
        }
    }
}
