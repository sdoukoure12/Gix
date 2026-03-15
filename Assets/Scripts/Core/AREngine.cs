using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Gix.Culling;
using Gix.Data;
using Gix.Rendering;
using Gix.Utils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Gix.Core
{
    /// <summary>
    /// Central AR Engine orchestrating the full rendering pipeline for Gix.
    ///
    /// Pipeline per frame (target &lt; 33 ms total for 30 fps):
    ///   1. Camera/tracking update  — provided by ARFoundation
    ///   2. Compute celestial objects — fetches pre-computed star positions;
    ///                                   runs SGP4 for satellites in real-time
    ///   3. Culling                  — OctreeCuller (stars) + SpatialGrid (satellites)
    ///   4. LOD selection            — LODManager
    ///   5. Render                   — StarRenderer + ConstellationRenderer + SatelliteRenderer
    ///
    /// Star positions are pre-computed on a background thread (worker) to avoid
    /// per-frame cost.  Satellite positions are computed on the main thread using
    /// cached SGP4 state (updated every frame with the precise UTC timestamp).
    /// </summary>
    public class AREngine : MonoBehaviour
    {
        // ------------------------------------------------------------------ inspector

        [Header("AR Foundation")]
        [SerializeField] private ARSession _arSession;
        [SerializeField] private ARCameraManager _cameraManager;
        [SerializeField] private Camera _arCamera;

        [Header("Renderers")]
        [SerializeField] private StarRenderer _starRenderer;
        [SerializeField] private ConstellationRenderer _constellationRenderer;
        [SerializeField] private SatelliteRenderer _satelliteRenderer;

        [Header("Settings")]
        [SerializeField, Range(1, 60)] private int _precomputeIntervalSeconds = 60;
        [SerializeField] private bool _enableConstellations = true;
        [SerializeField] private bool _enableSatellites = true;

        // ------------------------------------------------------------------ runtime state

        private StarCatalog _starCatalog;
        private ConstellationData[] _constellations;
        private List<SatelliteData> _satellites = new List<SatelliteData>();
        private List<TLEElements> _tleElements = new List<TLEElements>();

        private ObserverLocation _observer;
        private readonly LODManager _lodManager = new LODManager();
        private readonly SpatialGrid _satelliteGrid = new SpatialGrid(36, 18);

        // Background pre-computation
        private Thread _precomputeThread;
        private volatile bool _precomputeRunning;
        private DateTime _lastPrecomputeTime;

        // Cached satellite positions (main thread, updated each frame)
        private readonly List<(double az, double el, double range)> _satPositions
            = new List<(double, double, double)>();

        // Pipeline performance metrics
        private float _lastFrameMs;

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            if (_arCamera == null)
                _arCamera = Camera.main;
        }

        private void Start()
        {
            // Kick off the first background pre-computation after catalog is loaded
            SchedulePrecompute();
        }

        private void OnDestroy()
        {
            _precomputeRunning = false;
            _precomputeThread?.Join(1000);
        }

        // ------------------------------------------------------------------ public API

        /// <summary>
        /// Initialises the engine with catalog data.
        /// Call this after loading the catalog from disk/network.
        /// </summary>
        public void Initialise(
            StarCatalog catalog,
            ConstellationData[] constellations,
            List<SatelliteData> satellites,
            List<TLEElements> tleElements,
            ObserverLocation observer)
        {
            _starCatalog = catalog;
            _constellations = constellations;
            _satellites = satellites;
            _tleElements = tleElements;
            _observer = observer;

            _starRenderer?.SetCatalog(catalog);
            _constellationRenderer?.SetData(catalog, constellations);
            _satelliteRenderer?.SetCamera(_arCamera);

            SchedulePrecompute();
        }

        /// <summary>Updates the observer's GPS location.</summary>
        public void SetObserverLocation(double latDeg, double lonDeg, double altMetres)
        {
            _observer = new ObserverLocation
            {
                Latitude = latDeg,
                Longitude = lonDeg,
                AltitudeMetres = altMetres
            };
        }

        // ------------------------------------------------------------------ Unity update loop

        private void Update()
        {
            var frameStart = DateTime.UtcNow;

            if (_starCatalog == null) return;

            // Re-schedule pre-computation if interval elapsed
            if ((DateTime.UtcNow - _lastPrecomputeTime).TotalSeconds > _precomputeIntervalSeconds)
                SchedulePrecompute();

            // 1. Update satellite positions (real-time SGP4 on main thread)
            if (_enableSatellites)
                UpdateSatellitePositions();

            // 2. Render stars
            _starRenderer?.Render(_arCamera);

            // 3. Update constellation lines
            if (_enableConstellations)
                _constellationRenderer?.UpdatePositions();

            // 4. Render satellites
            if (_enableSatellites)
                _satelliteRenderer?.Render(_satellites, _satPositions);

            _lastFrameMs = (float)(DateTime.UtcNow - frameStart).TotalMilliseconds;
        }

        // ------------------------------------------------------------------ satellite positions

        private void UpdateSatellitePositions()
        {
            _satPositions.Clear();
            _satelliteGrid.Clear();

            DateTime utc = DateTime.UtcNow;

            for (int i = 0; i < _tleElements.Count && i < _satellites.Count; i++)
            {
                bool ok = SGP4Calculator.GetHorizontalPosition(
                    _tleElements[i], utc,
                    _observer.Latitude,
                    _observer.Longitude,
                    _observer.AltitudeMetres / 1000.0,
                    out double az, out double el, out double range);

                if (ok)
                {
                    _satPositions.Add((az, el, range));
                    _satelliteGrid.Insert(i, az, el);
                }
                else
                {
                    _satPositions.Add((0, -90, 0));
                }
            }
        }

        // ------------------------------------------------------------------ background pre-compute

        private void SchedulePrecompute()
        {
            if (_precomputeRunning) return;
            if (_starCatalog == null) return;

            _precomputeRunning = true;
            double lat = _observer.Latitude;
            double lon = _observer.Longitude;
            DateTime utc = DateTime.UtcNow;

            _precomputeThread = new Thread(() =>
            {
                try
                {
                    _starCatalog.PrecomputePositions(lat, lon, utc);
                    _lastPrecomputeTime = utc;
                }
                finally
                {
                    _precomputeRunning = false;
                }
            });
            _precomputeThread.IsBackground = true;
            _precomputeThread.Start();
        }

        // ------------------------------------------------------------------ diagnostics

        /// <summary>Last frame pipeline time in milliseconds.</summary>
        public float LastFrameMs => _lastFrameMs;

        /// <summary>Number of currently tracked satellites.</summary>
        public int ActiveSatelliteCount => _satellites.Count;
    }
}
