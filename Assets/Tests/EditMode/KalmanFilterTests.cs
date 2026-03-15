using NUnit.Framework;
using Gix.Utils;
using UnityEngine;

namespace Gix.Tests.EditMode
{
    /// <summary>
    /// Unit tests for <see cref="KalmanFilter"/> and <see cref="KalmanFilter2D"/>.
    /// </summary>
    [TestFixture]
    public class KalmanFilterTests
    {
        // ------------------------------------------------------------------ 1D KalmanFilter

        [Test]
        public void Update_FirstMeasurement_ReturnsExactMeasurement()
        {
            var kf = new KalmanFilter();
            float result = kf.Update(42.0f);
            Assert.AreEqual(42.0f, result, 1e-6f,
                "First update should return the measurement exactly (no prior state).");
        }

        [Test]
        public void Update_ConstantSignal_ConvergesToSignal()
        {
            var kf = new KalmanFilter(processNoise: 0.01f, measurementNoise: 1.0f);
            float signal = 100f;

            for (int i = 0; i < 100; i++)
                kf.Update(signal);

            Assert.AreEqual(signal, kf.State, 0.1f,
                "Filter should converge to a constant signal after many updates.");
        }

        [Test]
        public void Update_NoisySignal_SmoothsOutput()
        {
            var kfSmooth = new KalmanFilter(processNoise: 0.001f, measurementNoise: 10f);
            var kfRaw = new KalmanFilter(processNoise: 100f, measurementNoise: 0.001f);

            // Simulate a noisy step: true value = 50, measurement = 50 ± noise
            float trueValue = 50f;
            float totalSmoothedError = 0f;
            float totalRawError = 0f;

            System.Random rng = new System.Random(42);
            for (int i = 0; i < 200; i++)
            {
                float noise = (float)(rng.NextDouble() * 10 - 5); // ±5 noise
                float meas = trueValue + noise;
                float smoothed = kfSmooth.Update(meas);
                float raw = kfRaw.Update(meas);

                totalSmoothedError += Mathf.Abs(smoothed - trueValue);
                totalRawError += Mathf.Abs(raw - trueValue);
            }

            Assert.Less(totalSmoothedError, totalRawError,
                "Smooth filter should have lower total error than passthrough filter.");
        }

        [Test]
        public void Reset_ClearsState_NextUpdateReturnsRawMeasurement()
        {
            var kf = new KalmanFilter();
            kf.Update(10f);
            kf.Update(20f);
            kf.Update(30f);
            kf.Reset();

            float result = kf.Update(99f);
            Assert.AreEqual(99f, result, 1e-5f,
                "After reset, first update should return raw measurement.");
        }

        [Test]
        public void State_BeforeFirstUpdate_IsZero()
        {
            var kf = new KalmanFilter();
            Assert.AreEqual(0f, kf.State);
        }

        [Test]
        public void Update_StepChange_EventuallyConvergesAfterReset()
        {
            var kf = new KalmanFilter(processNoise: 0.1f, measurementNoise: 1.0f);

            // Converge to 0
            for (int i = 0; i < 50; i++) kf.Update(0f);
            Assert.AreEqual(0f, kf.State, 0.05f);

            // Apply a step to 100 — state should converge
            for (int i = 0; i < 100; i++) kf.Update(100f);
            Assert.AreEqual(100f, kf.State, 1.0f, "Filter should converge after a step change.");
        }

        // ------------------------------------------------------------------ 2D KalmanFilter2D

        [Test]
        public void Filter2D_FirstUpdate_ReturnsExactMeasurement()
        {
            var kf = new KalmanFilter2D();
            var result = kf.Update(new Vector2(10f, 20f));
            Assert.AreEqual(new Vector2(10f, 20f), result,
                "First 2D update should return the raw measurement.");
        }

        [Test]
        public void Filter2D_ConstantPosition_ConvergesToPosition()
        {
            var kf = new KalmanFilter2D(processNoise: 0.01f, measurementNoise: 1.0f);
            var target = new Vector2(300f, 600f);

            for (int i = 0; i < 100; i++) kf.Update(target);

            var result = kf.Update(target);
            Assert.AreEqual(target.x, result.x, 0.5f, "X should converge.");
            Assert.AreEqual(target.y, result.y, 0.5f, "Y should converge.");
        }

        [Test]
        public void Filter2D_Reset_NextUpdateIsRawMeasurement()
        {
            var kf = new KalmanFilter2D();
            kf.Update(new Vector2(1f, 1f));
            kf.Update(new Vector2(2f, 2f));
            kf.Reset();

            var result = kf.Update(new Vector2(99f, 88f));
            Assert.AreEqual(99f, result.x, 1e-5f);
            Assert.AreEqual(88f, result.y, 1e-5f);
        }

        [Test]
        public void Filter2D_AxesAreIndependent()
        {
            var kf = new KalmanFilter2D();
            // Drive X to 100, Y to -100
            for (int i = 0; i < 200; i++) kf.Update(new Vector2(100f, -100f));
            var result = kf.Update(new Vector2(100f, -100f));

            Assert.AreEqual(100f, result.x, 1f, "X axis should converge to 100.");
            Assert.AreEqual(-100f, result.y, 1f, "Y axis should converge to -100.");
        }
    }
}
