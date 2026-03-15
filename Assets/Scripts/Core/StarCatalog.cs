using System;
using System.Collections.Generic;
using Gix.Data;
using UnityEngine;

namespace Gix.Core
{
    /// <summary>
    /// Manages the full star catalog and provides efficient lookup,
    /// filtering by magnitude (LOD) and pre-computation of nightly positions.
    ///
    /// The catalog is partitioned by LOD level:
    ///   LOD 0 — magnitude &lt; 3   (bright stars with labels)
    ///   LOD 1 — magnitude 3–6    (simple point lights)
    ///   LOD 2 — magnitude &gt; 6   (map mode only; not rendered in AR)
    /// </summary>
    public class StarCatalog
    {
        // ------------------------------------------------------------------ fields

        private readonly StarData[] _stars;
        private readonly Dictionary<int, int> _hipIndex = new Dictionary<int, int>(); // HIP → array index
        private readonly int[] _lod0Indices;
        private readonly int[] _lod1Indices;

        // Pre-computed horizontal positions (populated by PrecomputePositions)
        private HorizontalCoordinates[] _precomputedCoords;
        private DateTime _precomputeTime;
        private double _precomputeLat;
        private double _precomputeLon;

        // ------------------------------------------------------------------ constructor

        /// <summary>
        /// Creates a new catalog from a raw star array.
        /// </summary>
        public StarCatalog(StarData[] stars)
        {
            if (stars == null) throw new ArgumentNullException(nameof(stars));
            _stars = stars;

            var lod0 = new List<int>();
            var lod1 = new List<int>();

            for (int i = 0; i < _stars.Length; i++)
            {
                _hipIndex[_stars[i].HipId] = i;
                int lod = _stars[i].GetLODLevel();
                if (lod == 0) lod0.Add(i);
                else if (lod == 1) lod1.Add(i);
            }

            _lod0Indices = lod0.ToArray();
            _lod1Indices = lod1.ToArray();
        }

        // ------------------------------------------------------------------ properties

        /// <summary>Total number of stars in the catalog.</summary>
        public int Count => _stars.Length;

        /// <summary>Number of bright (LOD 0) stars.</summary>
        public int BrightStarCount => _lod0Indices.Length;

        /// <summary>Number of medium (LOD 1) stars.</summary>
        public int MediumStarCount => _lod1Indices.Length;

        // ------------------------------------------------------------------ lookup

        /// <summary>Returns the star at the given catalog index.</summary>
        public ref readonly StarData GetStar(int index) => ref _stars[index];

        /// <summary>
        /// Finds a star by its Hipparcos catalog number.
        /// Returns false if not found.
        /// </summary>
        public bool TryGetByHip(int hipId, out StarData star)
        {
            if (_hipIndex.TryGetValue(hipId, out int idx))
            {
                star = _stars[idx];
                return true;
            }
            star = default;
            return false;
        }

        /// <summary>
        /// Returns the catalog index for a given HIP identifier.
        /// Returns -1 if not found.
        /// </summary>
        public int GetIndexByHip(int hipId)
            => _hipIndex.TryGetValue(hipId, out int idx) ? idx : -1;

        /// <summary>
        /// Returns the indices of all LOD-0 (bright) stars.
        /// </summary>
        public ReadOnlySpan<int> GetLOD0Indices() => _lod0Indices;

        /// <summary>
        /// Returns the indices of all LOD-1 (medium) stars.
        /// </summary>
        public ReadOnlySpan<int> GetLOD1Indices() => _lod1Indices;

        // ------------------------------------------------------------------ pre-computation

        /// <summary>
        /// Pre-computes horizontal coordinates for all bright + medium stars
        /// for a specific observer and time.
        ///
        /// Call this once per frame update (or more rarely for static stars)
        /// on a background thread to keep the render thread fast.
        /// </summary>
        /// <param name="observerLatDeg">Observer latitude in degrees.</param>
        /// <param name="observerLonDeg">Observer longitude in degrees.</param>
        /// <param name="utc">UTC time for computation.</param>
        public void PrecomputePositions(double observerLatDeg, double observerLonDeg, DateTime utc)
        {
            if (_precomputedCoords == null || _precomputedCoords.Length < _stars.Length)
                _precomputedCoords = new HorizontalCoordinates[_stars.Length];

            _precomputeLat = observerLatDeg;
            _precomputeLon = observerLonDeg;
            _precomputeTime = utc;

            // Pre-compute all LOD-0 and LOD-1 stars
            foreach (int i in _lod0Indices)
                _precomputedCoords[i] = ComputeCoords(_stars[i], observerLatDeg, observerLonDeg, utc);
            foreach (int i in _lod1Indices)
                _precomputedCoords[i] = ComputeCoords(_stars[i], observerLatDeg, observerLonDeg, utc);
        }

        /// <summary>
        /// Returns the pre-computed horizontal coordinate for a given star index.
        /// Call <see cref="PrecomputePositions"/> first.
        /// </summary>
        public HorizontalCoordinates GetPrecomputedCoord(int starIndex)
        {
            if (_precomputedCoords == null)
                throw new InvalidOperationException("Call PrecomputePositions() first.");
            return _precomputedCoords[starIndex];
        }

        // ------------------------------------------------------------------ helpers

        private static HorizontalCoordinates ComputeCoords(
            in StarData star, double lat, double lon, DateTime utc)
        {
            var (az, alt) = CelestialCalculator.EquatorialToHorizontal(
                star.RightAscension, star.Declination, lat, lon, utc);
            return new HorizontalCoordinates { Azimuth = az, Altitude = alt };
        }
    }
}
