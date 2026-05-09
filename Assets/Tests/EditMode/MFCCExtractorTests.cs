using NUnit.Framework;
using UnityEngine;

namespace EI.VR.Tests
{
    public class MFCCExtractorTests
    {
        private static MFCCConfig DefaultConfig() => new MFCCConfig
        {
            sampleRateHz = 16000,
            frameLengthSec = 0.025f,
            frameStrideSec = 0.010f,
            numFilters = 32,
            numCepstral = 13,
            fftSize = 512,
            lowFrequencyHz = 0f,
            highFrequencyHz = 0f,
            preEmphasis = 0.97f,
            floor = 1e-10f,
        };

        [Test]
        public void FrameCount_MatchesManualCalculation()
        {
            var cfg = DefaultConfig();
            int audioSamples = 16000; // 1 second at 16 kHz
            int frameLen = Mathf.RoundToInt(cfg.sampleRateHz * cfg.frameLengthSec);   // 400
            int frameStride = Mathf.RoundToInt(cfg.sampleRateHz * cfg.frameStrideSec); // 160
            int expected = 1 + (audioSamples - frameLen) / frameStride;               // ≈ 98

            Assert.AreEqual(expected, MFCCExtractor.FrameCount(audioSamples, cfg));
        }

        [Test]
        public void FrameCount_ReturnsZeroForTooShortAudio()
        {
            var cfg = DefaultConfig();
            // Less than one frame's worth of samples.
            int tooShort = 100;
            Assert.AreEqual(0, MFCCExtractor.FrameCount(tooShort, cfg));
        }

        [Test]
        public void Extract_ReturnsExpectedShape()
        {
            var cfg = DefaultConfig();
            int audioSamples = 16000;
            var audio = new float[audioSamples];
            // Fill with low-amplitude noise so floor doesn't dominate.
            var rng = new System.Random(42);
            for (int i = 0; i < audioSamples; i++) audio[i] = (float)(rng.NextDouble() * 2 - 1) * 0.01f;

            float[] features = MFCCExtractor.Extract(audio, cfg);
            int frames = MFCCExtractor.FrameCount(audioSamples, cfg);
            Assert.AreEqual(frames * cfg.numCepstral, features.Length);
        }

        [Test]
        public void Extract_SilenceProducesFlatLogFloor()
        {
            var cfg = DefaultConfig();
            var silence = new float[16000];
            float[] features = MFCCExtractor.Extract(silence, cfg);
            // Log floor = log(1e-10) ≈ -23.03. Coefficients computed from a flat
            // input should be very small (close to zero) except c[0] which captures
            // the energy and is large negative.
            Assert.That(features.Length, Is.GreaterThan(0));
            // The first cepstral coefficient should be very negative (log of a tiny floor).
            Assert.That(features[0], Is.LessThan(-5f));
        }
    }
}
