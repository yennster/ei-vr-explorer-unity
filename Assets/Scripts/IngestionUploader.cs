using System.Collections;
using UnityEngine;

namespace EI.VR
{
    /// <summary>
    /// Builds an IngestPayload from a SampleRecorder.Recording and POSTs it to
    /// the companion app's /api/ingest endpoint. Wraps the long-running coroutine
    /// in something a button can fire.
    /// </summary>
    public class IngestionUploader : MonoBehaviour
    {
        [SerializeField] private SampleRecorder recorder;
        [SerializeField] private TMPro.TMP_Text statusText;

        public string Label;
        public string Category = "training"; // training | testing
        public RecordingSensor SensorChoice = RecordingSensor.RightControllerIMU;

        public void Upload()
        {
            if (recorder?.LatestResult == null) { Set("No recording"); return; }
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            var c = new CompanionClient(AppState.I.CompanionBaseUrl, AppState.I.ApiKey);
            var rec = recorder.LatestResult;
            var payload = new CompanionClient.IngestPayload
            {
                category = Category,
                label = Label,
                fileName = $"quest-{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.json",
                deviceName = SystemInfo.deviceUniqueIdentifier.Substring(0, 12),
                deviceType = "QUEST_2",
                intervalMs = Mathf.RoundToInt(rec.intervalMs),
                sensors = rec.sensors,
                values = rec.values,
            };
            Set("Uploading…");
            yield return c.UploadSample(payload, () => Set($"Uploaded as {Label}"), err => Set("Failed: " + err));
        }

        private void Set(string s) { if (statusText != null) statusText.text = s; }
    }
}
