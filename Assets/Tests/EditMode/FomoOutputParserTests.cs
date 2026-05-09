using NUnit.Framework;

namespace EI.VR.Tests
{
    public class FomoOutputParserTests
    {
        // Builds a [gridH, gridW, channels] flat array indexed as
        // grid[(y*gridW + x) * channels + c].
        private static float[] MakeGrid(int gridH, int gridW, int channels)
        {
            return new float[gridH * gridW * channels];
        }

        private static void Set(float[] grid, int gridW, int channels, int x, int y, int c, float v)
        {
            grid[(y * gridW + x) * channels + c] = v;
        }

        [Test]
        public void Parse_EmptyGridProducesNoDetections()
        {
            int gridH = 8, gridW = 8, channels = 3; // 1 background + 2 classes
            var grid = MakeGrid(gridH, gridW, channels);
            var hits = FomoOutputParser.Parse(grid, gridH, gridW, channels);
            Assert.IsEmpty(hits);
        }

        [Test]
        public void Parse_SingleActiveCellProducesOneDetection()
        {
            int gridH = 8, gridW = 8, channels = 3;
            var grid = MakeGrid(gridH, gridW, channels);
            // Activate channel 1 at (3, 4) above threshold.
            Set(grid, gridW, channels, x: 3, y: 4, c: 1, v: 0.9f);

            var hits = FomoOutputParser.Parse(grid, gridH, gridW, channels, threshold: 0.5f);
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(1, hits[0].classIndex);
            Assert.AreEqual(0.9f, hits[0].score, 1e-5f);
        }

        [Test]
        public void Parse_AdjacentActiveCellsMergeIntoOneBox()
        {
            int gridH = 8, gridW = 8, channels = 3;
            var grid = MakeGrid(gridH, gridW, channels);
            // Three adjacent cells of class 2.
            Set(grid, gridW, channels, x: 2, y: 2, c: 2, v: 0.7f);
            Set(grid, gridW, channels, x: 3, y: 2, c: 2, v: 0.8f);
            Set(grid, gridW, channels, x: 3, y: 3, c: 2, v: 0.6f);

            var hits = FomoOutputParser.Parse(grid, gridH, gridW, channels);
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(2, hits[0].classIndex);
            // Bounding rect should cover x:2-3, y:2-3 normalised.
            Assert.That(hits[0].rect.x, Is.EqualTo(2f / 8).Within(1e-5f));
            Assert.That(hits[0].rect.y, Is.EqualTo(2f / 8).Within(1e-5f));
            Assert.That(hits[0].rect.width, Is.EqualTo(2f / 8).Within(1e-5f));
        }

        [Test]
        public void Parse_DifferentClassesDoNotMerge()
        {
            int gridH = 4, gridW = 4, channels = 3;
            var grid = MakeGrid(gridH, gridW, channels);
            // Adjacent cells but different non-background classes.
            Set(grid, gridW, channels, x: 1, y: 1, c: 1, v: 0.9f);
            Set(grid, gridW, channels, x: 2, y: 1, c: 2, v: 0.9f);

            var hits = FomoOutputParser.Parse(grid, gridH, gridW, channels);
            Assert.AreEqual(2, hits.Count);
        }

        [Test]
        public void Parse_BelowThresholdProducesNoDetection()
        {
            int gridH = 4, gridW = 4, channels = 2;
            var grid = MakeGrid(gridH, gridW, channels);
            Set(grid, gridW, channels, x: 1, y: 1, c: 1, v: 0.3f);

            var hits = FomoOutputParser.Parse(grid, gridH, gridW, channels, threshold: 0.5f);
            Assert.IsEmpty(hits);
        }
    }
}
