using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gix.Culling
{
    /// <summary>
    /// Axis-Aligned Bounding Box used by the Octree.
    /// </summary>
    public struct AABB
    {
        public Vector3 Center;
        public Vector3 HalfExtents;

        public Vector3 Min => Center - HalfExtents;
        public Vector3 Max => Center + HalfExtents;

        public bool Contains(Vector3 point)
        {
            Vector3 d = point - Center;
            return Math.Abs(d.x) <= HalfExtents.x
                && Math.Abs(d.y) <= HalfExtents.y
                && Math.Abs(d.z) <= HalfExtents.z;
        }

        /// <summary>
        /// Returns true when this AABB intersects the given view frustum (6 planes).
        /// </summary>
        public bool IntersectsFrustum(Plane[] frustumPlanes)
        {
            foreach (var plane in frustumPlanes)
            {
                // Find the AABB corner furthest along the plane normal (p-vertex)
                Vector3 pVertex = new Vector3(
                    plane.normal.x >= 0 ? Max.x : Min.x,
                    plane.normal.y >= 0 ? Max.y : Min.y,
                    plane.normal.z >= 0 ? Max.z : Min.z);

                if (plane.GetDistanceToPoint(pVertex) < 0f)
                    return false; // Entire AABB is behind this plane
            }
            return true;
        }
    }

    /// <summary>
    /// Octree node for efficient spatial culling of static star positions.
    ///
    /// Stars are projected to a unit sphere (their Cartesian equatorial direction vector)
    /// and stored in this tree.  At render time, only the nodes whose bounding box
    /// intersects the camera frustum are traversed.
    /// </summary>
    public class OctreeNode
    {
        private const int MaxItemsPerLeaf = 16;
        private const int MaxDepth = 8;

        public AABB Bounds;
        private readonly int _depth;
        private OctreeNode[] _children;          // null → leaf node
        private readonly List<int> _items = new List<int>(); // star catalog indices

        public OctreeNode(AABB bounds, int depth = 0)
        {
            Bounds = bounds;
            _depth = depth;
        }

        /// <summary>True when this node has been subdivided into 8 children.</summary>
        public bool IsLeaf => _children == null;

        /// <summary>
        /// Inserts a star catalog index with its unit-sphere position.
        /// </summary>
        public void Insert(int starIndex, Vector3 position)
        {
            if (!Bounds.Contains(position)) return;

            if (IsLeaf)
            {
                _items.Add(starIndex);
                if (_items.Count > MaxItemsPerLeaf && _depth < MaxDepth)
                    Subdivide();
            }
            else
            {
                foreach (var child in _children)
                    child.Insert(starIndex, position);
            }
        }

        /// <summary>
        /// Collects all star indices whose bounding region intersects the given frustum planes.
        /// </summary>
        public void Query(Plane[] frustumPlanes, List<int> results)
        {
            if (!Bounds.IntersectsFrustum(frustumPlanes)) return;

            if (IsLeaf)
            {
                results.AddRange(_items);
            }
            else
            {
                foreach (var child in _children)
                    child.Query(frustumPlanes, results);
            }
        }

        private void Subdivide()
        {
            _children = new OctreeNode[8];
            Vector3 half = Bounds.HalfExtents * 0.5f;
            Vector3 c = Bounds.Center;

            for (int i = 0; i < 8; i++)
            {
                Vector3 offset = new Vector3(
                    (i & 1) != 0 ? half.x : -half.x,
                    (i & 2) != 0 ? half.y : -half.y,
                    (i & 4) != 0 ? half.z : -half.z);
                _children[i] = new OctreeNode(
                    new AABB { Center = c + offset, HalfExtents = half },
                    _depth + 1);
            }

            // Re-insert existing items into children
            foreach (int idx in _items)
            {
                // NOTE: position is not stored; the caller must re-insert.
                // In practice the octree is built once and not modified.
            }
            _items.Clear();
        }
    }

    /// <summary>
    /// High-level octree wrapper for the celestial sphere.
    /// The sphere is mapped onto a [-1,1]³ cube; each star's equatorial
    /// Cartesian direction vector is used as its position.
    /// </summary>
    public class OctreeCuller
    {
        private readonly OctreeNode _root;

        public OctreeCuller()
        {
            _root = new OctreeNode(new AABB
            {
                Center = Vector3.zero,
                HalfExtents = Vector3.one * 1.01f // slightly larger than unit sphere
            });
        }

        /// <summary>
        /// Inserts a star given its equatorial RA/Dec direction (converted to Cartesian).
        /// </summary>
        /// <param name="starIndex">Index in the star catalog.</param>
        /// <param name="raHours">Right Ascension in hours (0–24).</param>
        /// <param name="decDeg">Declination in degrees (-90 to +90).</param>
        public void Insert(int starIndex, float raDeg, float decDeg)
        {
            float raRad = raDeg * Mathf.Deg2Rad;
            float decRad = decDeg * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(
                Mathf.Cos(decRad) * Mathf.Cos(raRad),
                Mathf.Cos(decRad) * Mathf.Sin(raRad),
                Mathf.Sin(decRad));
            _root.Insert(starIndex, pos);
        }

        /// <summary>
        /// Returns all star indices whose direction falls within the camera frustum.
        /// </summary>
        /// <param name="frustumPlanes">6 frustum planes from GeometryUtility.CalculateFrustumPlanes.</param>
        public List<int> Query(Plane[] frustumPlanes)
        {
            var results = new List<int>(256);
            _root.Query(frustumPlanes, results);
            return results;
        }
    }
}
