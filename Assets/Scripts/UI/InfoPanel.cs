using Gix.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Gix.UI
{
    /// <summary>
    /// Context info panel shown when the user taps a celestial object.
    ///
    /// Displays:
    ///   - Name and type icon
    ///   - Magnitude / brightness (stars) or altitude/elevation (satellites)
    ///   - Distance
    ///   - Short description
    ///   - "Follow" (add to favourites) button
    ///   - For satellites: "Next pass" and "Show trajectory" actions
    /// </summary>
    public class InfoPanel : MonoBehaviour
    {
        // ------------------------------------------------------------------ inspector

        [Header("Common Fields")]
        [SerializeField] private TMPro.TMP_Text _nameText;
        [SerializeField] private TMPro.TMP_Text _subtitleText;
        [SerializeField] private TMPro.TMP_Text _descriptionText;
        [SerializeField] private TMPro.TMP_Text _distanceText;
        [SerializeField] private Image _typeIcon;

        [Header("Star-specific")]
        [SerializeField] private GameObject _starSection;
        [SerializeField] private TMPro.TMP_Text _magnitudeText;
        [SerializeField] private TMPro.TMP_Text _spectralTypeText;

        [Header("Satellite-specific")]
        [SerializeField] private GameObject _satelliteSection;
        [SerializeField] private TMPro.TMP_Text _elevationText;
        [SerializeField] private TMPro.TMP_Text _nextPassText;
        [SerializeField] private Button _showTrajectoryButton;

        [Header("Actions")]
        [SerializeField] private Button _followButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Image _followIcon;
        [SerializeField] private Sprite _followSprite;
        [SerializeField] private Sprite _unfollowSprite;

        [Header("Animation")]
        [SerializeField] private Animator _panelAnimator;
        private static readonly int ShowTrigger = Animator.StringToHash("Show");
        private static readonly int HideTrigger = Animator.StringToHash("Hide");

        // ------------------------------------------------------------------ runtime

        private bool _isFollowing;
        private int _currentNoradId;
        private int _currentHipId;

        public delegate void FollowToggled(bool follow, int id, bool isSatellite);
        public event FollowToggled OnFollowToggled;

        public delegate void TrajectoryRequested(int noradId);
        public event TrajectoryRequested OnTrajectoryRequested;

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            _closeButton?.onClick.AddListener(Hide);
            _followButton?.onClick.AddListener(OnFollowClicked);
            _showTrajectoryButton?.onClick.AddListener(OnShowTrajectoryClicked);
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Shows the panel populated with star information.</summary>
        public void ShowStar(in StarData star, double azimuth, double altitude)
        {
            gameObject.SetActive(true);

            _starSection?.SetActive(true);
            _satelliteSection?.SetActive(false);

            _nameText.text = string.IsNullOrEmpty(star.Name) ? $"HIP {star.HipId}" : star.Name;
            _subtitleText.text = $"Az {azimuth:F1}°  Alt {altitude:F1}°";
            _magnitudeText.text = $"Mag {star.Magnitude:F1}";
            _spectralTypeText.text = $"Type {star.SpectralType}";
            _distanceText.text = star.DistanceLY > 0 ? $"{star.DistanceLY:F0} ly" : "Unknown";
            _descriptionText.text = BuildStarDescription(star);

            _currentHipId = star.HipId;
            _currentNoradId = -1;

            PlayShowAnimation();
        }

        /// <summary>Shows the panel populated with satellite information.</summary>
        public void ShowSatellite(SatelliteData sat,
            double azimuth, double elevation, double rangeKm,
            string nextPassText)
        {
            gameObject.SetActive(true);

            _starSection?.SetActive(false);
            _satelliteSection?.SetActive(true);

            _nameText.text = sat.Name;
            _subtitleText.text = $"NORAD #{sat.NoradId}  ·  {sat.Category}";
            _elevationText.text = $"El {elevation:F1}°  Az {azimuth:F1}°  Range {rangeKm:F0} km";
            _nextPassText.text = nextPassText ?? "—";
            _distanceText.text = $"{rangeKm:F0} km";
            _descriptionText.text = BuildSatelliteDescription(sat);

            _currentNoradId = sat.NoradId;
            _currentHipId = -1;

            PlayShowAnimation();
        }

        /// <summary>Hides and resets the panel.</summary>
        public void Hide()
        {
            if (_panelAnimator != null)
                _panelAnimator.SetTrigger(HideTrigger);
            else
                gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ button handlers

        private void OnFollowClicked()
        {
            _isFollowing = !_isFollowing;
            if (_followIcon != null)
                _followIcon.sprite = _isFollowing ? _unfollowSprite : _followSprite;

            bool isSat = _currentNoradId >= 0;
            int id = isSat ? _currentNoradId : _currentHipId;
            OnFollowToggled?.Invoke(_isFollowing, id, isSat);
        }

        private void OnShowTrajectoryClicked()
        {
            if (_currentNoradId >= 0)
                OnTrajectoryRequested?.Invoke(_currentNoradId);
        }

        // ------------------------------------------------------------------ helpers

        private void PlayShowAnimation()
        {
            if (_panelAnimator != null)
                _panelAnimator.SetTrigger(ShowTrigger);
        }

        private static string BuildStarDescription(in StarData star)
        {
            if (!string.IsNullOrEmpty(star.Name))
                return $"{star.Name} is a {star.SpectralType} star located {star.DistanceLY:F0} light-years away.";
            return $"An unnamed star in the {star.SpectralType} spectral class.";
        }

        private static string BuildSatelliteDescription(SatelliteData sat)
        {
            return $"{sat.Name} is a {sat.Category} satellite tracked by NORAD (#{sat.NoradId}).";
        }
    }
}
