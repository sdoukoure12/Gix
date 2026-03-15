using System;

namespace Gix.Utils
{
    /// <summary>
    /// Parses Two-Line Element (TLE) sets used to describe satellite orbits.
    /// Specification: https://celestrak.org/SOCRATES/query.php (Kelso TLE format)
    /// </summary>
    public static class TLEParser
    {
        /// <summary>
        /// Parses a TLE set from three lines (name, line1, line2).
        /// </summary>
        /// <param name="name">Satellite name line (line 0).</param>
        /// <param name="line1">TLE line 1.</param>
        /// <param name="line2">TLE line 2.</param>
        /// <returns>Parsed <see cref="TLEElements"/> structure.</returns>
        /// <exception cref="ArgumentException">Thrown when lines are malformed.</exception>
        public static TLEElements Parse(string name, string line1, string line2)
        {
            if (string.IsNullOrWhiteSpace(line1)) throw new ArgumentException("TLE line 1 is empty.", nameof(line1));
            if (string.IsNullOrWhiteSpace(line2)) throw new ArgumentException("TLE line 2 is empty.", nameof(line2));

            line1 = line1.Trim();
            line2 = line2.Trim();

            if (line1.Length < 69) throw new ArgumentException($"TLE line 1 too short ({line1.Length} chars, expected 69).", nameof(line1));
            if (line2.Length < 69) throw new ArgumentException($"TLE line 2 too short ({line2.Length} chars, expected 69).", nameof(line2));

            ValidateChecksum(line1, 1);
            ValidateChecksum(line2, 2);

            var tle = new TLEElements();
            tle.Name = (name ?? string.Empty).Trim();
            tle.SatelliteNumber = ParseInt(line1, 2, 5);
            tle.Classification = line1[7];
            tle.InternationalDesignator = line1.Substring(9, 8).Trim();

            // Epoch: YY + day-of-year fractional
            double rawEpoch = ParseDouble(line1, 18, 14);
            tle.Epoch = EpochToDateTime(rawEpoch);

            tle.MeanMotionDot = ParseDouble(line1, 33, 10);
            tle.MeanMotionDdot = ParseDecimalPoint(line1, 44, 8);
            tle.Bstar = ParseDecimalPoint(line1, 53, 8);

            tle.Inclination = ParseDouble(line2, 8, 8);
            tle.RightAscensionAscendingNode = ParseDouble(line2, 17, 8);
            tle.Eccentricity = double.Parse("0." + line2.Substring(26, 7).Trim());
            tle.ArgumentOfPerigee = ParseDouble(line2, 34, 8);
            tle.MeanAnomaly = ParseDouble(line2, 43, 8);
            tle.MeanMotion = ParseDouble(line2, 52, 11);
            tle.RevolutionNumber = ParseInt(line2, 63, 5);

            return tle;
        }

        // ------------------------------------------------------------------ helpers

        private static void ValidateChecksum(string line, int lineNumber)
        {
            int sum = 0;
            for (int i = 0; i < 68; i++)
            {
                char c = line[i];
                if (char.IsDigit(c)) sum += c - '0';
                else if (c == '-') sum += 1;
            }
            int expected = sum % 10;
            int actual = line[68] - '0';
            if (expected != actual)
                throw new ArgumentException(
                    $"TLE line {lineNumber} checksum mismatch: expected {expected}, got {actual}.");
        }

        private static int ParseInt(string line, int start, int length)
            => int.Parse(line.Substring(start, length).Trim());

        private static double ParseDouble(string line, int start, int length)
            => double.Parse(line.Substring(start, length).Trim(),
                System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// Parses a TLE "assumed decimal point" field of the form ±NNNNN±EE
        /// into a double.  Example: " 00000-0" → 0.0, "-11606-4" → -0.000011606.
        /// </summary>
        private static double ParseDecimalPoint(string line, int start, int length)
        {
            string raw = line.Substring(start, length).Trim();
            if (raw == "00000-0" || raw == " 00000-0") return 0.0;

            // Find last +/- that is not the leading sign
            int signIdx = -1;
            for (int i = raw.Length - 1; i > 0; i--)
            {
                if (raw[i] == '+' || raw[i] == '-') { signIdx = i; break; }
            }
            if (signIdx < 0) return 0.0;

            double mantissa = double.Parse("0." + raw.Substring(0, signIdx).TrimStart('+', '-').Trim(),
                System.Globalization.CultureInfo.InvariantCulture);
            if (raw[0] == '-') mantissa = -mantissa;

            int exp = int.Parse(raw.Substring(signIdx));
            return mantissa * Math.Pow(10.0, exp);
        }

        /// <summary>
        /// Converts a TLE epoch (YYddd.dddddddd) to a UTC DateTime.
        /// Years 00-56 → 2000-2056; 57-99 → 1957-1999.
        /// </summary>
        private static DateTime EpochToDateTime(double rawEpoch)
        {
            int year2d = (int)(rawEpoch / 1000.0);
            double dayOfYear = rawEpoch - year2d * 1000.0;

            int fullYear = year2d < 57 ? 2000 + year2d : 1900 + year2d;
            DateTime jan1 = new DateTime(fullYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return jan1.AddDays(dayOfYear - 1.0);
        }
    }

    /// <summary>
    /// Keplerian orbital elements extracted from a TLE set.
    /// </summary>
    public struct TLEElements
    {
        public string Name;
        public int SatelliteNumber;
        public char Classification;
        public string InternationalDesignator;
        public DateTime Epoch;

        // Line 1 derived
        public double MeanMotionDot;    // revs/day²
        public double MeanMotionDdot;   // revs/day³
        public double Bstar;            // drag term

        // Line 2 derived
        public double Inclination;                  // degrees
        public double RightAscensionAscendingNode;  // degrees
        public double Eccentricity;                 // dimensionless
        public double ArgumentOfPerigee;            // degrees
        public double MeanAnomaly;                  // degrees
        public double MeanMotion;                   // revs/day
        public int RevolutionNumber;
    }
}
