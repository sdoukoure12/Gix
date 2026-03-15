using System.Collections.Generic;
using Gix.Core;
using Gix.Data;
using UnityEngine;

namespace Gix.Rendering
{
    /// <summary>
    /// Renders all visible stars using GPU instancing (single draw call per LOD level).
    ///
    /// Stars are rendered as billboarded quads on the unit celestial sphere.
    /// Positions are computed by <see cref="StarCatalog.PrecomputePositions"/>
    /// and driven by the AR camera orientation.
    ///
    /// LOD policy:
    ///   LOD 0 (mag &lt; 3)   → detailed sprite quad + label  (bright stars)
    ///   LOD 1 (mag 3–6)   → simple point quad              (dim stars)
    ///   LOD 2 (mag &gt; 6)  → not rendered in AR
    /// </summary>
    public class StarRenderer : MonoBehaviour
    {
        // ------------------------------------------------------------------ inspector fields

        [Header("Materials (GPU Instancing must be enabled)")]
        [SerializeField] private Material _brightStarMaterial;
        [SerializeField] private Material _dimStarMaterial;

        [Header("Rendering")]
        [SerializeField] private Mesh _quadMesh;
        [SerializeField, Range(0.001f, 0.05f)] private float _brightStarSize = 0.012f;
        [SerializeField, Range(0.0005f, 0.02f)] private float _dimStarSize = 0.003f;

        [Header("Celestial Sphere")]
        [SerializeField, Range(50f, 1000f)] private float _celestialSphereRadius = 100f;

        // ------------------------------------------------------------------ runtime state

        private StarCatalog _catalog;

        // Instancing buffers
        private readonly List<Matrix4x4> _brightMatrices = new List<Matrix4x4>(256);
        private readonly List<Vector4> _brightColors = new List<Vector4>(256);
        private readonly List<Matrix4x4> _dimMatrices = new List<Matrix4x4>(4096);

        // Scratch arrays for DrawMeshInstanced batches (max 1023 per call)
        private static readonly Matrix4x4[] _batchBuffer = new Matrix4x4[1023];
        private static readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
        private static readonly List<Vector4> _colorBatch = new List<Vector4>(1023);

        // ------------------------------------------------------------------ public API

        /// <summary>Assigns the star catalog that this renderer will use.</summary>
        public void SetCatalog(StarCatalog catalog) => _catalog = catalog;

        /// <summary>
        /// Renders all visible stars for the current frame.
        /// Must be called after <see cref="StarCatalog.PrecomputePositions"/>.
        /// </summary>
        /// <param name="camera">The AR camera used for frustum culling.</param>
        public void Render(Camera camera)
        {
            if (_catalog == null || _quadMesh == null) return;

            BuildInstanceData(camera);
            DrawBright();
            DrawDim();
        }

        // ------------------------------------------------------------------ private helpers

        private void BuildInstanceData(Camera camera)
        {
            _brightMatrices.Clear();
            _brightColors.Clear();
            _dimMatrices.Clear();

            var frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);

            // LOD 0 – bright stars
            foreach (int idx in _catalog.GetLOD0Indices())
            {
                var coord = _catalog.GetPrecomputedCoord(idx);
                if (!coord.IsAboveHorizon) continue;

                Vector3 dir = HorizontalToWorld(coord.Azimuth, coord.Altitude);
                Vector3 pos = dir * _celestialSphereRadius;

                if (!IsInFrustum(pos, frustumPlanes)) continue;

                var star = _catalog.GetStar(idx);
                Matrix4x4 m = Matrix4x4.TRS(pos,
                    Quaternion.LookRotation(-dir, camera.transform.up),
                    Vector3.one * _brightStarSize);

                _brightMatrices.Add(m);
                _brightColors.Add(star.GetColor());
            }

            // LOD 1 – dim stars
            foreach (int idx in _catalog.GetLOD1Indices())
            {
                var coord = _catalog.GetPrecomputedCoord(idx);
                if (!coord.IsAboveHorizon) continue;

                Vector3 dir = HorizontalToWorld(coord.Azimuth, coord.Altitude);
                Vector3 pos = dir * _celestialSphereRadius;

                if (!IsInFrustum(pos, frustumPlanes)) continue;

                _dimMatrices.Add(Matrix4x4.TRS(pos,
                    Quaternion.LookRotation(-dir, camera.transform.up),
                    Vector3.one * _dimStarSize));
            }
        }

        private void DrawBright()
        {
            if (_brightMatrices.Count == 0 || _brightStarMaterial == null) return;

            // Draw in batches of 1023 (Unity limit for DrawMeshInstanced)
            for (int i = 0; i < _brightMatrices.Count; i += 1023)
            {
                int count = Mathf.Min(1023, _brightMatrices.Count - i);
                for (int j = 0; j < count; j++) _batchBuffer[j] = _brightMatrices[i + j];

                _colorBatch.Clear();
                for (int j = 0; j < count; j++) _colorBatch.Add(_brightColors[i + j]);

                _mpb.SetVectorArray("_Color", _colorBatch);
                Graphics.DrawMeshInstanced(_quadMesh, 0, _brightStarMaterial,
                    _batchBuffer, count, _mpb);
            }
        }

        private void DrawDim()
        {
            if (_dimMatrices.Count == 0 || _dimStarMaterial == null) return;

            for (int i = 0; i < _dimMatrices.Count; i += 1023)
            {
                int count = Mathf.Min(1023, _dimMatrices.Count - i);
                for (int j = 0; j < count; j++) _batchBuffer[j] = _dimMatrices[i + j];
                Graphics.DrawMeshInstanced(_quadMesh, 0, _dimStarMaterial, _batchBuffer, count);
            }
        }

        // ------------------------------------------------------------------ coordinate helpers

        /// <summary>
        /// Converts azimuth/altitude (horizontal coordinates) to a world-space unit direction.
        /// Convention: +Z is zenith, +X is East, -Z is North in Unity world space.
        /// (Adjusted so that Azimuth 0 = North in a standard AR coordinate frame.)
        /// </summary>
        private static Vector3 HorizontalToWorld(double azDeg, double altDeg)
        {
            float az = (float)(azDeg * Mathf.Deg2Rad);
            float alt = (float)(altDeg * Mathf.Deg2Rad);
            float cosAlt = Mathf.Cos(alt);
            // Azimuth: 0° = North (+Z), 90° = East (+X) in right-handed Y-up
            return new Vector3(
                cosAlt * Mathf.Sin(az),
                Mathf.Sin(alt),
                cosAlt * Mathf.Cos(az));
        }

        private static bool IsInFrustum(Vector3 pos, Plane[] planes)
        {
            foreach (var p in planes)
                if (p.GetDistanceToPoint(pos) < 0f) return false;
            return true;
        }
    }
}
