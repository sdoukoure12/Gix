using System;
using NUnit.Framework;
using Gix.Core;

namespace Gix.Tests.EditMode
{
    /// <summary>
    /// Unit tests for <see cref="CelestialCalculator"/>.
    /// All expected values are derived from the Astronomical Almanac and
    /// cross-checked with the Stellarium desktop planetarium.
    /// Tolerance: ±0.5° for all angular results (acceptable for consumer AR use).
    /// </summary>
    [TestFixture]
    public class CelestialCalculatorTests
    {
        // ------------------------------------------------------------------ Julian Date

        [Test]
        public void JulianDate_J2000Epoch_Returns2451545()
        {
            // J2000.0 = 2000-01-01 12:00:00 UTC
            var j2000 = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            double jd = CelestialCalculator.JulianDate(j2000);
            Assert.AreEqual(2451545.0, jd, 1e-6, "J2000 Julian Date should be 2451545.0");
        }

        [Test]
        public void JulianDate_1987April10_ReturnsKnownValue()
        {
            // Meeus example: 1987 April 10, 0h UT → JD 2446895.5
            var date = new DateTime(1987, 4, 10, 0, 0, 0, DateTimeKind.Utc);
            double jd = CelestialCalculator.JulianDate(date);
            Assert.AreEqual(2446895.5, jd, 1e-4);
        }

        [Test]
        public void JulianDate_1900Jan1_ReturnsKnownValue()
        {
            // 1900-01-01 12:00:00 UTC → JD 2415021.0
            var date = new DateTime(1900, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            double jd = CelestialCalculator.JulianDate(date);
            Assert.AreEqual(2415021.0, jd, 1e-4);
        }

        // ------------------------------------------------------------------ GMST

        [Test]
        public void GMST_J2000_ReturnsApprox280Degrees()
        {
            var j2000 = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            double gmst = CelestialCalculator.GreenwichMeanSiderealTime(j2000);
            // GMST at J2000 ≈ 280.46° (Meeus Table 12.a)
            Assert.AreEqual(280.46, gmst, 0.1, "GMST at J2000 should be ~280.46°");
        }

        [Test]
        public void GMST_IsNormalisedBetween0And360()
        {
            for (int d = 0; d < 365; d++)
            {
                var utc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(d);
                double gmst = CelestialCalculator.GreenwichMeanSiderealTime(utc);
                Assert.GreaterOrEqual(gmst, 0.0, $"GMST should be >= 0 on day {d}");
                Assert.Less(gmst, 360.0, $"GMST should be < 360 on day {d}");
            }
        }

        // ------------------------------------------------------------------ EquatorialToHorizontal

        [Test]
        public void EquatorialToHorizontal_CircumpolarStarAtNorthPole_HasAltitudeNearDeclination()
        {
            // At the North Pole (lat=90°), a star's altitude equals its declination.
            // Use a circumpolar star: Polaris RA=37.95°, Dec=89.26°
            var utc = new DateTime(2024, 3, 20, 0, 0, 0, DateTimeKind.Utc);
            var (_, alt) = CelestialCalculator.EquatorialToHorizontal(37.95, 89.26, 90.0, 0.0, utc);
            Assert.AreEqual(89.26, alt, 0.5, "At North Pole, Polaris altitude ≈ its declination");
        }

        [Test]
        public void EquatorialToHorizontal_StarOnEquatorFromEquator_MaxAltitudeIs90()
        {
            // A star exactly on the celestial equator (Dec=0) transits at 90° altitude
            // when observed from the equator (lat=0) and is on the meridian (HA=0 → RA=LST).
            // We pick a UTC where LST = 0° for lon=0.
            // At J2000: GMST ≈ 280.46°, so for LST=0 we need lon=-280.46 ≡ 79.54°E.
            // Instead, pick RA = LST at the chosen time.
            var utc = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            double lst = CelestialCalculator.LocalSiderealTime(utc, 0.0);
            var (_, alt) = CelestialCalculator.EquatorialToHorizontal(lst, 0.0, 0.0, 0.0, utc);
            Assert.AreEqual(90.0, alt, 1.0,
                "Star on equator transiting meridian from equatorial observer should reach ~90°");
        }

        [Test]
        public void EquatorialToHorizontal_BelowHorizonStar_HasNegativeAltitude()
        {
            // A star with Dec < -(90-lat) never rises from that latitude.
            // Observer lat=60°N, star Dec=-70° (never rises from 60°N since 90-60=30, and 70>30)
            var utc = new DateTime(2024, 6, 21, 0, 0, 0, DateTimeKind.Utc);
            var (_, alt) = CelestialCalculator.EquatorialToHorizontal(0.0, -70.0, 60.0, 0.0, utc);
            Assert.Less(alt, 0.0, "Star that never rises should have negative altitude");
        }

        // ------------------------------------------------------------------ HorizontalToCartesian

        [Test]
        public void HorizontalToCartesian_Zenith_ReturnsZenithVector()
        {
            var (x, y, z) = CelestialCalculator.HorizontalToCartesian(0.0, 90.0);
            Assert.AreEqual(0.0, x, 1e-10);
            Assert.AreEqual(0.0, y, 1e-10);
            Assert.AreEqual(1.0, z, 1e-10);
        }

        [Test]
        public void HorizontalToCartesian_North_ReturnsNorthVector()
        {
            // Azimuth=0° (North), Altitude=0° → Y-axis unit vector
            var (x, y, z) = CelestialCalculator.HorizontalToCartesian(0.0, 0.0);
            Assert.AreEqual(0.0, x, 1e-10);
            Assert.AreEqual(1.0, y, 1e-10);
            Assert.AreEqual(0.0, z, 1e-10);
        }

        [Test]
        public void HorizontalToCartesian_East_ReturnsEastVector()
        {
            // Azimuth=90° (East), Altitude=0° → X-axis unit vector
            var (x, y, z) = CelestialCalculator.HorizontalToCartesian(90.0, 0.0);
            Assert.AreEqual(1.0, x, 1e-10);
            Assert.AreEqual(0.0, y, 1e-10);
            Assert.AreEqual(0.0, z, 1e-10);
        }

        // ------------------------------------------------------------------ AngularSeparation

        [Test]
        public void AngularSeparation_SamePoint_ReturnsZero()
        {
            double sep = CelestialCalculator.AngularSeparation(45.0, 30.0, 45.0, 30.0);
            Assert.AreEqual(0.0, sep, 1e-10);
        }

        [Test]
        public void AngularSeparation_OppositePoints_Returns180()
        {
            // Antipodal points: (0,0) vs (180,0)
            double sep = CelestialCalculator.AngularSeparation(0.0, 0.0, 180.0, 0.0);
            Assert.AreEqual(180.0, sep, 1e-8);
        }

        [Test]
        public void AngularSeparation_90DegreeSeparation_IsCorrect()
        {
            // North pole (0°,90°) vs equator (0°,0°) → 90°
            double sep = CelestialCalculator.AngularSeparation(0.0, 90.0, 0.0, 0.0);
            Assert.AreEqual(90.0, sep, 1e-8);
        }

        // ------------------------------------------------------------------ NormalizeAngle360

        [Test]
        public void NormalizeAngle360_NegativeAngle_WrapsPositive()
        {
            Assert.AreEqual(350.0, CelestialCalculator.NormalizeAngle360(-10.0), 1e-10);
        }

        [Test]
        public void NormalizeAngle360_Over360_WrapsDown()
        {
            Assert.AreEqual(10.0, CelestialCalculator.NormalizeAngle360(370.0), 1e-10);
        }

        [Test]
        public void NormalizeAngle360_Zero_ReturnsZero()
        {
            Assert.AreEqual(0.0, CelestialCalculator.NormalizeAngle360(0.0), 1e-10);
        }

        // ------------------------------------------------------------------ Refraction

        [Test]
        public void ApplyRefraction_PositiveAltitude_IncreasesAltitude()
        {
            // Refraction raises apparent altitude
            double corrected = CelestialCalculator.ApplyRefraction(10.0);
            Assert.Greater(corrected, 10.0, "Refraction should increase apparent altitude");
        }

        [Test]
        public void ApplyRefraction_HighAltitude_SmallCorrection()
        {
            // At 45°, refraction is about 1 arcmin ≈ 0.017°
            double corrected = CelestialCalculator.ApplyRefraction(45.0);
            double delta = corrected - 45.0;
            Assert.AreEqual(0.017, delta, 0.005, "Refraction at 45° should be ~1 arcmin");
        }

        [Test]
        public void ApplyRefraction_BelowMinus1_NoCorrection()
        {
            double alt = -5.0;
            double corrected = CelestialCalculator.ApplyRefraction(alt);
            Assert.AreEqual(alt, corrected, 1e-10, "No refraction correction below -1°");
        }
    }
}
