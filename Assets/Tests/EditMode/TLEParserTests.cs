using System;
using NUnit.Framework;
using Gix.Utils;

namespace Gix.Tests.EditMode
{
    /// <summary>
    /// Unit tests for <see cref="TLEParser"/>.
    /// </summary>
    [TestFixture]
    public class TLEParserTests
    {
        // ------------------------------------------------------------------ valid ISS TLE (sample from CelesTrak)

        private const string ISSName = "ISS (ZARYA)";
        private const string ISSLine1 = "1 25544U 98067A   24001.50000000  .00016717  00000-0  10270-3 0  9997";
        private const string ISSLine2 = "2 25544  51.6402 197.9641 0006819 158.1034 201.9908 15.50060268433612";

        // ------------------------------------------------------------------ basic parsing

        [Test]
        public void Parse_ValidISSTLE_ReturnsSatelliteNumber25544()
        {
            var tle = TLEParser.Parse(ISSName, ISSLine1, ISSLine2);
            Assert.AreEqual(25544, tle.SatelliteNumber);
        }

        [Test]
        public void Parse_ValidISSTLE_ReturnsCorrectName()
        {
            var tle = TLEParser.Parse(ISSName, ISSLine1, ISSLine2);
            Assert.AreEqual(ISSName, tle.Name);
        }

        [Test]
        public void Parse_ValidISSTLE_ReturnsCorrectInclination()
        {
            var tle = TLEParser.Parse(ISSName, ISSLine1, ISSLine2);
            Assert.AreEqual(51.6402, tle.Inclination, 1e-4,
                "ISS inclination should be ~51.64°");
        }

        [Test]
        public void Parse_ValidISSTLE_ReturnsCorrectEccentricity()
        {
            var tle = TLEParser.Parse(ISSName, ISSLine1, ISSLine2);
            // Eccentricity in TLE line 2 is "0006819" → 0.0006819
            Assert.AreEqual(0.0006819, tle.Eccentricity, 1e-7);
        }

        [Test]
        public void Parse_ValidISSTLE_ReturnsCorrectMeanMotion()
        {
            var tle = TLEParser.Parse(ISSName, ISSLine1, ISSLine2);
            Assert.AreEqual(15.50060268, tle.MeanMotion, 1e-5,
                "ISS mean motion should be ~15.5 rev/day");
        }

        [Test]
        public void Parse_ValidISSTLE_ReturnsCorrectRAAN()
        {
            var tle = TLEParser.Parse(ISSName, ISSLine1, ISSLine2);
            Assert.AreEqual(197.9641, tle.RightAscensionAscendingNode, 1e-4);
        }

        [Test]
        public void Parse_ValidISSTLE_ReturnsCorrectMeanAnomaly()
        {
            var tle = TLEParser.Parse(ISSName, ISSLine1, ISSLine2);
            Assert.AreEqual(201.9908, tle.MeanAnomaly, 1e-4);
        }

        [Test]
        public void Parse_ValidISSTLE_ReturnsCorrectRevolutionNumber()
        {
            var tle = TLEParser.Parse(ISSName, ISSLine1, ISSLine2);
            Assert.AreEqual(43361, tle.RevolutionNumber);
        }

        // ------------------------------------------------------------------ epoch

        [Test]
        public void Parse_Epoch_ReturnsYear2024()
        {
            var tle = TLEParser.Parse(ISSName, ISSLine1, ISSLine2);
            // Line 1 epoch "24001.5" → year 2024, day 1.5 = Jan 1 12:00 UTC
            Assert.AreEqual(2024, tle.Epoch.Year);
            Assert.AreEqual(1, tle.Epoch.Month);
            Assert.AreEqual(1, tle.Epoch.Day);
        }

        [Test]
        public void Parse_EpochYear57_MapsTo1957()
        {
            // Year code 57 → 1957 (Sputnik era boundary)
            const string line1 = "1 00001U 57001A   57275.00000000  .00000000  00000-0  00000-0 0  9990";
            const string line2 = "2 00001  65.1000  64.5000 0300000  64.0000  90.0000 14.36363636000014";
            var tle = TLEParser.Parse("TEST", line1, line2);
            Assert.AreEqual(1957, tle.Epoch.Year);
        }

        [Test]
        public void Parse_EpochYear00_MapsTo2000()
        {
            const string line1 = "1 00001U 57001A   00001.00000000  .00000000  00000-0  00000-0 0  9995";
            const string line2 = "2 00001  65.1000  64.5000 0300000  64.0000  90.0000 14.36363636000014";
            var tle = TLEParser.Parse("TEST", line1, line2);
            Assert.AreEqual(2000, tle.Epoch.Year);
        }

        // ------------------------------------------------------------------ validation / exceptions

        [Test]
        public void Parse_EmptyLine1_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(
                () => TLEParser.Parse("X", "", ISSLine2));
        }

        [Test]
        public void Parse_EmptyLine2_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(
                () => TLEParser.Parse("X", ISSLine1, ""));
        }

        [Test]
        public void Parse_ShortLine1_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(
                () => TLEParser.Parse("X", "1 25544", ISSLine2));
        }

        [Test]
        public void Parse_BadChecksum_ThrowsArgumentException()
        {
            // Flip last digit of line 1 checksum (correct is '7', we set it to '0')
            string badLine1 = ISSLine1.Substring(0, 68) + "0"; // actual checksum is 7
            Assert.Throws<ArgumentException>(
                () => TLEParser.Parse("X", badLine1, ISSLine2));
        }

        [Test]
        public void Parse_NullName_AcceptsAndReturnsEmpty()
        {
            var tle = TLEParser.Parse(null, ISSLine1, ISSLine2);
            Assert.AreEqual(string.Empty, tle.Name);
        }

        // ------------------------------------------------------------------ classification

        [Test]
        public void Parse_Classification_IsU()
        {
            var tle = TLEParser.Parse(ISSName, ISSLine1, ISSLine2);
            Assert.AreEqual('U', tle.Classification, "ISS TLE classification should be 'U' (Unclassified)");
        }
    }
}
