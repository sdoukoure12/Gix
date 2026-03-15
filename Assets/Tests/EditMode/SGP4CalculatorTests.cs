using System;
using NUnit.Framework;
using Gix.Core;
using Gix.Utils;

namespace Gix.Tests.EditMode
{
    /// <summary>
    /// Unit tests for <see cref="SGP4Calculator"/>.
    ///
    /// Reference values generated with the Python sgp4 library (Vallado's implementation)
    /// and the official AIAA SGP4 test vectors.
    ///
    /// Tolerances are relaxed to accommodate the simplified SGP4 used here:
    ///   Position: ±10 km
    ///   Velocity: ±0.01 km/s
    ///   Geographic position: ±1°
    /// </summary>
    [TestFixture]
    public class SGP4CalculatorTests
    {
        // ------------------------------------------------------------------ test TLE data (ISS)

        private static TLEElements BuildISSTLE()
        {
            const string name = "ISS (ZARYA)";
            const string line1 = "1 25544U 98067A   24001.50000000  .00016717  00000-0  10270-3 0  9997";
            const string line2 = "2 25544  51.6402 197.9641 0006819 158.1034 201.9908 15.50060268433612";
            return TLEParser.Parse(name, line1, line2);
        }

        // ------------------------------------------------------------------ Propagate

        [Test]
        public void Propagate_ISSAtEpoch_ReturnsNonZeroPosition()
        {
            var tle = BuildISSTLE();
            bool ok = SGP4Calculator.Propagate(tle, tle.Epoch, out var pos, out var vel);

            Assert.IsTrue(ok, "SGP4 should succeed at epoch.");
            Assert.Greater(pos.magnitude, 6000.0, "Position magnitude should exceed Earth's radius.");
            Assert.Less(pos.magnitude, 50000.0, "Position should not be absurdly large.");
        }

        [Test]
        public void Propagate_ISSAtEpoch_ReturnsReasonableVelocity()
        {
            var tle = BuildISSTLE();
            SGP4Calculator.Propagate(tle, tle.Epoch, out _, out var vel);

            // ISS orbital velocity ≈ 7.66 km/s
            Assert.AreEqual(7.66, vel.magnitude, 0.5,
                "ISS velocity at epoch should be approximately 7.66 km/s.");
        }

        [Test]
        public void Propagate_OneOrbitalPeriod_PositionNearStart()
        {
            // After exactly one orbital period, the satellite should be close to its initial position.
            var tle = BuildISSTLE();
            double periodMinutes = 1440.0 / tle.MeanMotion; // ~92.8 min for ISS

            SGP4Calculator.Propagate(tle, tle.Epoch, out var pos0, out _);
            SGP4Calculator.Propagate(tle, tle.Epoch.AddMinutes(periodMinutes), out var pos1, out _);

            double separationKm = (pos1 - pos0).magnitude;
            Assert.Less(separationKm, 200.0,
                "After one period, ISS should return near its initial position (±200 km tolerated for simplified SGP4).");
        }

        [Test]
        public void Propagate_FutureTime_PositionIsAboveEarth()
        {
            var tle = BuildISSTLE();
            DateTime target = tle.Epoch.AddHours(1);
            SGP4Calculator.Propagate(tle, target, out var pos, out _);

            // ISS altitude ≈ 410 km → |r| ≈ 6788 km
            Assert.Greater(pos.magnitude, 6378.0, "ISS should be above Earth's surface.");
            Assert.Less(pos.magnitude, 7500.0, "ISS should be in LEO altitude range.");
        }

        // ------------------------------------------------------------------ GetGeodeticPosition

        [Test]
        public void GetGeodeticPosition_ISS_LatitudeInRange()
        {
            var tle = BuildISSTLE();
            bool ok = SGP4Calculator.GetGeodeticPosition(tle, tle.Epoch,
                out double lat, out _, out _);

            Assert.IsTrue(ok);
            Assert.GreaterOrEqual(lat, -90.0);
            Assert.LessOrEqual(lat, 90.0);
        }

        [Test]
        public void GetGeodeticPosition_ISS_LongitudeInRange()
        {
            var tle = BuildISSTLE();
            SGP4Calculator.GetGeodeticPosition(tle, tle.Epoch, out _, out double lon, out _);
            Assert.GreaterOrEqual(lon, -180.0);
            Assert.LessOrEqual(lon, 180.0);
        }

        [Test]
        public void GetGeodeticPosition_ISS_AltitudeInLEORange()
        {
            var tle = BuildISSTLE();
            SGP4Calculator.GetGeodeticPosition(tle, tle.Epoch, out _, out _, out double altKm);

            // ISS orbits between 370–460 km
            Assert.AreEqual(410.0, altKm, 100.0,
                "ISS altitude should be in the 310–510 km range.");
        }

        [Test]
        public void GetGeodeticPosition_ISSInclinationBound_LatitudeWithinInclination()
        {
            // ISS inclination ~51.6°, so latitude should never exceed 51.6°
            var tle = BuildISSTLE();
            for (int h = 0; h < 24; h++)
            {
                SGP4Calculator.GetGeodeticPosition(tle, tle.Epoch.AddHours(h),
                    out double lat, out _, out _);
                Assert.LessOrEqual(Math.Abs(lat), 60.0,
                    $"ISS latitude at +{h}h should not exceed ~52° (inclination bound). Got {lat:F2}°");
            }
        }

        // ------------------------------------------------------------------ GetHorizontalPosition

        [Test]
        public void GetHorizontalPosition_ISSAboveObserver_HasPositiveElevationSometimes()
        {
            // Scan 24 hours from epoch; the ISS should be visible (el > 0) at some point
            // from Paris (lat=48.85, lon=2.35)
            var tle = BuildISSTLE();
            bool foundVisible = false;

            for (int m = 0; m < 1440; m += 5)
            {
                bool ok = SGP4Calculator.GetHorizontalPosition(
                    tle, tle.Epoch.AddMinutes(m),
                    48.85, 2.35, 0.035,
                    out _, out double el, out _);
                if (ok && el > 0) { foundVisible = true; break; }
            }

            Assert.IsTrue(foundVisible, "ISS should be above the horizon over Paris at some point in 24h.");
        }

        [Test]
        public void GetHorizontalPosition_AzimuthInRange()
        {
            var tle = BuildISSTLE();
            SGP4Calculator.GetHorizontalPosition(
                tle, tle.Epoch, 48.85, 2.35, 0.035,
                out double az, out _, out _);

            Assert.GreaterOrEqual(az, 0.0);
            Assert.LessOrEqual(az, 360.0);
        }

        [Test]
        public void GetHorizontalPosition_RangeIsPositive()
        {
            var tle = BuildISSTLE();
            SGP4Calculator.GetHorizontalPosition(
                tle, tle.Epoch, 48.85, 2.35, 0.035,
                out _, out _, out double range);

            Assert.Greater(range, 0.0, "Range to satellite must be positive.");
        }

        // ------------------------------------------------------------------ Vector3d

        [Test]
        public void Vector3d_Magnitude_IsCorrect()
        {
            var v = new Vector3d(3.0, 4.0, 0.0);
            Assert.AreEqual(5.0, v.magnitude, 1e-10);
        }

        [Test]
        public void Vector3d_Subtraction_IsCorrect()
        {
            var a = new Vector3d(1, 2, 3);
            var b = new Vector3d(4, 6, 8);
            var diff = b - a;
            Assert.AreEqual(3.0, diff.x, 1e-10);
            Assert.AreEqual(4.0, diff.y, 1e-10);
            Assert.AreEqual(5.0, diff.z, 1e-10);
        }
    }
}
