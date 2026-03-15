using System.Collections.Generic;
using Gix.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Gix.UI
{
    /// <summary>
    /// AR mode overlay HUD.
    ///
    /// Displays a minimal translucent toolbar at the bottom of the screen with:
    ///   - Screenshot button
    ///   - Object list toggle
    ///   - Filter picker (Stars / Planets / Satellites / All)
    ///   - Quick settings (label brightness, night mode)
    ///   - Night mode indicator
    ///
    /// Automatically activates Night Mode (red labels) after sunset.
    /// </summary>
    public class AROverlay : MonoBehaviour
    {
        // ------------------------------------------------------------------ inspector

        [Header("Toolbar Buttons")]
        [SerializeField] private Button _screenshotButton;
        [SerializeField] private Button _objectListButton;
        [SerializeField] private Button _filterButton;
        [SerializeField] private Button _settingsButton;

        [Header("Night Mode")]
        [SerializeField] private GameObject _nightModeIndicator;
        [SerializeField] private Color _dayLabelColor = Color.white;
        [SerializeField] private Color _nightLabelColor = new Color(0.9f, 0.1f, 0.1f, 1f);

        [Header("Filter Panel")]
        [SerializeField] private GameObject _filterPanel;
        [SerializeField] private Toggle _showStarsToggle;
        [SerializeField] private Toggle _showSatellitesToggle;
        [SerializeField] private Toggle _showPlanetsToggle;

        [Header("Object List Panel")]
        [SerializeField] private GameObject _objectListPanel;
        [SerializeField] private Transform _objectListContent;
        [SerializeField] private GameObject _objectListItemPrefab;

        // ------------------------------------------------------------------ runtime

        private bool _isNightMode;
        private bool _filterPanelOpen;
        private bool _objectListOpen;

        // ------------------------------------------------------------------ filter state (readable externally)

        /// <summary>Whether stars should be rendered.</summary>
        public bool ShowStars { get; private set; } = true;

        /// <summary>Whether satellites should be rendered.</summary>
        public bool ShowSatellites { get; private set; } = true;

        /// <summary>Whether planets should be rendered.</summary>
        public bool ShowPlanets { get; private set; } = true;

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            _screenshotButton?.onClick.AddListener(OnScreenshotClicked);
            _objectListButton?.onClick.AddListener(OnObjectListClicked);
            _filterButton?.onClick.AddListener(OnFilterClicked);
            _settingsButton?.onClick.AddListener(OnSettingsClicked);

            _showStarsToggle?.onValueChanged.AddListener(v => ShowStars = v);
            _showSatellitesToggle?.onValueChanged.AddListener(v => ShowSatellites = v);
            _showPlanetsToggle?.onValueChanged.AddListener(v => ShowPlanets = v);

            SetFilterPanelOpen(false);
            SetObjectListOpen(false);
        }

        private void Update()
        {
            UpdateNightMode();
        }

        // ------------------------------------------------------------------ public API

        /// <summary>
        /// Populates the visible-objects list panel with the provided items.
        /// </summary>
        public void PopulateObjectList(IReadOnlyList<string> objectNames)
        {
            if (_objectListContent == null || _objectListItemPrefab == null) return;

            // Clear previous
            foreach (Transform child in _objectListContent)
                Destroy(child.gameObject);

            foreach (var name in objectNames)
            {
                var item = Instantiate(_objectListItemPrefab, _objectListContent);
                var text = item.GetComponentInChildren<TMPro.TMP_Text>();
                if (text != null) text.text = name;
            }
        }

        /// <summary>Forces night mode on or off (overrides automatic detection).</summary>
        public void SetNightMode(bool enabled)
        {
            _isNightMode = enabled;
            ApplyNightMode();
        }

        // ------------------------------------------------------------------ button handlers

        private void OnScreenshotClicked()
        {
            string path = Application.persistentDataPath + "/gix_" +
                System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[Gix] Screenshot saved: {path}");
        }

        private void OnObjectListClicked()
        {
            SetObjectListOpen(!_objectListOpen);
            SetFilterPanelOpen(false);
        }

        private void OnFilterClicked()
        {
            SetFilterPanelOpen(!_filterPanelOpen);
            SetObjectListOpen(false);
        }

        private void OnSettingsClicked()
        {
            // Open quick-settings panel (brightness slider etc.) — deferred
            Debug.Log("[Gix] Settings panel TODO");
        }

        // ------------------------------------------------------------------ helpers

        private void UpdateNightMode()
        {
            // Simple heuristic: use device local time
            int hour = System.DateTime.Now.Hour;
            bool shouldBeNight = hour >= 20 || hour < 6;
            if (shouldBeNight != _isNightMode)
            {
                _isNightMode = shouldBeNight;
                ApplyNightMode();
            }
        }

        private void ApplyNightMode()
        {
            if (_nightModeIndicator != null)
                _nightModeIndicator.SetActive(_isNightMode);
        }

        private void SetFilterPanelOpen(bool open)
        {
            _filterPanelOpen = open;
            if (_filterPanel != null) _filterPanel.SetActive(open);
        }

        private void SetObjectListOpen(bool open)
        {
            _objectListOpen = open;
            if (_objectListPanel != null) _objectListPanel.SetActive(open);
        }
    }
}
