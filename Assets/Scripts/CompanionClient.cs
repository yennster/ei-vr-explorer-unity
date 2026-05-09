using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EI.VR
{
    /// <summary>
    /// Talks to the Vercel-hosted Next.js companion app for things that are
    /// awkward client-side: pairing pickup, model-bundle build/poll, ingest
    /// proxy, retrain trigger.
    /// </summary>
    public class CompanionClient
    {
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public CompanionClient(string baseUrl, string apiKey)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey;
        }

        // GET {base}/api/pair?code=NNNNNN
        public IEnumerator FetchPairing(string code, Action<string, int> onSuccess, Action<string> onError)
        {
            using var req = UnityWebRequest.Get($"{_baseUrl}/api/pair?code={code}");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { onError(req.error); yield break; }
            try
            {
                var json = JObject.Parse(req.downloadHandler.text);
                onSuccess(json["apiKey"].Value<string>(), json["projectId"].Value<int>());
            }
            catch (Exception e) { onError(e.Message); }
        }

        // GET {base}/api/model-bundle/{projectId}
        // Returns the ONNX model bytes directly (companion server-side fetches
        // the EI TFLite deploy, extracts, converts via tflite2onnx, streams).
        public IEnumerator FetchModelBundle(int projectId, Action<byte[]> onModelBytes, Action<string> onError)
        {
            using var req = UnityWebRequest.Get($"{_baseUrl}/api/model-bundle/{projectId}");
            req.SetRequestHeader("x-api-key", _apiKey);
            req.timeout = 300; // EI build + conversion can take a few minutes
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                // The companion responds with JSON on error; surface its message.
                var body = req.downloadHandler != null ? req.downloadHandler.text : null;
                if (!string.IsNullOrEmpty(body))
                {
                    try
                    {
                        var json = JObject.Parse(body);
                        var msg = json["error"]?.Value<string>();
                        if (!string.IsNullOrEmpty(msg)) { onError(msg); yield break; }
                    }
                    catch { /* fall through */ }
                }
                onError($"HTTP {req.responseCode}: {req.error}");
                yield break;
            }
            var bytes = req.downloadHandler.data;
            if (bytes == null || bytes.Length < 16) { onError("Empty model response"); yield break; }
            onModelBytes(bytes);
        }

        // POST {base}/api/ingest
        public IEnumerator UploadSample(IngestPayload payload, Action onSuccess, Action<string> onError)
        {
            var json = JsonConvert.SerializeObject(payload);
            using var req = new UnityWebRequest($"{_baseUrl}/api/ingest", "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("x-api-key", _apiKey);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                onError($"Ingest failed ({req.responseCode}): {req.downloadHandler.text}");
                yield break;
            }
            onSuccess();
        }

        // POST {base}/api/retrain/{projectId}
        public IEnumerator StartRetrain(int projectId, Action<int> onJobId, Action<string> onError)
        {
            using var req = new UnityWebRequest($"{_baseUrl}/api/retrain/{projectId}", "POST")
            {
                uploadHandler = new UploadHandlerRaw(new byte[0]),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            req.SetRequestHeader("x-api-key", _apiKey);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { onError(req.error); yield break; }
            try
            {
                var json = JObject.Parse(req.downloadHandler.text);
                onJobId(json["jobId"].Value<int>());
            }
            catch (Exception e) { onError(e.Message); }
        }

        // GET {base}/api/retrain/{projectId}?jobId=N
        public IEnumerator PollRetrain(int projectId, int jobId, Action<bool, bool, string> onStatus, Action<string> onError)
        {
            using var req = UnityWebRequest.Get($"{_baseUrl}/api/retrain/{projectId}?jobId={jobId}");
            req.SetRequestHeader("x-api-key", _apiKey);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { onError(req.error); yield break; }
            try
            {
                var json = JObject.Parse(req.downloadHandler.text);
                var job = json["job"];
                bool finished = job["finished"].Value<bool>();
                bool ok = job["finishedSuccessful"]?.Value<bool>() ?? false;
                string tail = json["stdoutTail"]?.Value<string>();
                onStatus(finished, ok, tail);
            }
            catch (Exception e) { onError(e.Message); }
        }

        [Serializable]
        public class IngestPayload
        {
            public string category;
            public string label;
            public string fileName;
            public string deviceName;
            public string deviceType;
            public int intervalMs;
            public Sensor[] sensors;
            public float[][] values;
        }

        [Serializable]
        public class Sensor
        {
            public string name;
            public string units;
        }
    }
}
