using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace EI.VR
{
    /// <summary>
    /// Setup logic for the Feature Explorer scene: fetches feature labels and
    /// importance, picks top-3 by default for the X/Y/Z axes, and exposes a
    /// SetAxis(axisSlot, featureIndex) API the UI can wire up.
    /// </summary>
    public class AxisPicker : MonoBehaviour
    {
        [SerializeField] private FeatureCloud cloud;
        [SerializeField] private TMPro.TMP_Text statusText;
        [SerializeField] private int dspId; // set in inspector or auto-detected

        public List<string> FeatureLabels { get; private set; } = new();
        public int AxisX { get; private set; }
        public int AxisY { get; private set; } = 1;
        public int AxisZ { get; private set; } = 2;

        private EdgeImpulseClient _ei;

        private IEnumerator Start()
        {
            if (AppState.I == null || !AppState.I.IsPaired) { Set("Not paired"); yield break; }
            _ei = new EdgeImpulseClient(AppState.I.ApiKey, AppState.I.ProjectId);

            // If dspId wasn't set in inspector, grab the first DSP block from the impulse.
            if (dspId == 0)
            {
                yield return _ei.GetImpulse(json =>
                {
                    var first = json["impulse"]?["dspBlocks"]?[0]?["id"];
                    if (first != null) dspId = first.Value<int>();
                }, err => Set("Impulse fetch failed: " + err));
            }
            if (dspId == 0) { Set("No DSP block"); yield break; }

            yield return _ei.GetFeatureLabels(dspId, json =>
            {
                var labels = (JArray)json["labels"];
                if (labels != null) foreach (var t in labels) FeatureLabels.Add(t.Value<string>());
            }, err => Set("Labels fetch failed: " + err));

            yield return _ei.GetFeatureImportance(dspId, json =>
            {
                var arr = (JArray)json["features"];
                if (arr != null && arr.Count >= 3)
                {
                    AxisX = arr[0]["index"].Value<int>();
                    AxisY = arr[1]["index"].Value<int>();
                    AxisZ = arr[2]["index"].Value<int>();
                }
            }, _ => { /* importance is optional */ });

            yield return Reload();
        }

        public IEnumerator SetAxis(int slot, int featureIndex)
        {
            if (slot == 0) AxisX = featureIndex;
            else if (slot == 1) AxisY = featureIndex;
            else AxisZ = featureIndex;
            yield return Reload();
        }

        private IEnumerator Reload()
        {
            Set("Loading…");
            yield return _ei.GetFeatureGraph(dspId, "training", AxisX, AxisY, AxisZ,
                resp => { cloud.SetData(resp, AxisX, AxisY, AxisZ); Set($"{resp.totalSampleCount} windows"); },
                err => Set("Graph fetch failed: " + err));
        }

        private void Set(string s) { if (statusText != null) statusText.text = s; }
    }
}
