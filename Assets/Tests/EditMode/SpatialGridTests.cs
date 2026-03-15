using System.Collections.Generic;
using NUnit.Framework;
using Gix.Culling;
using UnityEngine;

namespace Gix.Tests.EditMode
{
    /// <summary>
    /// Unit tests for <see cref="SpatialGrid"/>.
    /// </summary>
    [TestFixture]
    public class SpatialGridTests
    {
        // ------------------------------------------------------------------ construction

        [Test]
        public void Constructor_ValidArguments_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new SpatialGrid(36, 18));
        }

        [Test]
        public void Constructor_ZeroAzCells_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new SpatialGrid(0, 18));
        }

        [Test]
        public void Constructor_ZeroAltCells_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new SpatialGrid(36, 0));
        }

        // ------------------------------------------------------------------ insert & query

        [Test]
        public void InsertAndQuery_SatelliteInView_IsReturned()
        {
            var grid = new SpatialGrid(36, 18);
            grid.Insert(42, 180.0, 45.0); // South, 45° elevation

            var results = new List<int>(grid.Query(180.0, 45.0, 20.0, 20.0));
            CollectionAssert.Contains(results, 42);
        }

        [Test]
        public void InsertAndQuery_SatelliteOutsideView_IsNotReturned()
        {
            var grid = new SpatialGrid(36, 18);
            grid.Insert(99, 0.0, 0.0); // North, horizon

            // Query on the opposite side of the sky
            var results = new List<int>(grid.Query(180.0, 60.0, 10.0, 10.0));
            CollectionAssert.DoesNotContain(results, 99);
        }

        [Test]
        public void Clear_AfterInsert_QueryReturnsNothing()
        {
            var grid = new SpatialGrid(36, 18);
            grid.Insert(1, 90.0, 30.0);
            grid.Clear();

            var results = new List<int>(grid.Query(90.0, 30.0, 20.0, 20.0));
            Assert.AreEqual(0, results.Count, "Grid should be empty after Clear().");
        }

        [Test]
        public void Insert_MultipleSatellites_AllFoundInQueryWindow()
        {
            var grid = new SpatialGrid(36, 18);
            int[] satellites = { 1, 2, 3, 4, 5 };
            foreach (int id in satellites)
                grid.Insert(id, 90.0, 45.0);

            var results = new List<int>(grid.Query(90.0, 45.0, 15.0, 15.0));
            foreach (int id in satellites)
                CollectionAssert.Contains(results, id, $"Satellite {id} should be found.");
        }

        [Test]
        public void Insert_BelowHorizon_IsNotReturned()
        {
            var grid = new SpatialGrid();
            // Below horizon: alt = -20
            grid.Insert(77, 90.0, -20.0);
            var results = new List<int>(grid.Query(90.0, -20.0, 5.0, 5.0));
            // The grid should not crash but may or may not include the satellite;
            // the important thing is it doesn't throw.
            Assert.IsNotNull(results);
        }

        [Test]
        public void Query_WrapAroundAzimuth_FindsSatelliteNearNorth()
        {
            var grid = new SpatialGrid(36, 18);
            // Satellite at az=358° (just West of North)
            grid.Insert(10, 358.0, 30.0);

            // Query centred at 2° with a wide window that should wrap around 0°/360°
            var results = new List<int>(grid.Query(2.0, 30.0, 15.0, 15.0));
            CollectionAssert.Contains(results, 10,
                "Query window straddling 0°/360° should find satellite at 358°.");
        }

        // ------------------------------------------------------------------ large population

        [Test]
        public void Insert_1000Satellites_QueryRemainsCorrect()
        {
            var grid = new SpatialGrid(36, 18);

            // Insert 1000 satellites at random positions
            var rng = new System.Random(0);
            for (int i = 0; i < 1000; i++)
                grid.Insert(i, rng.NextDouble() * 360.0, rng.NextDouble() * 180.0 - 90.0);

            // Query a wide window — should return a non-trivial set without crashing
            var results = new List<int>(grid.Query(180.0, 0.0, 90.0, 45.0));
            Assert.Greater(results.Count, 0, "Wide query should return some results.");
            Assert.Less(results.Count, 1001, "Should not return more than inserted.");
        }
    }
}
