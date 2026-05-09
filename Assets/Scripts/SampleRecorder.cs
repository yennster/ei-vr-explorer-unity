using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace EI.VR
{
    public enum RecordingSensor
    {
        LeftControllerIMU,
        RightControllerIMU,
        HeadsetIMU,
        Microphone,
    }

    /// <summary>
    /// Captures a fixed-length window of IMU (or mic) samples from the chosen
    /// Quest source. Output is a Recording object with sensor metadata and a
    /// values matrix shaped [N_samples][N_axes], ready for IngestionUploader.
    /// </summary>
    public class SampleRecorder : MonoBehaviour
    {
        [SerializeField] private int sampleRateHz = 62; // typical EI motion default
        [SerializeField] private int lengthMs = 2000;

        public bool IsRecording { get; private set; }
        public float Progress01 { get; private set; }

        private float _accum;
        private int _samplesNeeded;
        private RecordingSensor _sensor;
        private List<float[]> _values;
        private CompanionClient.Sensor[] _sensorMeta;

        public Recording LatestResult { get; private set; }

        public void Begin(RecordingSensor sensor)
        {
            if (IsRecording) return;
            _sensor = sensor;
            _samplesNeeded = Mathf.RoundToInt(sampleRateHz * (lengthMs / 1000f));
            _values = new List<float[]>(_samplesNeeded);
            _accum = 0f;
            Progress01 = 0f;
            _sensorMeta = SensorMetaFor(sensor);
            IsRecording = true;
        }

        private void Update()
        {
            if (!IsRecording) return;
            float dt = 1f / sampleRateHz;
            _accum += Time.deltaTime;
            while (_accum >= dt && _values.Count < _samplesNeeded)
            {
                _values.Add(ReadSample(_sensor));
                _accum -= dt;
            }
            Progress01 = _values.Count / (float)_samplesNeeded;
            if (_values.Count >= _samplesNeeded)
            {
                IsRecording = false;
                LatestResult = new Recording
                {
                    sensors = _sensorMeta,
                    values = _values.ToArray(),
                    intervalMs = 1000f / sampleRateHz,
                };
            }
        }

        private static float[] ReadSample(RecordingSensor s)
        {
            // For simplicity we read acceleration + angular velocity for IMU
            // sensors. Mic recording is handled separately via Unity's
            // Microphone API and is left as a stub here.
            switch (s)
            {
                case RecordingSensor.LeftControllerIMU:
                    return ReadIMU(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller);
                case RecordingSensor.RightControllerIMU:
                    return ReadIMU(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller);
                case RecordingSensor.HeadsetIMU:
                    return ReadIMU(InputDeviceCharacteristics.HeadMounted);
                default:
                    return new float[] { 0, 0, 0 };
            }
        }

        private static float[] ReadIMU(InputDeviceCharacteristics filter)
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(filter, devices);
            if (devices.Count == 0) return new float[] { 0, 0, 0, 0, 0, 0 };
            var d = devices[0];
            d.TryGetFeatureValue(CommonUsages.deviceAcceleration, out var a);
            d.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out var w);
            return new[] { a.x, a.y, a.z, w.x, w.y, w.z };
        }

        private static CompanionClient.Sensor[] SensorMetaFor(RecordingSensor s)
        {
            if (s == RecordingSensor.Microphone)
            {
                return new[] { new CompanionClient.Sensor { name = "audio", units = "wav" } };
            }
            return new[]
            {
                new CompanionClient.Sensor { name = "accX", units = "m/s2" },
                new CompanionClient.Sensor { name = "accY", units = "m/s2" },
                new CompanionClient.Sensor { name = "accZ", units = "m/s2" },
                new CompanionClient.Sensor { name = "gyrX", units = "rad/s" },
                new CompanionClient.Sensor { name = "gyrY", units = "rad/s" },
                new CompanionClient.Sensor { name = "gyrZ", units = "rad/s" },
            };
        }

        public class Recording
        {
            public CompanionClient.Sensor[] sensors;
            public float[][] values;
            public float intervalMs;
        }
    }
}
