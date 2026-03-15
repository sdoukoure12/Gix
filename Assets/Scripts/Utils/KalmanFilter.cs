using UnityEngine;

namespace Gix.Utils
{
    /// <summary>
    /// Scalar 1D Kalman filter for smoothing noisy measurements.
    /// Used to stabilise projected screen positions of celestial object labels.
    /// </summary>
    public class KalmanFilter
    {
        // State estimate
        private float _x;

        // Error covariance
        private float _p;

        // Process noise covariance (how much we trust the model)
        private readonly float _q;

        // Measurement noise covariance (how much we trust the measurement)
        private readonly float _r;

        private bool _initialised;

        /// <summary>
        /// Creates a new Kalman filter.
        /// </summary>
        /// <param name="processNoise">
        /// Process noise (Q). Higher values make the filter respond faster to changes.
        /// Recommended range: 0.001–0.1 for screen-space positions.
        /// </param>
        /// <param name="measurementNoise">
        /// Measurement noise (R). Higher values smooth more aggressively.
        /// Recommended range: 0.1–10 for screen-space positions.
        /// </param>
        public KalmanFilter(float processNoise = 0.01f, float measurementNoise = 1.0f)
        {
            _q = processNoise;
            _r = measurementNoise;
            _p = 1.0f;
        }

        /// <summary>
        /// Updates the filter with a new measurement and returns the smoothed estimate.
        /// </summary>
        /// <param name="measurement">Raw measured value.</param>
        /// <returns>Kalman-smoothed estimate.</returns>
        public float Update(float measurement)
        {
            if (!_initialised)
            {
                _x = measurement;
                _initialised = true;
                return _x;
            }

            // Predict
            float pPred = _p + _q;

            // Update (Kalman gain)
            float k = pPred / (pPred + _r);
            _x = _x + k * (measurement - _x);
            _p = (1.0f - k) * pPred;

            return _x;
        }

        /// <summary>Current smoothed state estimate.</summary>
        public float State => _x;

        /// <summary>Resets the filter state.</summary>
        public void Reset()
        {
            _initialised = false;
            _p = 1.0f;
        }
    }

    /// <summary>
    /// 2D Kalman filter pair for smoothing Vector2 screen-space positions.
    /// </summary>
    public class KalmanFilter2D
    {
        private readonly KalmanFilter _filterX;
        private readonly KalmanFilter _filterY;

        public KalmanFilter2D(float processNoise = 0.01f, float measurementNoise = 1.0f)
        {
            _filterX = new KalmanFilter(processNoise, measurementNoise);
            _filterY = new KalmanFilter(processNoise, measurementNoise);
        }

        /// <summary>Updates both axes and returns the smoothed position.</summary>
        public Vector2 Update(Vector2 measurement)
            => new Vector2(_filterX.Update(measurement.x), _filterY.Update(measurement.y));

        /// <summary>Resets both axis filters.</summary>
        public void Reset()
        {
            _filterX.Reset();
            _filterY.Reset();
        }
    }
}
