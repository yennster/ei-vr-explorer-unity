using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace EI.VR
{
    /// <summary>
    /// Reads metadata.json from a deploy bundle (the zip produced by the
    /// Edge Impulse → Unity Sentis custom deployment block) and auto-fills
    /// the matching <see cref="LiveInferenceRunner"/> + <see cref="ObjectDetectionRunner"/>
    /// configs so the user doesn't have to copy DSP parameters from EI Studio
    /// into the inspector by hand.
    ///
    /// Bundle format expected:
    ///   <code>
    ///   deploy.zip/
    ///     ├── model.onnx
    ///     ├── metadata.json   ← classes, sensor, frequency, dspBlocks[].parameters
    ///     └── ...
    ///   </code>
    ///
    /// On disk after RetrainController extracts it, metadata.json sits next
    /// to <see cref="AppState.ModelPath"/> (defaults to
    /// <c>persistentDataPath/metadata.json</c>).
    /// </summary>
    public static class BundleMetadataLoader
    {
        public class Bundle
        {
            public string[] Classes;
            public string Sensor;
            public int Frequency;
            public JObject Raw;
            public JArray DspBlocks;
        }

        /// <summary>Try to load metadata.json sitting alongside model.onnx.</summary>
        public static Bundle TryLoadFromDisk(string modelPath)
        {
            if (string.IsNullOrEmpty(modelPath)) return null;
            var dir = Path.GetDirectoryName(modelPath);
            if (string.IsNullOrEmpty(dir)) return null;
            var path = Path.Combine(dir, "metadata.json");
            if (!File.Exists(path)) return null;
            try { return Parse(File.ReadAllText(path)); }
            catch (Exception e) { Debug.LogWarning($"[BundleMetadata] Parse failed: {e.Message}"); return null; }
        }

        /// <summary>
        /// Extract model.onnx + metadata.json from a deploy.zip stream.
        /// Writes both to <paramref name="destDir"/> and returns the parsed bundle.
        /// </summary>
        public static Bundle ExtractZip(byte[] zipBytes, string destDir)
        {
            Directory.CreateDirectory(destDir);
            using var ms = new MemoryStream(zipBytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            Bundle bundle = null;
            foreach (var entry in zip.Entries)
            {
                var name = Path.GetFileName(entry.FullName);
                if (string.IsNullOrEmpty(name)) continue;
                if (name == "model.onnx" || name == "metadata.json")
                {
                    var outPath = Path.Combine(destDir, name);
                    using var src = entry.Open();
                    using var dst = File.Create(outPath);
                    src.CopyTo(dst);
                }
            }
            var metaPath = Path.Combine(destDir, "metadata.json");
            if (File.Exists(metaPath))
            {
                try { bundle = Parse(File.ReadAllText(metaPath)); }
                catch (Exception e) { Debug.LogWarning($"[BundleMetadata] Parse failed: {e.Message}"); }
            }
            return bundle;
        }

        /// <summary>Apply the impulse's DSP block parameters to a runner's configs.</summary>
        public static void Apply(Bundle bundle, LiveInferenceRunner runner)
        {
            if (bundle == null || runner == null) return;
            // Note: runner fields are private SerializeField — set via reflection
            // since this is runtime-only (Editor would use SerializedObject).
            var t = runner.GetType();
            SetField(t, runner, "classNames", bundle.Classes ?? new[] { "?" });
            if (bundle.Frequency > 0) SetField(t, runner, "motionRateHz", bundle.Frequency);
            if (bundle.Frequency > 0) SetField(t, runner, "audioRateHz", bundle.Frequency);

            foreach (var b in EnumerateDspBlocks(bundle))
            {
                var bt = (b["type"]?.Value<string>() ?? "").ToLowerInvariant();
                var p = b["parameters"] as JObject;
                if (p == null) continue;
                if (bt.Contains("spectral")) ApplySpectral(runner, p);
                else if (bt.Contains("mfcc")) ApplyMfcc(runner, p);
                else if (bt.Contains("mfe")) ApplyMfe(runner, p);
            }
        }

        // ---- internals -------------------------------------------------------

        private static Bundle Parse(string json)
        {
            var root = JObject.Parse(json);
            JArray classesArr = root["classes"] as JArray;
            return new Bundle
            {
                Classes = classesArr?.ToObject<string[]>() ?? Array.Empty<string>(),
                Sensor = root["sensor"]?.Value<string>(),
                Frequency = root["frequency"]?.Value<int>() ?? 0,
                DspBlocks = root["dspBlocks"] as JArray,
                Raw = root,
            };
        }

        private static System.Collections.Generic.IEnumerable<JObject> EnumerateDspBlocks(Bundle b)
        {
            if (b.DspBlocks == null) yield break;
            foreach (var t in b.DspBlocks)
                if (t is JObject obj) yield return obj;
        }

        private static void ApplySpectral(LiveInferenceRunner r, JObject p)
        {
            var cfg = GetFieldValue<SpectralAnalysisConfig>(r, "spectralConfig");
            if (cfg == null) return;
            cfg.peakCount = p["spectral-peaks-count"]?.Value<int?>() ?? cfg.peakCount;
            cfg.lowFrequencyHz = p["filter-cutoff"]?.Value<float?>() ?? cfg.lowFrequencyHz;
            // EI's "spectral-power-edges" is a comma-separated string; normalize.
            var edges = p["spectral-power-edges"]?.Value<string>();
            if (!string.IsNullOrEmpty(edges))
            {
                var parts = edges.Split(',');
                var floats = new System.Collections.Generic.List<float>();
                foreach (var s in parts)
                    if (float.TryParse(s.Trim(), out var f)) floats.Add(f);
                if (floats.Count >= 2) cfg.powerEdges = floats.ToArray();
            }
        }

        private static void ApplyMfe(LiveInferenceRunner r, JObject p)
        {
            var cfg = GetFieldValue<MFEConfig>(r, "mfeConfig");
            if (cfg == null) return;
            cfg.frameLengthSec = p["frame_length"]?.Value<float?>() ?? cfg.frameLengthSec;
            cfg.frameStrideSec = p["frame_stride"]?.Value<float?>() ?? cfg.frameStrideSec;
            cfg.numFilters = p["num_filters"]?.Value<int?>() ?? cfg.numFilters;
            cfg.fftSize = p["fft_length"]?.Value<int?>() ?? cfg.fftSize;
            cfg.lowFrequencyHz = p["low_frequency"]?.Value<float?>() ?? cfg.lowFrequencyHz;
            cfg.highFrequencyHz = p["high_frequency"]?.Value<float?>() ?? cfg.highFrequencyHz;
            cfg.preEmphasis = p["pre_cof"]?.Value<float?>() ?? cfg.preEmphasis;
        }

        private static void ApplyMfcc(LiveInferenceRunner r, JObject p)
        {
            var cfg = GetFieldValue<MFCCConfig>(r, "mfccConfig");
            if (cfg == null) return;
            cfg.frameLengthSec = p["frame_length"]?.Value<float?>() ?? cfg.frameLengthSec;
            cfg.frameStrideSec = p["frame_stride"]?.Value<float?>() ?? cfg.frameStrideSec;
            cfg.numFilters = p["num_filters"]?.Value<int?>() ?? cfg.numFilters;
            cfg.numCepstral = p["num_cepstral"]?.Value<int?>() ?? cfg.numCepstral;
            cfg.fftSize = p["fft_length"]?.Value<int?>() ?? cfg.fftSize;
            cfg.lowFrequencyHz = p["low_frequency"]?.Value<float?>() ?? cfg.lowFrequencyHz;
            cfg.highFrequencyHz = p["high_frequency"]?.Value<float?>() ?? cfg.highFrequencyHz;
            cfg.preEmphasis = p["pre_cof"]?.Value<float?>() ?? cfg.preEmphasis;
        }

        private static void SetField(Type t, object obj, string name, object value)
        {
            var f = t.GetField(name, System.Reflection.BindingFlags.Instance |
                                     System.Reflection.BindingFlags.Public |
                                     System.Reflection.BindingFlags.NonPublic);
            if (f != null) f.SetValue(obj, value);
        }

        private static T GetFieldValue<T>(object obj, string name) where T : class
        {
            var f = obj.GetType().GetField(name, System.Reflection.BindingFlags.Instance |
                                                  System.Reflection.BindingFlags.Public |
                                                  System.Reflection.BindingFlags.NonPublic);
            return f?.GetValue(obj) as T;
        }
    }
}
