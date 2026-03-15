using System.Collections.Generic;
using Gix.Core;
using Gix.Data;
using UnityEngine;

namespace Gix.Rendering
{
    /// <summary>
    /// Renders constellation line figures by drawing line segments between
    /// pairs of stars defined in <see cref="ConstellationData.LinePairs"/>.
    ///
    /// Each constellation is rendered as a single <see cref="LineRenderer"/>
    /// component to keep draw calls minimal.
    /// </summary>
    public class ConstellationRenderer : MonoBehaviour
    {
        // ------------------------------------------------------------------ inspector fields

        [SerializeField] private Material _lineMaterial;
        [SerializeField, Range(0.001f, 0.05f)] private float _lineWidth = 0.008f;
        [SerializeField] private Color _lineColor = new Color(0.3f, 0.5f, 1.0f, 0.6f);
        [SerializeField, Range(50f, 1000f)] private float _celestialSphereRadius = 100f;

        // ------------------------------------------------------------------ runtime

        private StarCatalog _catalog;
        private ConstellationData[] _constellations;
        private readonly Dictionary<string, LineRenderer> _lineRenderers
            = new Dictionary<string, LineRenderer>();

        // ------------------------------------------------------------------ public API

        /// <summary>Assigns the star catalog and constellation data.</summary>
        public void SetData(StarCatalog catalog, ConstellationData[] constellations)
        {
            _catalog = catalog;
            _constellations = constellations;
            RebuildLineRenderers();
        }

        /// <summary>
        /// Updates the world-space positions of all constellation lines
        /// based on the current pre-computed star positions.
        ///
        /// Call after <see cref="StarCatalog.PrecomputePositions"/>.
        /// </summary>
        public void UpdatePositions()
        {
            if (_catalog == null || _constellations == null) return;

            foreach (var constellation in _constellations)
            {
                if (!_lineRenderers.TryGetValue(constellation.Abbreviation, out var lr)) continue;

                int pairCount = constellation.LinePairs != null ? constellation.LinePairs.Length / 2 : 0;
                lr.positionCount = pairCount * 2;
                int posIdx = 0;

                for (int p = 0; p < pairCount; p++)
                {
                    int hipA = constellation.LinePairs[p * 2];
                    int hipB = constellation.LinePairs[p * 2 + 1];

                    bool okA = TryGetPrecomputedForHip(hipA, out var coordA);
                    bool okB = TryGetPrecomputedForHip(hipB, out var coordB);

                    if (!okA || !okB)
                    {
                        // Degenerate line (star not found) — place at origin so it's invisible
                        lr.SetPosition(posIdx++, Vector3.zero);
                        lr.SetPosition(posIdx++, Vector3.zero);
                        continue;
                    }

                    lr.SetPosition(posIdx++, HorizontalToWorld(coordA.Azimuth, coordA.Altitude));
                    lr.SetPosition(posIdx++, HorizontalToWorld(coordB.Azimuth, coordB.Altitude));
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        private void RebuildLineRenderers()
        {
            // Destroy existing
            foreach (var lr in _lineRenderers.Values)
                if (lr != null) Destroy(lr.gameObject);
            _lineRenderers.Clear();

            if (_constellations == null) return;

            foreach (var constellation in _constellations)
            {
                var go = new GameObject($"Constellation_{constellation.Abbreviation}");
                go.transform.SetParent(transform, false);

                var lr = go.AddComponent<LineRenderer>();
                lr.material = _lineMaterial;
                lr.startWidth = _lineWidth;
                lr.endWidth = _lineWidth;
                lr.startColor = _lineColor;
                lr.endColor = _lineColor;
                lr.useWorldSpace = true;
                lr.numCapVertices = 2;

                _lineRenderers[constellation.Abbreviation] = lr;
            }
        }

        private bool TryGetPrecomputedForHip(int hipId, out HorizontalCoordinates coord)
        {
            int idx = _catalog.GetIndexByHip(hipId);
            if (idx < 0) { coord = default; return false; }
            coord = _catalog.GetPrecomputedCoord(idx);
            return true;
        }

        private Vector3 HorizontalToWorld(double azDeg, double altDeg)
        {
            float az = (float)(azDeg * Mathf.Deg2Rad);
            float alt = (float)(altDeg * Mathf.Deg2Rad);
            float cosAlt = Mathf.Cos(alt);
            return new Vector3(
                cosAlt * Mathf.Sin(az),
                Mathf.Sin(alt),
                cosAlt * Mathf.Cos(az)) * _celestialSphereRadius;
        }
    }
}
