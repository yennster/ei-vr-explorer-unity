using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EI.VR
{
    /// <summary>
    /// Casts a controller ray into the FeatureCloud; on trigger press finds the
    /// nearest point and fetches its source raw sample to render as a floating
    /// waveform next to the picked point.
    ///
    /// Uses the new Input System with a serialized InputAction so the binding
    /// is reconfigurable from the inspector. Default binding fires on the
    /// right XR controller's trigger.
    /// </summary>
    public class SamplePicker : MonoBehaviour
    {
        [SerializeField] private FeatureCloud cloud;
        [SerializeField] private Transform rayOrigin; // controller transform
        [SerializeField] private GameObject waveformPrefab; // empty object with WaveformRenderer
        [SerializeField] private LayerMask cloudLayer = ~0;

        [SerializeField]
        private InputAction pickAction = new InputAction(
            name: "PickPoint",
            type: InputActionType.Button,
            binding: "<XRController>{RightHand}/triggerPressed");

        private EdgeImpulseClient _ei;
        private GameObject _activeWaveform;

        private void OnEnable() => pickAction.Enable();
        private void OnDisable() => pickAction.Disable();

        private void Start()
        {
            if (AppState.I != null && AppState.I.IsPaired)
                _ei = new EdgeImpulseClient(AppState.I.ApiKey, AppState.I.ProjectId);
        }

        private void Update()
        {
            if (pickAction.WasPressedThisFrame()
                && _ei != null && rayOrigin != null && cloud != null)
            {
                if (Physics.Raycast(rayOrigin.position, rayOrigin.forward, out var hit, 10f, cloudLayer))
                {
                    var meta = cloud.FindNearest(hit.point);
                    if (meta != null) StartCoroutine(LoadAndShow(meta, hit.point));
                }
            }
        }

        private IEnumerator LoadAndShow(FeatureCloud.PointMeta meta, Vector3 worldPos)
        {
            yield return _ei.GetSampleSlice(meta.sample.id, json =>
            {
                if (_activeWaveform != null) Destroy(_activeWaveform);
                _activeWaveform = Instantiate(waveformPrefab, worldPos + Vector3.up * 0.15f, Quaternion.identity);
                var w = _activeWaveform.GetComponent<WaveformRenderer>();
                if (w != null) w.SetFromSliceJson(json, meta.label, meta.sample.name);
            }, err => Debug.LogWarning("Slice fetch failed: " + err));
        }
    }
}
