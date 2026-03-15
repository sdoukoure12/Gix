using System;
using System.Collections.Generic;
using Gix.Core;
using Gix.Data;
using Gix.Utils;
using UnityEngine;

namespace Gix.Rendering
{
    /// <summary>
    /// Renders active satellites as labelled icons in the AR view.
    ///
    /// Each visible satellite is represented by:
    ///   - A 3D icon (billboard quad) colour-coded by category.
    ///   - A world-space label (TextMeshPro in world-space canvas) stabilised
    ///     with a per-satellite <see cref="KalmanFilter2D"/>.
    ///   - An optional trajectory line showing the ground track.
    /// </summary>
    public class SatelliteRenderer : MonoBehaviour
    {
        // ------------------------------------------------------------------ inspector

        [Header("Prefabs")]
        [SerializeField] private GameObject _satelliteIconPrefab;
        [SerializeField] private GameObject _labelPrefab;

        [Header("Appearance")]
        [SerializeField, Range(50f, 1000f)] private float _celestialSphereRadius = 100f;
        [SerializeField] private Color _activeColor = Color.green;
        [SerializeField] private Color _debrisColor = Color.red;
        [SerializeField] private Color _defaultColor = Color.cyan;

        [Header("Kalman Smoothing")]
        [SerializeField] private float _kalmanProcessNoise = 0.01f;
        [SerializeField] private float _kalmanMeasurementNoise = 2.0f;

        // ------------------------------------------------------------------ runtime

        private Camera _arCamera;

        // Per-satellite rendering state
        private readonly Dictionary<int, SatelliteVisual> _visuals
            = new Dictionary<int, SatelliteVisual>();

        private readonly struct SatVisualPosition
        {
            public readonly double Azimuth;
            public readonly double Elevation;
            public readonly double Range;
            public readonly bool Visible;

            public SatVisualPosition(double az, double el, double range, bool visible)
            { Azimuth = az; Elevation = el; Range = range; Visible = visible; }
        }

        private class SatelliteVisual
        {
            public GameObject Icon;
            public GameObject Label;
            public KalmanFilter2D ScreenFilter = new KalmanFilter2D(0.01f, 2.0f);
            public bool WasVisible;
        }

        // ------------------------------------------------------------------ public API

        public void SetCamera(Camera arCamera) => _arCamera = arCamera;

        /// <summary>
        /// Updates satellite visuals for the current frame.
        /// </summary>
        /// <param name="satellites">Active satellites to render.</param>
        /// <param name="positions">
        /// Pre-computed azimuth/elevation for each satellite (parallel array to satellites).
        /// </param>
        public void Render(IReadOnlyList<SatelliteData> satellites,
                           IReadOnlyList<(double az, double el, double range)> positions)
        {
            if (satellites == null || positions == null) return;

            HashSet<int> activeIds = new HashSet<int>();

            for (int i = 0; i < satellites.Count; i++)
            {
                var sat = satellites[i];
                var (az, el, range) = positions[i];
                activeIds.Add(sat.NoradId);

                bool visible = el > 0;
                UpdateVisual(sat, az, el, visible);
            }

            // Remove visuals for satellites that are no longer active
            var toRemove = new List<int>();
            foreach (var kv in _visuals)
                if (!activeIds.Contains(kv.Key)) toRemove.Add(kv.Key);
            foreach (int id in toRemove) RemoveVisual(id);
        }

        // ------------------------------------------------------------------ helpers

        private void UpdateVisual(SatelliteData sat, double azDeg, double elDeg, bool visible)
        {
            if (!_visuals.TryGetValue(sat.NoradId, out var visual))
            {
                visual = CreateVisual(sat);
                _visuals[sat.NoradId] = visual;
            }

            visual.Icon.SetActive(visible);
            visual.Label.SetActive(visible);

            if (!visible) return;

            Vector3 worldPos = HorizontalToWorld(azDeg, elDeg) * _celestialSphereRadius;
            visual.Icon.transform.position = worldPos;

            // Billboard: face the camera
            if (_arCamera != null)
                visual.Icon.transform.LookAt(
                    worldPos + _arCamera.transform.rotation * Vector3.forward,
                    _arCamera.transform.up);

            // Apply Kalman-smoothed screen position to label
            if (_arCamera != null)
            {
                Vector3 screenPos = _arCamera.WorldToScreenPoint(worldPos);
                Vector2 smoothed = visual.ScreenFilter.Update(new Vector2(screenPos.x, screenPos.y));
                // Convert back to world pos for the label offset
                Vector3 labelScreen = new Vector3(smoothed.x, smoothed.y + 20f, screenPos.z);
                visual.Label.transform.position =
                    _arCamera.ScreenToWorldPoint(labelScreen);
            }
        }

        private SatelliteVisual CreateVisual(SatelliteData sat)
        {
            var icon = _satelliteIconPrefab != null
                ? Instantiate(_satelliteIconPrefab, transform)
                : CreateDefaultIcon(sat);

            var label = _labelPrefab != null
                ? Instantiate(_labelPrefab, transform)
                : CreateDefaultLabel(sat.Name);

            // Color by category
            var renderer = icon.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Color c = sat.Category == SatelliteCategory.Debris ? _debrisColor
                        : sat.Category == SatelliteCategory.Station ? _activeColor
                        : _defaultColor;
                renderer.material.color = c;
            }

            return new SatelliteVisual
            {
                Icon = icon,
                Label = label,
                ScreenFilter = new KalmanFilter2D(_kalmanProcessNoise, _kalmanMeasurementNoise)
            };
        }

        private void RemoveVisual(int noradId)
        {
            if (!_visuals.TryGetValue(noradId, out var v)) return;
            if (v.Icon != null) Destroy(v.Icon);
            if (v.Label != null) Destroy(v.Label);
            _visuals.Remove(noradId);
        }

        private GameObject CreateDefaultIcon(SatelliteData sat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = $"Satellite_{sat.NoradId}";
            go.transform.SetParent(transform);
            go.transform.localScale = Vector3.one * 0.5f;
            return go;
        }

        private GameObject CreateDefaultLabel(string text)
        {
            var go = new GameObject($"Label_{text}");
            go.transform.SetParent(transform);
            return go;
        }

        private static Vector3 HorizontalToWorld(double azDeg, double altDeg)
        {
            float az = (float)(azDeg * Mathf.Deg2Rad);
            float alt = (float)(altDeg * Mathf.Deg2Rad);
            float cosAlt = Mathf.Cos(alt);
            return new Vector3(cosAlt * Mathf.Sin(az), Mathf.Sin(alt), cosAlt * Mathf.Cos(az));
        }
    }
}
