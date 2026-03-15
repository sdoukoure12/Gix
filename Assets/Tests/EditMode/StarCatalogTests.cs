using System;
using NUnit.Framework;
using Gix.Core;
using Gix.Data;

namespace Gix.Tests.EditMode
{
    /// <summary>
    /// Unit tests for <see cref="StarCatalog"/>.
    /// </summary>
    [TestFixture]
    public class StarCatalogTests
    {
        private static StarData[] BuildTestCatalog()
        {
            return new[]
            {
                new StarData { HipId = 32349, Name = "Sirius",  Magnitude = -1.46f, RightAscension = 101.29f, Declination = -16.72f, SpectralType = "A1V", DistanceLY = 8.6f,   ColorIndex = 0.0f  },
                new StarData { HipId = 30438, Name = "Canopus", Magnitude =  -0.74f, RightAscension = 95.99f,  Declination = -52.70f, SpectralType = "A9II", DistanceLY = 313f,  ColorIndex = 0.15f },
                new StarData { HipId = 69673, Name = "Arcturus",Magnitude =  -0.05f, RightAscension = 213.92f, Declination =  19.18f, SpectralType = "K1.5IIIFe-0.5", DistanceLY = 37f, ColorIndex = 1.23f },
                new StarData { HipId =  9884, Name = "",         Magnitude = 4.5f,   RightAscension =  30.0f,  Declination =  10.0f,  SpectralType = "G5",  DistanceLY = 100f,  ColorIndex = 0.7f  },
                new StarData { HipId = 11111, Name = "",         Magnitude = 7.2f,   RightAscension =  60.0f,  Declination = -30.0f,  SpectralType = "M3",  DistanceLY = 200f,  ColorIndex = 1.5f  },
            };
        }

        // ------------------------------------------------------------------ constructor

        [Test]
        public void Constructor_NullArray_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new StarCatalog(null));
        }

        [Test]
        public void Constructor_ValidArray_SetsCount()
        {
            var catalog = new StarCatalog(BuildTestCatalog());
            Assert.AreEqual(5, catalog.Count);
        }

        // ------------------------------------------------------------------ LOD partitioning

        [Test]
        public void BrightStarCount_MatchesMagnitudeLessThan3()
        {
            var catalog = new StarCatalog(BuildTestCatalog());
            // Sirius (-1.46), Canopus (-0.74), Arcturus (-0.05) all < 3
            Assert.AreEqual(3, catalog.BrightStarCount);
        }

        [Test]
        public void MediumStarCount_MatchesMagnitude3To6()
        {
            var catalog = new StarCatalog(BuildTestCatalog());
            // HIP 9884 has magnitude 4.5 (between 3 and 6)
            Assert.AreEqual(1, catalog.MediumStarCount);
        }

        [Test]
        public void GetLOD0Indices_ContainsBrightStars()
        {
            var catalog = new StarCatalog(BuildTestCatalog());
            var indices = catalog.GetLOD0Indices();
            Assert.AreEqual(3, indices.Length);
        }

        [Test]
        public void GetLOD1Indices_ContainsMediumStars()
        {
            var catalog = new StarCatalog(BuildTestCatalog());
            var indices = catalog.GetLOD1Indices();
            Assert.AreEqual(1, indices.Length);
        }

        // ------------------------------------------------------------------ lookup

        [Test]
        public void TryGetByHip_ExistingHip_ReturnsTrue()
        {
            var catalog = new StarCatalog(BuildTestCatalog());
            Assert.IsTrue(catalog.TryGetByHip(32349, out var star));
            Assert.AreEqual("Sirius", star.Name);
        }

        [Test]
        public void TryGetByHip_NonExistingHip_ReturnsFalse()
        {
            var catalog = new StarCatalog(BuildTestCatalog());
            Assert.IsFalse(catalog.TryGetByHip(99999, out _));
        }

        // ------------------------------------------------------------------ pre-computation

        [Test]
        public void PrecomputePositions_DoesNotThrow()
        {
            var catalog = new StarCatalog(BuildTestCatalog());
            Assert.DoesNotThrow(() =>
                catalog.PrecomputePositions(48.85, 2.35, DateTime.UtcNow));
        }

        [Test]
        public void GetPrecomputedCoord_BeforePrecompute_ThrowsInvalidOperationException()
        {
            var catalog = new StarCatalog(BuildTestCatalog());
            Assert.Throws<InvalidOperationException>(() => catalog.GetPrecomputedCoord(0));
        }

        [Test]
        public void GetPrecomputedCoord_AfterPrecompute_AzimuthInRange()
        {
            var catalog = new StarCatalog(BuildTestCatalog());
            catalog.PrecomputePositions(48.85, 2.35, DateTime.UtcNow);

            var lod0 = catalog.GetLOD0Indices();
            foreach (int idx in lod0)
            {
                var coord = catalog.GetPrecomputedCoord(idx);
                Assert.GreaterOrEqual(coord.Azimuth, 0.0);
                Assert.Less(coord.Azimuth, 360.0);
            }
        }

        [Test]
        public void GetPrecomputedCoord_AfterPrecompute_AltitudeInRange()
        {
            var catalog = new StarCatalog(BuildTestCatalog());
            catalog.PrecomputePositions(48.85, 2.35, DateTime.UtcNow);

            var lod0 = catalog.GetLOD0Indices();
            foreach (int idx in lod0)
            {
                var coord = catalog.GetPrecomputedCoord(idx);
                Assert.GreaterOrEqual(coord.Altitude, -90.0);
                Assert.LessOrEqual(coord.Altitude, 90.0);
            }
        }
    }
}
