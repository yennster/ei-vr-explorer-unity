using System.Collections.Generic;
using UnityEngine;

namespace EI.VR
{
    /// <summary>
    /// Renders the EI 3D feature scatter as a room-scale cloud of instanced
    /// spheres. One Graphics.RenderMeshInstanced call per class so we can
    /// push 10k+ points at 90 Hz on Quest 2.
    ///
    /// Inputs come from EdgeImpulseClient.FeatureGraphResponse — call
    /// SetData() with the response plus the chosen X/Y/Z feature indices.
    /// </summary>
    public class FeatureCloud : MonoBehaviour
    {
        [SerializeField] private Mesh pointMesh;        // a low-poly sphere
        [SerializeField] private Material pointMaterial; // unlit, instancing on
        [SerializeField] private float pointScale = 0.02f;
        [SerializeField] private float worldScale = 1.5f;

        private readonly Color[] _palette = new[]
        {
            new Color(0.96f, 0.27f, 0.31f), new Color(0.31f, 0.78f, 0.47f),
            new Color(0.31f, 0.50f, 0.96f), new Color(0.96f, 0.78f, 0.31f),
            new Color(0.78f, 0.31f, 0.96f), new Color(0.31f, 0.78f, 0.96f),
            new Color(0.96f, 0.50f, 0.31f), new Color(0.50f, 0.96f, 0.31f),
        };

        private readonly Dictionary<string, List<Matrix4x4>> _byClass = new();
        private readonly Dictionary<string, Color> _classColor = new();
        private readonly List<PointMeta> _meta = new();

        public IReadOnlyList<PointMeta> Points => _meta;

        public void SetData(EdgeImpulseClient.FeatureGraphResponse resp, int axX, int axY, int axZ)
        {
            _byClass.Clear();
            _classColor.Clear();
            _meta.Clear();

            // Find min/max per axis to normalize into a [-worldScale, +worldScale]^3 cube.
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            double minZ = double.MaxValue, maxZ = double.MinValue;
            foreach (var p in resp.data)
            {
                if (!p.X.TryGetValue(axX.ToString(), out var x)) continue;
                if (!p.X.TryGetValue(axY.ToString(), out var y)) continue;
                if (!p.X.TryGetValue(axZ.ToString(), out var z)) continue;
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
                if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
            }

            float Norm(double v, double lo, double hi)
            {
                if (hi - lo < 1e-9) return 0f;
                return (float)((v - lo) / (hi - lo) * 2.0 - 1.0) * worldScale;
            }

            for (int i = 0; i < resp.data.Count; i++)
            {
                var p = resp.data[i];
                if (!p.X.TryGetValue(axX.ToString(), out var x)) continue;
                if (!p.X.TryGetValue(axY.ToString(), out var y)) continue;
                if (!p.X.TryGetValue(axZ.ToString(), out var z)) continue;

                var pos = new Vector3(Norm(x, minX, maxX), Norm(y, minY, maxY), Norm(z, minZ, maxZ));
                if (!_byClass.TryGetValue(p.yLabel, out var list))
                {
                    list = new List<Matrix4x4>();
                    _byClass[p.yLabel] = list;
                    _classColor[p.yLabel] = _palette[_classColor.Count % _palette.Length];
                }
                list.Add(Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * pointScale));
                _meta.Add(new PointMeta { worldPos = pos, label = p.yLabel, sample = p.sample, index = i });
            }
        }

        private void Update()
        {
            if (pointMesh == null || pointMaterial == null) return;
            // Render each class as one batched instanced draw.
            var rp = new RenderParams(pointMaterial) { worldBounds = new Bounds(transform.position, Vector3.one * worldScale * 4f) };
            foreach (var (label, mats) in _byClass)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_BaseColor", _classColor[label]);
                rp.matProps = mpb;
                // Use RenderMeshInstanced for 1023+ instances by chunking.
                const int chunk = 1023;
                for (int i = 0; i < mats.Count; i += chunk)
                {
                    int n = Mathf.Min(chunk, mats.Count - i);
                    Graphics.RenderMeshInstanced(rp, pointMesh, 0, mats, n, i);
                }
            }
        }

        /// <summary>Find the closest point to a world-space ray hit point.</summary>
        public PointMeta FindNearest(Vector3 worldPos, float maxDist = 0.05f)
        {
            float best = maxDist;
            PointMeta hit = null;
            foreach (var m in _meta)
            {
                var d = Vector3.Distance(transform.TransformPoint(m.worldPos), worldPos);
                if (d < best) { best = d; hit = m; }
            }
            return hit;
        }

        public class PointMeta
        {
            public Vector3 worldPos;
            public string label;
            public EdgeImpulseClient.SampleRef sample;
            public int index;
        }
    }
}
