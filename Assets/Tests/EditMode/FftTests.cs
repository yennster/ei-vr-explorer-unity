using NUnit.Framework;
using UnityEngine;

namespace EI.VR.Tests
{
    public class FftTests
    {
        [Test]
        public void NextPow2_RoundsUpCorrectly()
        {
            Assert.AreEqual(1, Fft.NextPow2(1));
            Assert.AreEqual(2, Fft.NextPow2(2));
            Assert.AreEqual(4, Fft.NextPow2(3));
            Assert.AreEqual(16, Fft.NextPow2(13));
            Assert.AreEqual(64, Fft.NextPow2(64));
            Assert.AreEqual(128, Fft.NextPow2(65));
            Assert.AreEqual(1024, Fft.NextPow2(1000));
        }

        [Test]
        public void Magnitude_PureToneProducesPeakAtExpectedBin()
        {
            // Sample rate 1000 Hz, FFT size 256, sine at 100 Hz.
            // Expected peak bin: 100 * 256 / 1000 = ~25.6 → bin 26.
            const int N = 256;
            const float sampleRate = 1000f;
            const float toneHz = 100f;
            var input = new float[N];
            for (int i = 0; i < N; i++)
            {
                input[i] = Mathf.Sin(2f * Mathf.PI * toneHz * i / sampleRate);
            }

            float[] mag = Fft.Magnitude(input, N);
            int expectedBin = Mathf.RoundToInt(toneHz * N / sampleRate);
            int peakBin = ArgMax(mag);

            Assert.That(System.Math.Abs(peakBin - expectedBin), Is.LessThanOrEqualTo(1),
                $"peak at bin {peakBin}, expected ~{expectedBin}");
        }

        [Test]
        public void Magnitude_OutputLengthIsHalfPlusOne()
        {
            const int N = 64;
            var input = new float[32];
            float[] mag = Fft.Magnitude(input, N);
            Assert.AreEqual(N / 2 + 1, mag.Length);
        }

        [Test]
        public void Magnitude_RejectsNonPowerOfTwo()
        {
            var input = new float[100];
            Assert.Throws<System.ArgumentException>(() => Fft.Magnitude(input, 100));
        }

        [Test]
        public void Magnitude_DcInputProducesPeakAtBinZero()
        {
            const int N = 64;
            var input = new float[N];
            for (int i = 0; i < N; i++) input[i] = 1.0f;
            float[] mag = Fft.Magnitude(input, N);
            Assert.AreEqual(0, ArgMax(mag));
        }

        private static int ArgMax(float[] arr)
        {
            int best = 0;
            float bestVal = float.NegativeInfinity;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] > bestVal) { bestVal = arr[i]; best = i; }
            return best;
        }
    }
}
