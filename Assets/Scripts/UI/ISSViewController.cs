using System;
using System.Collections;
using Gix.Core;
using Gix.Data;
using Gix.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Gix.UI
{
    /// <summary>
    /// "What does the ISS see?" feature panel.
    ///
    /// When the user taps the ISS:
    ///   1. The backend is queried for the ISS's current geographic position.
    ///   2. A miniature Earth map shows the ISS ground track.
    ///   3. The AR view switches to an ISS-perspective simulation,
    ///      rendering the star field as seen from LEO altitude (~410 km).
    ///
    /// The star field from ISS orbit is identical to Earth's view for distant
    /// stars (parallax negligible), so we reuse the same rendering pipeline.
    /// </summary>
    public class ISSViewController : MonoBehaviour
    {
        // ------------------------------------------------------------------ inspector

        [Header("Backend")]
        [Tooltip("URL template for ISS position API. {0} = UTC timestamp (seconds).")]
        [SerializeField] private string _issApiUrl = "https://api.wheretheiss.at/v1/satellites/25544";

        [Header("Mini-Map")]
        [SerializeField] private RawImage _miniMapImage;
        [SerializeField] private RectTransform _issMarker;
        [SerializeField] private RectTransform _issGroundTrackContainer;

        [Header("ISS View Panel")]
        [SerializeField] private GameObject _issViewPanel;
        [SerializeField] private TMPro.TMP_Text _positionLabel;
        [SerializeField] private TMPro.TMP_Text _altitudeLabel;
        [SerializeField] private TMPro.TMP_Text _speedLabel;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _toggleLiveFeedButton;

        [Header("Update Rate")]
        [SerializeField, Range(1f, 30f)] private float _updateIntervalSeconds = 5f;

        // ------------------------------------------------------------------ runtime

        private TLEElements _issTLE;
        private bool _isActive;
        private Coroutine _updateCoroutine;

        // Live telemetry (fetched from backend or computed from TLE)
        private double _issLatDeg;
        private double _issLonDeg;
        private double _issAltKm;

        // Observer location (for satellite-perspective calculation)
        private ObserverLocation _observer;

        // ------------------------------------------------------------------ events

        /// <summary>Fired when ISS position is updated.</summary>
        public event Action<double, double, double> OnISSPositionUpdated;

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            _closeButton?.onClick.AddListener(Deactivate);
            _toggleLiveFeedButton?.onClick.AddListener(ToggleLiveFeed);
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_updateCoroutine != null) StopCoroutine(_updateCoroutine);
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Activates the ISS view for the given TLE and observer.</summary>
        public void Activate(TLEElements issTLE, ObserverLocation observer)
        {
            _issTLE = issTLE;
            _observer = observer;
            _isActive = true;
            gameObject.SetActive(true);

            if (_updateCoroutine != null) StopCoroutine(_updateCoroutine);
            _updateCoroutine = StartCoroutine(UpdateLoop());
        }

        /// <summary>Deactivates the ISS view panel.</summary>
        public void Deactivate()
        {
            _isActive = false;
            if (_updateCoroutine != null) StopCoroutine(_updateCoroutine);
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ update loop

        private IEnumerator UpdateLoop()
        {
            while (_isActive)
            {
                UpdateFromTLE();
                UpdateUI();
                yield return new WaitForSeconds(_updateIntervalSeconds);
            }
        }

        /// <summary>
        /// Computes ISS position from TLE (used when no network is available or in offline mode).
        /// </summary>
        private void UpdateFromTLE()
        {
            bool ok = SGP4Calculator.GetGeodeticPosition(
                _issTLE, DateTime.UtcNow,
                out double lat, out double lon, out double alt);

            if (ok)
            {
                _issLatDeg = lat;
                _issLonDeg = lon;
                _issAltKm = alt;
                OnISSPositionUpdated?.Invoke(lat, lon, alt);
            }
        }

        private void UpdateUI()
        {
            if (_positionLabel != null)
                _positionLabel.text = $"{FormatCoord(_issLatDeg, 'N', 'S')}  {FormatCoord(_issLonDeg, 'E', 'W')}";

            if (_altitudeLabel != null)
                _altitudeLabel.text = $"Alt: {_issAltKm:F0} km";

            UpdateMiniMapMarker();
        }

        private void UpdateMiniMapMarker()
        {
            if (_issMarker == null || _miniMapImage == null) return;

            // Map lat/lon to normalised UV in the mini-map texture
            // (assumes equirectangular projection)
            float u = (float)((_issLonDeg + 180.0) / 360.0);
            float v = (float)((_issLatDeg + 90.0) / 180.0);

            RectTransform mapRect = _miniMapImage.rectTransform;
            Vector2 anchoredPos = new Vector2(
                (u - 0.5f) * mapRect.rect.width,
                (v - 0.5f) * mapRect.rect.height);

            _issMarker.anchoredPosition = anchoredPos;
        }

        private void ToggleLiveFeed()
        {
            // Placeholder: toggle between TLE-computed and NASA live camera feed
            Debug.Log("[Gix] Live ISS feed toggle (requires premium subscription).");
        }

        // ------------------------------------------------------------------ helpers

        private static string FormatCoord(double deg, char pos, char neg)
        {
            char dir = deg >= 0 ? pos : neg;
            return $"{Math.Abs(deg):F2}°{dir}";
        }
    }
}
