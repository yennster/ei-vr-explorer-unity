using System;
using UnityEngine;

namespace EI.VR
{
    /// <summary>
    /// Process-wide singleton holding the paired Edge Impulse credentials and
    /// the currently loaded TFLite model bundle. Scenes subscribe to ModelChanged
    /// to hot-swap when retraining produces a new artifact.
    /// </summary>
    public sealed class AppState : MonoBehaviour
    {
        public static AppState I { get; private set; }

        public string ApiKey { get; private set; }
        public int ProjectId { get; private set; }
        public string CompanionBaseUrl { get; private set; }

        public string ModelPath { get; private set; }
        public event Action ModelChanged;

        public bool IsPaired => !string.IsNullOrEmpty(ApiKey) && ProjectId > 0;

        private void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            LoadFromStore();
        }

        public void SetCredentials(string apiKey, int projectId, string companionBaseUrl)
        {
            ApiKey = apiKey;
            ProjectId = projectId;
            CompanionBaseUrl = companionBaseUrl?.TrimEnd('/') ?? "";
            SecureStore.Save("ei.apiKey", apiKey);
            SecureStore.Save("ei.projectId", projectId.ToString());
            SecureStore.Save("ei.companion", CompanionBaseUrl);
        }

        public void SetModelPath(string path)
        {
            ModelPath = path;
            ModelChanged?.Invoke();
        }

        public void Clear()
        {
            ApiKey = null; ProjectId = 0; CompanionBaseUrl = null; ModelPath = null;
            SecureStore.Clear();
        }

        private void LoadFromStore()
        {
            ApiKey = SecureStore.Load("ei.apiKey");
            ProjectId = int.TryParse(SecureStore.Load("ei.projectId"), out var p) ? p : 0;
            CompanionBaseUrl = SecureStore.Load("ei.companion") ?? "";
        }
    }
}
