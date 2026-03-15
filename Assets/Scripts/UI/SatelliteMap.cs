using System;
using System.Collections.Generic;
using Gix.Core;
using Gix.Data;
using Gix.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Gix.UI
{
    /// <summary>
    /// 3D satellite map showing a rotating globe with satellite tracks.
    ///
    /// Features:
    ///   - Interactive globe (pinch-to-zoom, drag-to-rotate).
    ///   - Satellite points colour-coded by category (green = active, red = debris).
    ///   - Ground track line for the selected satellite (past 30 min + next 90 min).
    ///   - Time slider to scrub satellite positions through the day.
    /// </summary>
    public class SatelliteMap : MonoBehaviour
    {
        // ------------------------------------------------------------------ inspector

        [Header("Globe")]
        [SerializeField] private Transform _globeTransform;
        [SerializeField] private Material _globeMaterial;
        [SerializeField, Range(0.5f, 5f)] private float _minZoom = 1f;
        [SerializeField, Range(1f, 20f)] private float _maxZoom = 8f;

        [Header("Satellite Points")]
        [SerializeField] private GameObject _satPointPrefab;
        [SerializeField] private Color _activeColor = Color.green;
        [SerializeField] private Color _debrisColor = Color.red;
        [SerializeField] private Color _defaultColor = Color.cyan;

        [Header("Ground Track")]
        [SerializeField] private LineRenderer _groundTrackLine;
        [SerializeField] private int _pastMinutes = 30;
        [SerializeField] private int _futureMinutes = 90;
        [SerializeField] private int _groundTrackSteps = 120;

        [Header("Time Slider")]
        [SerializeField] private Slider _timeSlider;
        [SerializeField] private TMPro.TMP_Text _timeLabel;

        // ------------------------------------------------------------------ runtime

        private List<SatelliteData> _satellites = new List<SatelliteData>();
        private List<TLEElements> _tleElements = new List<TLEElements>();
        private readonly Dictionary<int, GameObject> _satPoints = new Dictionary<int, GameObject>();

        private int _selectedNoradId = -1;
        private float _currentZoom = 3f;
        private Vector3 _lastMousePos;
        private bool _isDragging;
        private float _timeOffsetHours = 0f;  // from _timeSlider

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            _timeSlider?.onValueChanged.AddListener(OnTimeSliderChanged);
        }

        private void Update()
        {
            HandleGlobeInput();
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Loads satellite data into the map.</summary>
        public void SetData(List<SatelliteData> satellites, List<TLEElements> tleElements)
        {
            _satellites = satellites;
            _tleElements = tleElements;

            RebuildSatellitePoints();
            UpdateSatellitePositions();
        }

        /// <summary>Highlights the given satellite and draws its ground track.</summary>
        public void SelectSatellite(int noradId)
        {
            _selectedNoradId = noradId;
            DrawGroundTrack(noradId);
        }

        /// <summary>Updates satellite dot positions for the current slider time.</summary>
        public void UpdateSatellitePositions()
        {
            DateTime utc = DateTime.UtcNow.AddHours(_timeOffsetHours);

            for (int i = 0; i < _tleElements.Count && i < _satellites.Count; i++)
            {
                bool ok = SGP4Calculator.GetGeodeticPosition(
                    _tleElements[i], utc,
                    out double lat, out double lon, out _);

                if (!ok) continue;
                int noradId = _satellites[i].NoradId;
                if (!_satPoints.TryGetValue(noradId, out var point)) continue;

                point.transform.localPosition = LatLonToSphere(lat, lon);
            }
        }

        // ------------------------------------------------------------------ ground track

        private void DrawGroundTrack(int noradId)
        {
            if (_groundTrackLine == null) return;

            int tleIdx = _tleElements.FindIndex(t => t.SatelliteNumber == noradId);
            if (tleIdx < 0) { _groundTrackLine.positionCount = 0; return; }

            var tle = _tleElements[tleIdx];
            DateTime utcNow = DateTime.UtcNow.AddHours(_timeOffsetHours);

            int totalSteps = _groundTrackSteps;
            float totalMinutes = _pastMinutes + _futureMinutes;
            float stepMinutes = totalMinutes / totalSteps;

            var positions = new Vector3[totalSteps + 1];

            for (int s = 0; s <= totalSteps; s++)
            {
                float minuteOffset = -_pastMinutes + s * stepMinutes;
                DateTime t = utcNow.AddMinutes(minuteOffset);

                if (SGP4Calculator.GetGeodeticPosition(tle, t, out double lat, out double lon, out _))
                    positions[s] = LatLonToSphere(lat, lon) * 1.01f; // slightly above surface
                else
                    positions[s] = positions[s > 0 ? s - 1 : 0];
            }

            _groundTrackLine.positionCount = totalSteps + 1;
            _groundTrackLine.SetPositions(positions);
        }

        // ------------------------------------------------------------------ globe interaction

        private void HandleGlobeInput()
        {
            if (_globeTransform == null) return;

            // Mouse/touch drag to rotate
            if (Input.GetMouseButtonDown(0))
            {
                _lastMousePos = Input.mousePosition;
                _isDragging = true;
            }
            if (Input.GetMouseButtonUp(0)) _isDragging = false;

            if (_isDragging && Input.GetMouseButton(0))
            {
                Vector3 delta = Input.mousePosition - _lastMousePos;
                _globeTransform.Rotate(Vector3.up, -delta.x * 0.3f, Space.World);
                _globeTransform.Rotate(Vector3.right, delta.y * 0.3f, Space.World);
                _lastMousePos = Input.mousePosition;
            }

            // Scroll wheel zoom
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _currentZoom = Mathf.Clamp(_currentZoom - scroll * 0.5f, _minZoom, _maxZoom);
                _globeTransform.localScale = Vector3.one * _currentZoom;
            }
        }

        // ------------------------------------------------------------------ helpers

        private void RebuildSatellitePoints()
        {
            foreach (var p in _satPoints.Values)
                if (p != null) Destroy(p);
            _satPoints.Clear();

            if (_satPointPrefab == null || _globeTransform == null) return;

            foreach (var sat in _satellites)
            {
                var go = Instantiate(_satPointPrefab, _globeTransform);
                go.name = $"Sat_{sat.NoradId}";

                // Color by category
                var r = go.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    r.material.color = sat.Category == SatelliteCategory.Debris ? _debrisColor
                                     : sat.Category == SatelliteCategory.Station ? _activeColor
                                     : _defaultColor;
                }

                _satPoints[sat.NoradId] = go;
            }
        }

        private static Vector3 LatLonToSphere(double latDeg, double lonDeg, float radius = 1f)
        {
            float lat = (float)(latDeg * Mathf.Deg2Rad);
            float lon = (float)(lonDeg * Mathf.Deg2Rad);
            return new Vector3(
                Mathf.Cos(lat) * Mathf.Sin(lon),
                Mathf.Sin(lat),
                Mathf.Cos(lat) * Mathf.Cos(lon)) * radius;
        }

        private void OnTimeSliderChanged(float value)
        {
            // Slider maps 0–1 to -12h to +12h from now
            _timeOffsetHours = (value - 0.5f) * 24f;

            DateTime display = DateTime.UtcNow.AddHours(_timeOffsetHours);
            if (_timeLabel != null)
                _timeLabel.text = display.ToString("HH:mm UTC");

            UpdateSatellitePositions();
            if (_selectedNoradId >= 0) DrawGroundTrack(_selectedNoradId);
        }
    }
}
