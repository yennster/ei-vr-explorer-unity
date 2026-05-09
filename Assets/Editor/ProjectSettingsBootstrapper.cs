#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace EI.VR.EditorTools
{
    /// <summary>
    /// One-shot project-settings fixups that run on every Editor load.
    /// Idempotent — only sets values that are still at their default/empty.
    ///
    /// Currently handles:
    ///   • Microphone Usage Description (Unity build-fails without this when
    ///     any compiled script references the Microphone class — which ours
    ///     do, in SampleRecorder + LiveInferenceRunner for audio impulses).
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSettingsBootstrapper
    {
        private const string MicrophoneUsage =
            "Captures IMU and audio samples for Edge Impulse training and on-device inference.";

        static ProjectSettingsBootstrapper()
        {
            // PlayerSettings.iOS.microphoneUsageDescription is the canonical
            // string Unity checks at build time across platforms. Setting it
            // unblocks Android builds even though the field is named iOS.
            if (string.IsNullOrEmpty(PlayerSettings.iOS.microphoneUsageDescription))
            {
                PlayerSettings.iOS.microphoneUsageDescription = MicrophoneUsage;
                Debug.Log("[EI VR] Set Microphone Usage Description in Player Settings.");
            }
        }
    }
}
#endif
