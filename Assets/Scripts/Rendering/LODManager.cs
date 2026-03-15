using System;
using System.Collections.Generic;
using Gix.Core;
using Gix.Data;
using UnityEngine;

namespace Gix.Rendering
{
    /// <summary>
    /// Manages Level-of-Detail (LOD) decisions for all renderable celestial objects.
    ///
    /// LOD policy:
    ///   · Stars (magnitude &lt; 3)   — bright sprite + label  (always rendered in AR)
    ///   · Stars (magnitude 3–6)   — simple point            (rendered when count below budget)
    ///   · Stars (magnitude &gt; 6)  — not rendered in AR (map mode only)
    ///   · Satellites               — rendered when elevation &gt; horizon; label if el &gt; 5°
    ///
    /// The LOD manager enforces a maximum renderable-object budget to maintain 30+ fps.
    /// </summary>
    public class LODManager
    {
        // ------------------------------------------------------------------ constants

        /// <summary>Maximum total instances rendered in a single frame.</summary>
        public const int MaxInstanceBudget = 5000;

        /// <summary>Minimum elevation angle (degrees) to show a satellite label.</summary>
        public const float SatelliteLabelMinElevation = 5f;

        // ------------------------------------------------------------------ inner types

        /// <summary>LOD result for a single renderable object.</summary>
        public enum LODLevel
        {
            /// <summary>Full quality: sprite, label, glow.</summary>
            Full,
            /// <summary>Reduced: point only, no label.</summary>
            Point,
            /// <summary>Not rendered this frame.</summary>
            Culled
        }

        public struct LODResult
        {
            public int ObjectId;
            public LODLevel Level;
            public bool ShowLabel;
        }

        // ------------------------------------------------------------------ public API

        /// <summary>
        /// Evaluates the LOD level for each visible star, respecting the budget.
        /// </summary>
        /// <param name="catalog">Star catalog.</param>
        /// <param name="visibleBrightIndices">Indices of in-frustum LOD-0 stars.</param>
        /// <param name="visibleDimIndices">Indices of in-frustum LOD-1 stars.</param>
        /// <returns>Per-star LOD decisions.</returns>
        public List<LODResult> EvaluateStars(
            StarCatalog catalog,
            IReadOnlyList<int> visibleBrightIndices,
            IReadOnlyList<int> visibleDimIndices)
        {
            var results = new List<LODResult>(visibleBrightIndices.Count + visibleDimIndices.Count);
            int budget = MaxInstanceBudget;

            // Bright stars always get full detail
            foreach (int idx in visibleBrightIndices)
            {
                if (budget <= 0) break;
                var star = catalog.GetStar(idx);
                results.Add(new LODResult
                {
                    ObjectId = star.HipId,
                    Level = LODLevel.Full,
                    ShowLabel = !string.IsNullOrEmpty(star.Name)
                });
                budget--;
            }

            // Dim stars get point rendering if budget allows
            foreach (int idx in visibleDimIndices)
            {
                if (budget <= 0) break;
                var star = catalog.GetStar(idx);
                results.Add(new LODResult
                {
                    ObjectId = star.HipId,
                    Level = LODLevel.Point,
                    ShowLabel = false
                });
                budget--;
            }

            return results;
        }

        /// <summary>
        /// Evaluates the LOD level for each visible satellite.
        /// </summary>
        public List<LODResult> EvaluateSatellites(
            IReadOnlyList<SatelliteData> satellites,
            IReadOnlyList<(double az, double el, double range)> positions,
            int remainingBudget)
        {
            var results = new List<LODResult>(satellites.Count);

            for (int i = 0; i < satellites.Count; i++)
            {
                if (remainingBudget <= 0) break;
                var sat = satellites[i];
                var (_, el, _) = positions[i];

                if (el < 0) continue; // below horizon

                results.Add(new LODResult
                {
                    ObjectId = sat.NoradId,
                    Level = LODLevel.Full,
                    ShowLabel = el > SatelliteLabelMinElevation
                });
                remainingBudget--;
            }

            return results;
        }
    }
}
