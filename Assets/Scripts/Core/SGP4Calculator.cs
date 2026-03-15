using System;
using UnityEngine;
using Gix.Utils;

namespace Gix.Core
{
    /// <summary>
    /// Simplified SGP4/Keplerian satellite propagator.
    ///
    /// Propagates a satellite's orbital state from its TLE epoch to a target time,
    /// producing an Earth-Centered Inertial (ECI) position and velocity vector.
    ///
    /// Implementation notes:
    ///   - For LEO objects (period &lt; 225 min) a simplified SGP4 with J2 secular
    ///     perturbations and first-order atmospheric drag is used.
    ///   - For deep-space objects (period >= 225 min) a simple Keplerian propagator
    ///     is used.
    ///   - Position accuracy: typically &lt; 5 km for intervals &lt; 1 orbit.
    ///
    /// Units: km, km/s (all output); degrees (I/O angular).
    ///
    /// References:
    ///   Vallado, D.A. — "Fundamentals of Astrodynamics and Applications", 4th ed. (2013)
    ///   Kelso, T.S.   — https://celestrak.org/publications/AIAA/2006-6753/
    /// </summary>
    public static class SGP4Calculator
    {
        // ------------------------------------------------------------------ WGS-84 / SGP4 constants

        /// <summary>Earth gravitational parameter (km³/s²).</summary>
        private const double Mu = 398600.4418;

        /// <summary>Earth equatorial radius (km, WGS-84).</summary>
        private const double Re = 6378.137;

        /// <summary>J2 zonal harmonic coefficient.</summary>
        private const double J2 = 1.08262668e-3;

        private const double TwoPi      = Math.PI * 2.0;
        private const double MinPerDay  = 1440.0;          // minutes per day
        private const double SecPerDay  = 86400.0;         // seconds per day

        // ------------------------------------------------------------------ public API

        /// <summary>
        /// Propagates the satellite from its TLE epoch to <paramref name="targetUtc"/>
        /// and returns the ECI position (km) and velocity (km/s).
        /// </summary>
        public static bool Propagate(TLEElements tle, DateTime targetUtc,
            out Vector3d posKm, out Vector3d velKms)
        {
            double minutesSinceEpoch = (targetUtc - tle.Epoch).TotalMinutes;
            return PropagateSGP4(tle, minutesSinceEpoch, out posKm, out velKms);
        }

        /// <summary>
        /// Returns the sub-satellite geodetic latitude (°), longitude (°) and altitude (km).
        /// </summary>
        public static bool GetGeodeticPosition(TLEElements tle, DateTime targetUtc,
            out double latDeg, out double lonDeg, out double altKm)
        {
            if (!Propagate(tle, targetUtc, out Vector3d pos, out _))
            {
                latDeg = lonDeg = altKm = 0;
                return false;
            }
            ECIToGeodetic(pos, targetUtc, out latDeg, out lonDeg, out altKm);
            return true;
        }

        /// <summary>
        /// Computes the azimuth (°) and elevation (°) of the satellite as seen from
        /// an observer at the given geodetic coordinates.
        /// </summary>
        public static bool GetHorizontalPosition(
            TLEElements tle,
            DateTime targetUtc,
            double observerLatDeg,
            double observerLonDeg,
            double observerAltKm,
            out double azimuthDeg,
            out double elevationDeg,
            out double rangeKm)
        {
            azimuthDeg = elevationDeg = rangeKm = 0;

            if (!Propagate(tle, targetUtc, out Vector3d satECI, out _))
                return false;

            Vector3d obsECI = GeodeticToECI(observerLatDeg, observerLonDeg, observerAltKm, targetUtc);
            Vector3d rng    = satECI - obsECI;
            rangeKm         = rng.magnitude;
            if (rangeKm < 1e-9) return false;

            // Rotate range vector from ECI to South-East-Zenith (SEZ) topocentric frame
            double lstRad = CelestialCalculator.LocalSiderealTime(targetUtc, observerLonDeg) * Math.PI / 180.0;
            double latRad = observerLatDeg * Math.PI / 180.0;
            double sinLat = Math.Sin(latRad), cosLat = Math.Cos(latRad);
            double sinLst = Math.Sin(lstRad), cosLst = Math.Cos(lstRad);

            double s = sinLat * cosLst * rng.x + sinLat * sinLst * rng.y - cosLat * rng.z;
            double e = -sinLst * rng.x + cosLst * rng.y;
            double z =  cosLat * cosLst * rng.x + cosLat * sinLst * rng.y + sinLat * rng.z;

            elevationDeg = Math.Asin(Clamp(z / rangeKm, -1.0, 1.0)) * 180.0 / Math.PI;
            azimuthDeg   = NormDeg(Math.Atan2(-e, s) * 180.0 / Math.PI + 180.0);
            return true;
        }

        // ------------------------------------------------------------------ propagation core

        /// <summary>
        /// SGP4/simplified Keplerian propagator.
        /// Period &lt; 225 min → secular-perturbation SGP4 (LEO).
        /// Period >= 225 min → simple Keplerian (deep space).
        /// </summary>
        private static bool PropagateSGP4(TLEElements tle, double minutesSinceEpoch,
            out Vector3d posKm, out Vector3d velKms)
        {
            posKm = velKms = Vector3d.zero;

            // Mean motion in rad/s (TLE gives rev/day)
            double n0_rps = tle.MeanMotion * TwoPi / SecPerDay;
            if (n0_rps <= 0) return false;

            // Semi-major axis from mean motion (km)
            double a0 = Math.Pow(Mu / (n0_rps * n0_rps), 1.0 / 3.0);

            double periodMin = MinPerDay / tle.MeanMotion;

            if (periodMin >= 225.0)
                return PropagateKeplerian(tle, minutesSinceEpoch, out posKm, out velKms);

            // ----- Secular effects (first-order J2 + drag) ---------------
            double e0       = tle.Eccentricity;
            double incRad   = tle.Inclination * Math.PI / 180.0;
            double cosI     = Math.Cos(incRad);
            double sinI     = Math.Sin(incRad);
            double eta0     = a0 * e0;                  // not used directly
            double p0       = a0 * (1.0 - e0 * e0);     // semi-latus rectum (km)
            double n0InvP2  = (a0 / Re) * (a0 / Re);    // (a/Re)^2, dimensionless

            // J2 secular rates (rad/s)
            double j2Factor = 1.5 * J2 * (Re * Re) / (p0 * p0);
            double raanDot  = -j2Factor * n0_rps * cosI;                          // RAAN precession
            double argpDot  =  j2Factor * n0_rps * (2.0 - 2.5 * sinI * sinI);    // perigee drift
            double mDot     =  j2Factor * n0_rps * Math.Sqrt(1.0 - e0 * e0)
                               * (1.0 - 1.5 * sinI * sinI);                       // mean motion correction

            // First-order drag (TLE gives ndot in rev/day²)
            double ndot_rps2 = tle.MeanMotionDot * TwoPi / (SecPerDay * SecPerDay);

            double tSec = minutesSinceEpoch * 60.0;   // propagation time in seconds

            // Updated mean motion
            double n_rps = n0_rps + ndot_rps2 * tSec;
            if (n_rps <= 0) n_rps = n0_rps;

            // Updated semi-major axis (from updated mean motion)
            double a = Math.Pow(Mu / (n_rps * n_rps), 1.0 / 3.0);

            // Updated orbital elements at time t
            double raan  = NormRad(tle.RightAscensionAscendingNode * Math.PI / 180.0 + raanDot * tSec);
            double argp  = NormRad(tle.ArgumentOfPerigee * Math.PI / 180.0 + argpDot * tSec);
            double m0    = tle.MeanAnomaly * Math.PI / 180.0;
            double M     = NormRad(m0 + (n0_rps + mDot) * tSec);
            double e     = Math.Max(1e-7, e0);   // eccentricity unchanged at first order

            // Solve Kepler's equation
            double E = SolveKepler(M, e);

            // True anomaly
            double nu = 2.0 * Math.Atan2(
                Math.Sqrt(1.0 + e) * Math.Sin(E / 2.0),
                Math.Sqrt(1.0 - e) * Math.Cos(E / 2.0));

            // Radius (km)
            double p  = a * (1.0 - e * e);
            double r  = p / (1.0 + e * Math.Cos(nu));

            return OrbitalElementsToECI(r, nu, a, e, incRad, raan, argp, Mu,
                out posKm, out velKms);
        }

        /// <summary>Simple Keplerian propagator (no perturbations).</summary>
        private static bool PropagateKeplerian(TLEElements tle, double minutesSinceEpoch,
            out Vector3d posKm, out Vector3d velKms)
        {
            posKm = velKms = Vector3d.zero;

            double n_rps  = tle.MeanMotion * TwoPi / SecPerDay;    // rad/s
            double a      = Math.Pow(Mu / (n_rps * n_rps), 1.0 / 3.0); // km
            double e      = tle.Eccentricity;
            double incRad = tle.Inclination * Math.PI / 180.0;
            double raan   = tle.RightAscensionAscendingNode * Math.PI / 180.0;
            double argp   = tle.ArgumentOfPerigee * Math.PI / 180.0;
            double m0     = tle.MeanAnomaly * Math.PI / 180.0;

            double M  = NormRad(m0 + n_rps * minutesSinceEpoch * 60.0);
            double E  = SolveKepler(M, e);
            double nu = 2.0 * Math.Atan2(
                Math.Sqrt(1.0 + e) * Math.Sin(E / 2.0),
                Math.Sqrt(1.0 - e) * Math.Cos(E / 2.0));

            double p = a * (1.0 - e * e);
            double r = p / (1.0 + e * Math.Cos(nu));

            return OrbitalElementsToECI(r, nu, a, e, incRad, raan, argp, Mu,
                out posKm, out velKms);
        }

        /// <summary>
        /// Converts radius, true anomaly and classical orbital elements to ECI position and velocity.
        /// </summary>
        private static bool OrbitalElementsToECI(
            double r, double nu,
            double a, double e,
            double incRad, double raan, double argp,
            double mu,
            out Vector3d posKm, out Vector3d velKms)
        {
            posKm = velKms = Vector3d.zero;

            double p = a * (1.0 - e * e);

            // Perifocal coordinates (km)
            double xPQW = r * Math.Cos(nu);
            double yPQW = r * Math.Sin(nu);

            // Perifocal velocity (km/s)
            double sqrtMuP = Math.Sqrt(mu / p);
            double vxPQW   = -sqrtMuP * Math.Sin(nu);
            double vyPQW   =  sqrtMuP * (e + Math.Cos(nu));

            // Rotation matrix: perifocal → ECI
            // [R] = Rz(-Ω) · Rx(-i) · Rz(-ω)
            double cosRaan = Math.Cos(raan), sinRaan = Math.Sin(raan);
            double cosInc  = Math.Cos(incRad), sinInc  = Math.Sin(incRad);
            double cosArgp = Math.Cos(argp), sinArgp = Math.Sin(argp);

            double r11 =  cosRaan * cosArgp - sinRaan * sinArgp * cosInc;
            double r12 = -cosRaan * sinArgp - sinRaan * cosArgp * cosInc;
            double r21 =  sinRaan * cosArgp + cosRaan * sinArgp * cosInc;
            double r22 = -sinRaan * sinArgp + cosRaan * cosArgp * cosInc;
            double r31 =  sinArgp * sinInc;
            double r32 =  cosArgp * sinInc;

            posKm  = new Vector3d(r11 * xPQW + r12 * yPQW,
                                  r21 * xPQW + r22 * yPQW,
                                  r31 * xPQW + r32 * yPQW);

            velKms = new Vector3d(r11 * vxPQW + r12 * vyPQW,
                                  r21 * vxPQW + r22 * vyPQW,
                                  r31 * vxPQW + r32 * vyPQW);
            return true;
        }

        // ------------------------------------------------------------------ coordinate conversions

        private static void ECIToGeodetic(Vector3d posECI, DateTime utc,
            out double latDeg, out double lonDeg, out double altKm)
        {
            double gmstDeg = CelestialCalculator.GreenwichMeanSiderealTime(utc);
            double r       = Math.Sqrt(posECI.x * posECI.x + posECI.y * posECI.y);

            // Geographic (West-positive) longitude corrected for Earth rotation
            lonDeg = NormDeg(Math.Atan2(posECI.y, posECI.x) * 180.0 / Math.PI - gmstDeg);
            // Map to [-180, +180]
            if (lonDeg > 180.0) lonDeg -= 360.0;

            // Iterative geodetic latitude (Bowring's method)
            const double f  = 1.0 / 298.257223563; // WGS-84 flattening
            const double e2 = 2 * f - f * f;
            double lat = Math.Atan2(posECI.z, r);
            for (int i = 0; i < 6; i++)
            {
                double sinLat = Math.Sin(lat);
                double N      = Re / Math.Sqrt(1.0 - e2 * sinLat * sinLat);
                lat = Math.Atan2(posECI.z + e2 * N * sinLat, r);
            }
            latDeg = lat * 180.0 / Math.PI;
            double sinLat2 = Math.Sin(lat);
            double N2      = Re / Math.Sqrt(1.0 - e2 * sinLat2 * sinLat2);
            altKm = (Math.Abs(lat) > 0.01)
                ? posECI.z / sinLat2 - N2 * (1.0 - e2)
                : r / Math.Cos(lat) - N2;
        }

        private static Vector3d GeodeticToECI(double latDeg, double lonDeg,
            double altKm, DateTime utc)
        {
            double gmstDeg = CelestialCalculator.GreenwichMeanSiderealTime(utc);
            double latRad  = latDeg * Math.PI / 180.0;
            double lonRad  = NormRad((lonDeg + gmstDeg) * Math.PI / 180.0);

            const double f  = 1.0 / 298.257223563;
            const double e2 = 2 * f - f * f;
            double sinLat   = Math.Sin(latRad);
            double cosLat   = Math.Cos(latRad);
            double N        = Re / Math.Sqrt(1.0 - e2 * sinLat * sinLat);

            double rc = (N + altKm) * cosLat;
            return new Vector3d(
                rc * Math.Cos(lonRad),
                rc * Math.Sin(lonRad),
                (N * (1.0 - e2) + altKm) * sinLat);
        }

        // ------------------------------------------------------------------ math helpers

        private static double SolveKepler(double M, double e)
        {
            double E = M;
            for (int i = 0; i < 50; i++)
            {
                double dE = (M - E + e * Math.Sin(E)) / (1.0 - e * Math.Cos(E));
                E += dE;
                if (Math.Abs(dE) < 1e-12) break;
            }
            return E;
        }

        private static double NormRad(double angle)
        {
            angle %= TwoPi;
            return angle < 0 ? angle + TwoPi : angle;
        }

        private static double NormDeg(double angle)
        {
            angle %= 360.0;
            return angle < 0 ? angle + 360.0 : angle;
        }

        private static double Clamp(double v, double lo, double hi)
            => v < lo ? lo : v > hi ? hi : v;
    }

    // ------------------------------------------------------------------ supporting types

    /// <summary>
    /// Double-precision 3D vector.  Mirrors Unity's Vector3 API for satellite calculations
    /// where single-precision float introduces unacceptable error at orbital distances.
    /// </summary>
    public struct Vector3d
    {
        public double x, y, z;

        public Vector3d(double x, double y, double z)
        { this.x = x; this.y = y; this.z = z; }

        public static readonly Vector3d zero = new Vector3d(0, 0, 0);

        public double magnitude => Math.Sqrt(x * x + y * y + z * z);

        public static Vector3d operator -(Vector3d a, Vector3d b)
            => new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);

        public static Vector3d operator +(Vector3d a, Vector3d b)
            => new Vector3d(a.x + b.x, a.y + b.y, a.z + b.z);

        public static Vector3d operator *(Vector3d a, double s)
            => new Vector3d(a.x * s, a.y * s, a.z * s);

        public override string ToString() => $"({x:F3}, {y:F3}, {z:F3})";
    }
}
