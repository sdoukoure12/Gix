using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gix.Culling
{
    /// <summary>
    /// Uniform spatial grid for fast culling of moving objects (satellites).
    ///
    /// The sky sphere is divided into cells in azimuth × elevation.
    /// Each satellite is placed in the cell corresponding to its current
    /// azimuth/elevation.  At render time, only cells overlapping the
    /// camera frustum (approximated as an azimuth/elevation window) are queried.
    /// </summary>
    public class SpatialGrid
    {
        // Grid dimensions
        private readonly int _azCells;   // number of cells in azimuth (0–360°)
        private readonly int _altCells;  // number of cells in elevation (-90° to +90°)

        private readonly List<int>[] _cells;  // satellite indices per cell

        /// <summary>
        /// Creates a new spatial grid.
        /// </summary>
        /// <param name="azimuthCells">Number of cells along the azimuth axis (default: 36 = 10°/cell).</param>
        /// <param name="altitudeCells">Number of cells along the altitude axis (default: 18 = 10°/cell).</param>
        public SpatialGrid(int azimuthCells = 36, int altitudeCells = 18)
        {
            if (azimuthCells <= 0) throw new ArgumentOutOfRangeException(nameof(azimuthCells));
            if (altitudeCells <= 0) throw new ArgumentOutOfRangeException(nameof(altitudeCells));

            _azCells = azimuthCells;
            _altCells = altitudeCells;
            _cells = new List<int>[_azCells * _altCells];
            for (int i = 0; i < _cells.Length; i++)
                _cells[i] = new List<int>(4);
        }

        /// <summary>Clears all satellite entries (call once per frame before re-inserting).</summary>
        public void Clear()
        {
            foreach (var cell in _cells) cell.Clear();
        }

        /// <summary>
        /// Inserts a satellite into the grid at the given azimuth/elevation.
        /// </summary>
        /// <param name="satelliteIndex">Index in the active satellite list.</param>
        /// <param name="azimuthDeg">Azimuth in degrees (0–360, N=0 clockwise).</param>
        /// <param name="altitudeDeg">Altitude in degrees (-90 to +90).</param>
        public void Insert(int satelliteIndex, double azimuthDeg, double altitudeDeg)
        {
            int cellIdx = GetCellIndex(azimuthDeg, altitudeDeg);
            if (cellIdx >= 0) _cells[cellIdx].Add(satelliteIndex);
        }

        /// <summary>
        /// Returns satellite indices in cells overlapping the given az/alt window.
        /// </summary>
        /// <param name="centerAzDeg">Centre azimuth of the view window (degrees).</param>
        /// <param name="centerAltDeg">Centre altitude of the view window (degrees).</param>
        /// <param name="halfWidthAzDeg">Half-width of the az window (degrees).</param>
        /// <param name="halfWidthAltDeg">Half-height of the alt window (degrees).</param>
        public IEnumerable<int> Query(
            double centerAzDeg,
            double centerAltDeg,
            double halfWidthAzDeg,
            double halfWidthAltDeg)
        {
            double cellAzWidth = 360.0 / _azCells;
            double cellAltWidth = 180.0 / _altCells;

            // Clamp altitude window to [-90, 90]
            double altMin = Math.Max(-90.0, centerAltDeg - halfWidthAltDeg);
            double altMax = Math.Min(90.0, centerAltDeg + halfWidthAltDeg);

            int altMinCell = AltitudeToCell(altMin);
            int altMaxCell = AltitudeToCell(altMax);

            double azMin = centerAzDeg - halfWidthAzDeg;
            double azMax = centerAzDeg + halfWidthAzDeg;

            // Number of az cells to cover (cap at full circle)
            int azSpan = Math.Min(_azCells, (int)Math.Ceiling((azMax - azMin) / cellAzWidth) + 1);

            for (int ia = 0; ia < azSpan; ia++)
            {
                double az = azMin + ia * cellAzWidth;
                int azCell = AzimuthToCell(az);

                for (int ialt = altMinCell; ialt <= altMaxCell; ialt++)
                {
                    int cellIdx = ialt * _azCells + azCell;
                    foreach (int sat in _cells[cellIdx])
                        yield return sat;
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        private int GetCellIndex(double azDeg, double altDeg)
        {
            if (altDeg < -90.0 || altDeg > 90.0) return -1;
            int azCell = AzimuthToCell(azDeg);
            int altCell = AltitudeToCell(altDeg);
            return altCell * _azCells + azCell;
        }

        private int AzimuthToCell(double azDeg)
        {
            azDeg = azDeg % 360.0;
            if (azDeg < 0) azDeg += 360.0;
            return (int)(azDeg / 360.0 * _azCells) % _azCells;
        }

        private int AltitudeToCell(double altDeg)
        {
            // Map [-90, 90] → [0, _altCells)
            int cell = (int)((altDeg + 90.0) / 180.0 * _altCells);
            return Math.Min(Math.Max(0, cell), _altCells - 1);
        }
    }
}
