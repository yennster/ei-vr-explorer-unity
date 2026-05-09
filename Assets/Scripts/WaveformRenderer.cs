using UnityEngine;
using Newtonsoft.Json.Linq;

namespace EI.VR
{
    /// <summary>
    /// Renders a multi-axis time-series sample as a stack of LineRenderers in
    /// world space. Driven by the response from /raw-data/{sampleId}/slice.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class WaveformRenderer : MonoBehaviour
    {
        [SerializeField] private float width = 0.4f;
        [SerializeField] private float heightPerAxis = 0.06f;
        [SerializeField] private TMPro.TMP_Text label;

        public void SetFromSliceJson(JObject json, string classLabel, string sampleName)
        {
            // Expected shape: { data: { values: number[][], sensors: [{name,units}] } }
            var values = (JArray)json["data"]?["values"];
            var sensors = (JArray)json["data"]?["sensors"];
            if (values == null || values.Count == 0) return;

            int n = values.Count;
            int axes = ((JArray)values[0]).Count;

            // Clear any old child line renderers.
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);

            for (int a = 0; a < axes; a++)
            {
                var go = new GameObject($"axis_{a}");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.widthMultiplier = 0.003f;
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = lr.endColor = Color.HSVToRGB((a * 0.18f) % 1f, 0.6f, 1f);
                lr.positionCount = n;

                // Normalize per-axis to fit heightPerAxis.
                float min = float.MaxValue, max = float.MinValue;
                for (int i = 0; i < n; i++)
                {
                    float v = ((JArray)values[i])[a].Value<float>();
                    if (v < min) min = v; if (v > max) max = v;
                }
                float range = Mathf.Max(1e-6f, max - min);
                float yOff = (axes - 1 - a) * heightPerAxis;

                for (int i = 0; i < n; i++)
                {
                    float v = ((JArray)values[i])[a].Value<float>();
                    float xN = (i / (float)(n - 1)) * width - width * 0.5f;
                    float yN = ((v - min) / range - 0.5f) * heightPerAxis + yOff;
                    lr.SetPosition(i, new Vector3(xN, yN, 0));
                }
            }

            if (label != null) label.text = $"{classLabel}\n{sampleName}";
        }
    }
}
