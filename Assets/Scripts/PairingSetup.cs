using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EI.VR
{
    /// <summary>
    /// Setup scene controller. Two ways to pair:
    ///   (a) Type a 6-digit code from the companion site into a virtual keyboard.
    ///   (b) Scan a QR (left as a TODO — needs a barcode lib like ZXing.Net for Unity).
    /// </summary>
    public class PairingSetup : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_InputField codeInput;
        [SerializeField] private TMPro.TMP_InputField companionUrlInput; // pre-filled from a default
        [SerializeField] private TMPro.TMP_Text statusText;
        [SerializeField] private string nextScene = "Explorer";

        private const string DefaultCompanion = "https://your-companion.vercel.app";

        private void Start()
        {
            if (AppState.I != null && AppState.I.IsPaired) { LoadNext(); return; }
            if (companionUrlInput != null && string.IsNullOrEmpty(companionUrlInput.text))
                companionUrlInput.text = DefaultCompanion;
        }

        public void OnPairButton() => StartCoroutine(Pair());

        private IEnumerator Pair()
        {
            string code = codeInput?.text?.Trim();
            string baseUrl = companionUrlInput?.text?.Trim() ?? DefaultCompanion;
            if (string.IsNullOrEmpty(code)) { Set("Enter code"); yield break; }

            var c = new CompanionClient(baseUrl, "");
            string apiKey = null; int projectId = 0;
            yield return c.FetchPairing(code,
                (k, p) => { apiKey = k; projectId = p; },
                err => Set("Pairing failed: " + err));
            if (string.IsNullOrEmpty(apiKey)) yield break;

            AppState.I.SetCredentials(apiKey, projectId, baseUrl);
            Set("Paired");
            LoadNext();
        }

        private void LoadNext()
        {
            if (!string.IsNullOrEmpty(nextScene)) SceneManager.LoadScene(nextScene);
        }

        private void Set(string s) { if (statusText != null) statusText.text = s; }
    }
}
