using System.IO;

using UnityEngine;
using UnityEngine.UI;

namespace EI.VR
{
    /// <summary>
    /// Object-detection demo (Quest 2 — synthetic camera).
    ///
    /// Quest 2 doesn't expose its passthrough cameras to apps, so we render a
    /// synthetic 3D scene through a virtual Unity Camera into a RenderTexture,
    /// feed that to a Sentis worker running an Edge Impulse FOMO ONNX, and
    /// overlay bounding boxes on the viewport quad in the user's view.
    ///
    /// On Quest 3 / 3S the same architecture works once the Camera Access API
    /// replaces sourceCamera with the real passthrough camera frames.
    /// </summary>
    public class ObjectDetectionRunner : MonoBehaviour
    {
        [Header("Scene wiring")]
        [SerializeField] private Camera sourceCamera;       // virtual camera viewing the demo scene
        [SerializeField] private RawImage feedDisplay;      // viewport RawImage shown to the user
        [SerializeField] private BoundingBoxOverlay overlay;

        [Header("Model")]
        [SerializeField] private int inputSize = 96;        // FOMO default; MUST match your impulse
        [SerializeField] private int strideMs = 200;        // run inference every N ms
        [SerializeField] private float confidenceThreshold = 0.5f;
        [SerializeField] private bool isNCHW = false;       // EI FOMO ONNX is typically NHWC; flip if your export differs

        private RenderTexture _rt;
        private Texture2D _readback;
        private Unity.InferenceEngine.Model _model;
        private Unity.InferenceEngine.Worker _worker;
        private float _accumMs;

        private void Start()
        {
            _rt = new RenderTexture(inputSize, inputSize, 16, RenderTextureFormat.ARGB32);
            _readback = new Texture2D(inputSize, inputSize, TextureFormat.RGB24, false);
            if (sourceCamera != null) sourceCamera.targetTexture = _rt;
            if (feedDisplay != null) feedDisplay.texture = _rt;

            if (AppState.I != null)
            {
                AppState.I.ModelChanged += LoadModel;
                LoadModel();
            }
        }

        private void OnDestroy()
        {
            if (AppState.I != null) AppState.I.ModelChanged -= LoadModel;
            _worker?.Dispose();
            if (_rt != null) _rt.Release();
            if (_readback != null) Destroy(_readback);
        }

        private void LoadModel()
        {
            if (string.IsNullOrEmpty(AppState.I?.ModelPath) || !File.Exists(AppState.I.ModelPath)) return;
            try
            {
                using var stream = new FileStream(AppState.I.ModelPath, FileMode.Open, FileAccess.Read);
                _model = Unity.InferenceEngine.ModelLoader.Load(stream);
                _worker?.Dispose();
                _worker = new Unity.InferenceEngine.Worker(_model, Unity.InferenceEngine.BackendType.GPUCompute);
                Debug.Log($"[ObjDet] Sentis model loaded from {AppState.I.ModelPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ObjDet] LoadModel failed: {e}");
            }
        }

        private void Update()
        {
            if (_worker == null) return;
            _accumMs += Time.deltaTime * 1000f;
            if (_accumMs < strideMs) return;
            _accumMs = 0f;
            RunOnce();
        }

        private void RunOnce()
        {
            // Pull camera RT into CPU texture.
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            _readback.ReadPixels(new Rect(0, 0, inputSize, inputSize), 0, 0);
            _readback.Apply();
            RenderTexture.active = prev;

            // Pack pixels into a normalised float[] tensor.
            var pixels = _readback.GetPixels32();
            int n = pixels.Length;
            var data = new float[n * 3];
            if (isNCHW)
            {
                int plane = n;
                for (int i = 0; i < n; i++)
                {
                    var p = pixels[i];
                    data[i + 0 * plane] = p.r / 255f;
                    data[i + 1 * plane] = p.g / 255f;
                    data[i + 2 * plane] = p.b / 255f;
                }
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    var p = pixels[i];
                    data[i * 3 + 0] = p.r / 255f;
                    data[i * 3 + 1] = p.g / 255f;
                    data[i * 3 + 2] = p.b / 255f;
                }
            }
            var shape = isNCHW
                ? new Unity.InferenceEngine.TensorShape(1, 3, inputSize, inputSize)
                : new Unity.InferenceEngine.TensorShape(1, inputSize, inputSize, 3);
            using var input = new Unity.InferenceEngine.Tensor<float>(shape, data);
            _worker.Schedule(input);

            using var output = (_worker.PeekOutput() as Unity.InferenceEngine.Tensor<float>).ReadbackAndClone();
            var raw = output.DownloadToArray();

            // FOMO output is (1, gridH, gridW, channels). Sentis returns the
            // dims in the tensor shape; pull them out.
            var outShape = output.shape;
            int gridH, gridW, channels;
            if (outShape.rank == 4)
            {
                gridH = outShape[1];
                gridW = outShape[2];
                channels = outShape[3];
            }
            else if (outShape.rank == 3)
            {
                gridH = outShape[0];
                gridW = outShape[1];
                channels = outShape[2];
            }
            else
            {
                Debug.LogWarning($"[ObjDet] Unexpected FOMO output rank {outShape.rank}");
                return;
            }

            var detections = FomoOutputParser.Parse(raw, gridH, gridW, channels, confidenceThreshold);
            overlay?.Render(detections);
        }
    }
}
