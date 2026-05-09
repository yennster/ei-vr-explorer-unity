using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EI.VR
{
    /// <summary>
    /// Coroutine-based client for the Edge Impulse Studio API. Mirrors the
    /// TypeScript client in web-companion/src/lib/edge-impulse.ts. Always
    /// checks the response `success` field (uniform across the EI API).
    /// </summary>
    public class EdgeImpulseClient
    {
        private const string Studio = "https://studio.edgeimpulse.com/v1";
        private readonly string _apiKey;
        private readonly int _projectId;

        public EdgeImpulseClient(string apiKey, int projectId)
        {
            _apiKey = apiKey;
            _projectId = projectId;
        }

        public IEnumerator GetImpulse(Action<JObject> onSuccess, Action<string> onError)
            => Get($"/api/{_projectId}/impulse", onSuccess, onError);

        public IEnumerator GetFeatureLabels(int dspId, Action<JObject> onSuccess, Action<string> onError)
            => Get($"/api/{_projectId}/dsp/{dspId}/features/labels", onSuccess, onError);

        public IEnumerator GetFeatureImportance(int dspId, Action<JObject> onSuccess, Action<string> onError)
            => Get($"/api/{_projectId}/dsp/{dspId}/features/importance", onSuccess, onError);

        public IEnumerator GetFeatureGraph(int dspId, string category, int ax1, int ax2, int ax3,
            Action<FeatureGraphResponse> onSuccess, Action<string> onError)
        {
            var path = $"/api/{_projectId}/dsp/{dspId}/features/get-graph/{category}" +
                       $"?featureAx1={ax1}&featureAx2={ax2}&featureAx3={ax3}";
            return Get(path, raw =>
            {
                try { onSuccess(raw.ToObject<FeatureGraphResponse>()); }
                catch (Exception e) { onError("Parse error: " + e.Message); }
            }, onError);
        }

        public IEnumerator GetSampleSlice(int sampleId, Action<JObject> onSuccess, Action<string> onError)
            => Get($"/api/{_projectId}/raw-data/{sampleId}/slice", onSuccess, onError);

        private IEnumerator Get(string path, Action<JObject> onSuccess, Action<string> onError)
        {
            using var req = UnityWebRequest.Get(Studio + path);
            req.SetRequestHeader("x-api-key", _apiKey);
            req.SetRequestHeader("Accept", "application/json");
            yield return req.SendWebRequest();
            HandleResponse(req, onSuccess, onError);
        }

        private static void HandleResponse(UnityWebRequest req, Action<JObject> onSuccess, Action<string> onError)
        {
            if (req.result != UnityWebRequest.Result.Success)
            {
                onError($"HTTP {(int)req.responseCode}: {req.error}");
                return;
            }
            try
            {
                var json = JObject.Parse(req.downloadHandler.text);
                if (json["success"]?.Value<bool>() != true)
                {
                    onError(json["error"]?.Value<string>() ?? "Unknown EI error");
                    return;
                }
                onSuccess(json);
            }
            catch (Exception e) { onError("Parse error: " + e.Message); }
        }

        // ------- typed responses (only the ones we need shape-checked) -------

        [Serializable]
        public class FeatureGraphResponse
        {
            public bool success;
            public int totalSampleCount;
            public int skipFirstFeatures;
            public List<FeatureGraphPoint> data;
        }

        [Serializable]
        public class FeatureGraphPoint
        {
            // X is keyed by feature index as string. Newtonsoft handles this.
            public Dictionary<string, double> X;
            public int y;
            public string yLabel;
            public SampleRef sample;
        }

        [Serializable]
        public class SampleRef
        {
            public int id;
            public string name;
            public double startMs;
            public double endMs;
        }
    }
}
