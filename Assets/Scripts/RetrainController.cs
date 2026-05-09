using System.Collections;
using UnityEngine;

namespace EI.VR
{
    /// <summary>
    /// Drives the Collect & Retrain scene's "Retrain & Redeploy" button:
    ///  1. POST /api/retrain → jobId
    ///  2. Poll status every 5s, surface stdout tail to a TMP_Text HUD
    ///  3. On success, POST model-bundle build/poll
    ///  4. Download the new TFLite into persistentDataPath, set AppState.ModelPath
    ///     so LiveInferenceRunner hot-swaps it.
    /// </summary>
    public class RetrainController : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text statusText;
        [SerializeField] private TMPro.TMP_Text logText;

        public void OnRetrainButton() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            if (AppState.I == null || !AppState.I.IsPaired) { Set("Not paired"); yield break; }
            var c = new CompanionClient(AppState.I.CompanionBaseUrl, AppState.I.ApiKey);

            int jobId = 0;
            yield return c.StartRetrain(AppState.I.ProjectId, id => jobId = id, err => Set("Train start failed: " + err));
            if (jobId == 0) yield break;
            Set($"Training job #{jobId} started");

            bool finished = false, ok = false;
            string tail = "";
            while (!finished)
            {
                yield return new WaitForSeconds(5f);
                yield return c.PollRetrain(AppState.I.ProjectId, jobId,
                    (f, s, t) => { finished = f; ok = s; tail = t; },
                    err => Set("Poll failed: " + err));
                if (logText != null && !string.IsNullOrEmpty(tail)) logText.text = tail;
                Set(finished ? (ok ? "Training done — building TFLite…" : "Training failed") : "Training…");
                if (finished && !ok) yield break;
            }

            byte[] modelBytes = null;
            yield return c.FetchModelBundle(AppState.I.ProjectId,
                b => modelBytes = b, err => Set("Model fetch failed: " + err));
            if (modelBytes == null) yield break;

            var path = System.IO.Path.Combine(Application.persistentDataPath, "model.onnx");
            System.IO.File.WriteAllBytes(path, modelBytes);
            AppState.I.SetModelPath(path);
            Set("New model installed — switch to Live Inference scene");
        }

        private void Set(string s) { if (statusText != null) statusText.text = s; }
    }
}
