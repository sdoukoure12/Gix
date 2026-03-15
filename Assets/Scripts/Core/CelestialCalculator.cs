using System;

namespace Gix.Core
{
    /// <summary>
    /// Provides precise astronomical calculations for celestial object positions.
    ///
    /// Coordinate systems used:
    ///   - Equatorial: Right Ascension (RA) / Declination (Dec) — fixed to the stars.
    ///   - Horizontal: Azimuth (Az, clockwise from North) / Altitude (Alt, degrees above horizon).
    ///
    /// References:
    ///   Meeus, J. — "Astronomical Algorithms", 2nd ed. (1998).
    /// </summary>
    public static class CelestialCalculator
    {
        // ------------------------------------------------------------------ constants

        private const double Rad = Math.PI / 180.0;
        private const double Deg = 180.0 / Math.PI;

        // ------------------------------------------------------------------ public API

        /// <summary>
        /// Converts equatorial coordinates (RA/Dec) to local horizontal coordinates
        /// (Az/Alt) for a given observer location and UTC time.
        /// </summary>
        /// <param name="rightAscensionDeg">Right Ascension in degrees (0–360).</param>
        /// <param name="declinationDeg">Declination in degrees (-90 to +90).</param>
        /// <param name="observerLatDeg">Observer geodetic latitude in degrees.</param>
        /// <param name="observerLonDeg">Observer geodetic longitude in degrees (East positive).</param>
        /// <param name="utc">Universal Coordinated Time.</param>
        /// <returns>Azimuth (°, N=0 clockwise) and Altitude (°) above horizon.</returns>
        public static (double azimuth, double altitude) EquatorialToHorizontal(
            double rightAscensionDeg,
            double declinationDeg,
            double observerLatDeg,
            double observerLonDeg,
            DateTime utc)
        {
            double lst = LocalSiderealTime(utc, observerLonDeg);
            double ha = NormalizeAngle360(lst - rightAscensionDeg); // Hour Angle in degrees

            double decRad = declinationDeg * Rad;
            double latRad = observerLatDeg * Rad;
            double haRad = ha * Rad;

            // Altitude
            double sinAlt = Math.Sin(decRad) * Math.Sin(latRad)
                          + Math.Cos(decRad) * Math.Cos(latRad) * Math.Cos(haRad);
            double altitude = Math.Asin(Clamp(sinAlt, -1.0, 1.0)) * Deg;

            // Azimuth (measured from South, then converted to North)
            double cosAz = (Math.Sin(decRad) - Math.Sin(latRad) * sinAlt)
                         / (Math.Cos(latRad) * Math.Cos(altitude * Rad));
            double azimuth = Math.Acos(Clamp(cosAz, -1.0, 1.0)) * Deg;

            if (Math.Sin(haRad) > 0.0) azimuth = 360.0 - azimuth;

            return (azimuth, altitude);
        }

        /// <summary>
        /// Computes the Greenwich Mean Sidereal Time (GMST) in degrees for a given UTC.
        /// </summary>
        public static double GreenwichMeanSiderealTime(DateTime utc)
        {
            double jd = JulianDate(utc);
            double t = (jd - 2451545.0) / 36525.0; // Julian centuries from J2000.0

            // GMST in seconds of time (IAU 1982 formula)
            double gmst = 280.46061837
                        + 360.98564736629 * (jd - 2451545.0)
                        + t * t * 0.000387933
                        - t * t * t / 38710000.0;

            return NormalizeAngle360(gmst);
        }

        /// <summary>
        /// Computes the Local Sidereal Time (LST) in degrees for a given observer longitude and UTC.
        /// </summary>
        public static double LocalSiderealTime(DateTime utc, double longitudeDeg)
            => NormalizeAngle360(GreenwichMeanSiderealTime(utc) + longitudeDeg);

        /// <summary>
        /// Computes the Julian Date for a given UTC DateTime.
        /// </summary>
        public static double JulianDate(DateTime utc)
        {
            // Algorithm from Meeus, Ch.7
            int y = utc.Year;
            int m = utc.Month;
            double d = utc.Day
                     + utc.Hour / 24.0
                     + utc.Minute / 1440.0
                     + utc.Second / 86400.0
                     + utc.Millisecond / 86400000.0;

            if (m <= 2) { y--; m += 12; }

            int a = y / 100;
            int b = 2 - a + a / 4;

            return Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + d + b - 1524.5;
        }

        /// <summary>
        /// Converts azimuth/altitude to a 3D unit direction vector in a right-handed
        /// coordinate system where +Z is zenith, +X is East, +Y is North.
        /// </summary>
        public static (double x, double y, double z) HorizontalToCartesian(double azimuthDeg, double altitudeDeg)
        {
            double az = azimuthDeg * Rad;
            double alt = altitudeDeg * Rad;

            double cosAlt = Math.Cos(alt);
            double x = cosAlt * Math.Sin(az);   // East
            double y = cosAlt * Math.Cos(az);   // North
            double z = Math.Sin(alt);            // Up

            return (x, y, z);
        }

        /// <summary>
        /// Computes the angular separation in degrees between two points
        /// given in equatorial coordinates.
        /// </summary>
        public static double AngularSeparation(
            double ra1Deg, double dec1Deg,
            double ra2Deg, double dec2Deg)
        {
            double ra1 = ra1Deg * Rad;
            double dec1 = dec1Deg * Rad;
            double ra2 = ra2Deg * Rad;
            double dec2 = dec2Deg * Rad;

            double cosSep = Math.Sin(dec1) * Math.Sin(dec2)
                          + Math.Cos(dec1) * Math.Cos(dec2) * Math.Cos(ra1 - ra2);

            return Math.Acos(Clamp(cosSep, -1.0, 1.0)) * Deg;
        }

        /// <summary>
        /// Applies atmospheric refraction correction to an observed altitude.
        /// Formula from Meeus, p.106 (valid above ~ -1°).
        /// </summary>
        /// <param name="trueAltitudeDeg">Geometric altitude in degrees.</param>
        /// <returns>Apparent altitude after refraction correction (degrees).</returns>
        public static double ApplyRefraction(double trueAltitudeDeg)
        {
            if (trueAltitudeDeg < -1.0) return trueAltitudeDeg;
            // Refraction in arc-minutes
            double r = 1.02 / Math.Tan((trueAltitudeDeg + 10.3 / (trueAltitudeDeg + 5.11)) * Rad) + 0.0019279;
            return trueAltitudeDeg + r / 60.0;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Normalises an angle to the range [0, 360).</summary>
        public static double NormalizeAngle360(double angle)
        {
            angle = angle % 360.0;
            return angle < 0.0 ? angle + 360.0 : angle;
        }

        private static double Clamp(double value, double min, double max)
            => value < min ? min : value > max ? max : value;
    }
}
