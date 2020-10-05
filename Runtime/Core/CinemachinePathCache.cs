﻿using UnityEngine;
using Cinemachine.Utility;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace Cinemachine
{
    /// <summary>Wrapper class for SplineContainer use in Cinemachine</summary>
    public class CinemachinePathCache
    {
        /// <summary>How to interpret the Path Position</summary>
        public enum PositionUnits
        {
            /// <summary>Use PathPosition units, where 0 is first waypoint, 1 is second waypoint, etc</summary>
            PathUnits,
            /// <summary>Use Distance Along Path.  Path will be sampled according to its Resolution
            /// setting, and a distance lookup table will be cached internally</summary>
            Distance,
            /// <summary>Normalized units, where 0 is the start of the path, and 1 is the end.
            /// Path will be sampled according to its Resolution
            /// setting, and a distance lookup table will be cached internally</summary>
            Normalized
        }

        /// <summary>When calculating the distance cache, sample the path this many
        /// times between points</summary>
        internal int m_DistanceCacheSampleStepsPerSegment = 20;

        /// <summary>The path to follow</summary>
        public SplineContainer m_Path;

        /// <summary>Update the cached path elements if necessary</summary>
        public void UpdatePathCache(SplineContainer path)
        {
            if (m_Path != path)
            {
                m_Path = path;
                InvalidateDistanceCache();
                return;
            }
            // if callback()
            InvalidateDistanceCache();
        }

        /// <summary>Call this if the path changes in such a way as to affect distances
        /// or other cached path elements</summary>
        private void InvalidateDistanceCache()
        {
            m_DistanceToPos = null;
            m_PosToDistance = null;
            m_CachedSampleSteps = 0;
            m_PathLength = 0;
        }

        /// <summary>The minimum value for the path position</summary>
        public float MinPos { get { return 0; } }

        /// <summary>The maximum value for the path position</summary>
        public float MaxPos
        {
            get
            {
                if (!m_Path)
                    return 0;
                int count = m_Path.Spline.KnotCount - 1;
                if (count < 1)
                    return 0;
                return m_Path.Spline.Closed ? count + 1 : count;
            }
        }

        /// <summary>Get the minimum value, for the given unit type</summary>
        /// <param name="units">The unit type</param>
        /// <returns>The minimum allowable value for this path</returns>
        public float MinUnit(PositionUnits units)
        {
            if (units == PositionUnits.Normalized)
                return 0;
            return units == PositionUnits.Distance ? 0 : MinPos;
        }

        /// <summary>Get the maximum value, for the given unit type</summary>
        /// <param name="units">The unit type</param>
        /// <returns>The maximum allowable value for this path</returns>
        public float MaxUnit(PositionUnits units)
        {
            if (units == PositionUnits.Normalized)
                return 1;
            return units == PositionUnits.Distance ? PathLength : MaxPos;
        }

        /// <summary>True if the path ends are joined to form a continuous loop</summary>
        public bool Looped {
            get
            {
                return m_Path ? m_Path.Spline.Closed : false;
            }
        }

        /// <summary>See whether the distance cache is valid.  If it's not valid,
        /// then any call to GetPathLength() or ToNativePathUnits() will
        /// trigger a potentially costly regeneration of the path distance cache</summary>
        /// <returns>Whether the cache is valid</returns>
        public bool DistanceCacheIsValid()
        {
            return (MaxPos == MinPos)
                || (m_DistanceToPos != null && m_PosToDistance != null
                    && m_CachedSampleSteps == m_DistanceCacheSampleStepsPerSegment
                    && m_CachedSampleSteps > 0);
        }

        /// <summary>Get the length of the path in distance units.
        /// If the distance cache is not valid, then calling this will
        /// trigger a potentially costly regeneration of the path distance cache</summary>
        /// <returns>The length of the path in distance units, when sampled at this rate</returns>
        public float PathLength
        {
            get
            {
                if (!m_Path || m_DistanceCacheSampleStepsPerSegment < 1)
                    return 0;
                if (!DistanceCacheIsValid())
                    ResamplePath(m_DistanceCacheSampleStepsPerSegment);
                return m_PathLength;
            }
        }
        
        /// <summary>Get a standardized path position, taking spins into account if looped</summary>
        /// <param name="pos">Position along the path</param>
        /// <returns>Standardized position, between MinPos and MaxPos</returns>
        private float StandardizePos(float pos)
        {
            if (Looped && MaxPos > 0)
            {
                pos = pos % MaxPos;
                if (pos < 0)
                    pos += MaxPos;
                return pos;
            }
            return Mathf.Clamp(pos, 0, MaxPos);
        }

        /// <summary>Standardize a distance along the path based on the path length.
        /// If the distance cache is not valid, then calling this will
        /// trigger a potentially costly regeneration of the path distance cache</summary>
        /// <param name="distance">The distance to standardize</param>
        /// <returns>The standardized distance, ranging from 0 to path length</returns>
        private float StandardizePathDistance(float distance)
        {
            float length = PathLength;
            if (length < UnityVectorExtensions.Epsilon)
                return 0;
            if (Looped)
            {
                distance = distance % length;
                if (distance < 0)
                    distance += length;
            }
            return Mathf.Clamp(distance, 0, length);
        }

        /// <summary>Standardize the unit, so that it lies between MinUmit and MaxUnit</summary>
        /// <param name="pos">The value to be standardized</param>
        /// <param name="units">The unit type</param>
        /// <returns>The standardized value of pos, between MinUnit and MaxUnit</returns>
        public float StandardizeUnit(float pos, PositionUnits units)
        {
            if (units == PositionUnits.PathUnits)
                return StandardizePos(pos);
            if (units == PositionUnits.Distance)
                return StandardizePathDistance(pos);
            float len = PathLength;
            if (len < UnityVectorExtensions.Epsilon)
                return 0;
            return StandardizePathDistance(pos * len) / len;
        }

        /// <summary>Returns standardized position</summary>
        private float GetBoundingIndices(float pos, out int indexA, out int indexB)
        {
            pos = StandardizePos(pos);
            int numWaypoints = m_Path.Spline.KnotCount;
            if (numWaypoints < 2)
                indexA = indexB = 0;
            else
            {
                indexA = Mathf.FloorToInt(pos);
                if (indexA >= numWaypoints)
                {
                    // Only true if looped
                    pos -= MaxPos;
                    indexA = 0;
                }
                indexB = indexA + 1;
                if (indexB == numWaypoints)
                {
                    if (Looped)
                        indexB = 0;
                    else
                    {
                        --indexB;
                        --indexA;
                    }
                }
            }
            return pos;
        }

        /// <summary>Get a worldspace position of a point along the path</summary>
        /// <param name="pos">Postion along the path.  Need not be standardized.</param>
        /// <returns>World-space position of the point along at path at pos</returns>
        internal Vector3 EvaluatePosition(float pos)
        {
            float3 result = float3.zero;
            if (m_Path && m_Path.Spline.KnotCount > 0)
            {
                int indexA, indexB;
                pos = GetBoundingIndices(pos, out indexA, out indexB);
                if (indexA == indexB)
                    result = m_Path.EvaluateCurvePosition(indexA, 0);
                else
                    result = m_Path.EvaluateCurvePosition(indexA, pos - indexA);
            }
            return new Vector3(result.x, result.y, result.z);
        }

        /// <summary>Get the tangent of the curve at a point along the path.</summary>
        /// <param name="pos">Postion along the path.  Need not be standardized.</param>
        /// <returns>World-space direction of the path tangent.
        /// Length of the vector represents the tangent strength</returns>
        private Vector3 EvaluateTangent(float pos)
        {
            return Vector3.zero;
        }

        // same as Quaternion.AngleAxis(roll, Vector3.forward), just simplified
        private Quaternion RollAroundForward(float angle)
        {
            float halfAngle = angle * 0.5F * Mathf.Deg2Rad;
            return new Quaternion(
                0,
                0,
                Mathf.Sin(halfAngle),
                Mathf.Cos(halfAngle));
        }

        /// <summary>Get the orientation the curve at a point along the path.</summary>
        /// <param name="pos">Postion along the path.  Need not be standardized.</param>
        /// <returns>World-space orientation of the path</returns>
        internal Quaternion EvaluateOrientation(float pos)
        {
            return Quaternion.identity;
        }

        /// <summary>Get a worldspace position of a point along the path</summary>
        /// <param name="pos">Postion along the path.  Need not be normalized.</param>
        /// <param name="units">The unit to use when interpreting the value of pos.</param>
        /// <returns>World-space position of the point along at path at pos</returns>
        public Vector3 EvaluatePositionAtUnit(float pos, PositionUnits units)
        {

            return EvaluatePosition(ToNativePathUnits(pos, units));
        }

        /// <summary>Get the tangent of the curve at a point along the path.</summary>
        /// <param name="pos">Postion along the path.  Need not be normalized.</param>
        /// <param name="units">The unit to use when interpreting the value of pos.</param>
        /// <returns>World-space direction of the path tangent.
        /// Length of the vector represents the tangent strength</returns>
        public Vector3 EvaluateTangentAtUnit(float pos, PositionUnits units)
        {
            return EvaluateTangent(ToNativePathUnits(pos, units));
        }

        /// <summary>Get the orientation the curve at a point along the path.</summary>
        /// <param name="pos">Postion along the path.  Need not be normalized.</param>
        /// <param name="units">The unit to use when interpreting the value of pos.</param>
        /// <returns>World-space orientation of the path</returns>
        public Quaternion EvaluateOrientationAtUnit(float pos, PositionUnits units)
        {
            return EvaluateOrientation(ToNativePathUnits(pos, units));
        }

        /// <summary>Find the closest point on the path to a given worldspace target point.</summary>
        /// <remarks>Performance could be improved by checking the bounding polygon of each segment,
        /// and only entering the best segment(s)</remarks>
        /// <param name="p">Worldspace target that we want to approach</param>
        /// <param name="startSegment">In what segment of the path to start the search.
        /// A Segment is a section of path between 2 waypoints.</param>
        /// <param name="searchRadius">How many segments on either side of the startSegment
        /// to search.  -1 means no limit, i.e. search the entire path</param>
        /// <param name="stepsPerSegment">We search a segment by dividing it into this many
        /// straight pieces.  The higher the number, the more accurate the result, but performance
        /// is proportionally slower for higher numbers</param>
        /// <returns>The position along the path that is closest to the target point.
        /// The value is in Path Units, not Distance units.</returns>
        public float FindClosestPoint(
            Vector3 p, int startSegment, int searchRadius, int stepsPerSegment)
        {
            float start = MinPos;
            float end = MaxPos;
            if (searchRadius >= 0)
            {
                int r = Mathf.FloorToInt(Mathf.Min(searchRadius, (end - start) / 2f));
                start = startSegment - r;
                end = startSegment + r + 1;
                if (!Looped)
                {
                    start = Mathf.Max(start, MinPos);
                    end = Mathf.Min(end, MaxPos);
                }
            }
            stepsPerSegment = Mathf.RoundToInt(Mathf.Clamp(stepsPerSegment, 1f, 100f));
            float stepSize = 1f / stepsPerSegment;
            float bestPos = startSegment;
            float bestDistance = float.MaxValue;
            int iterations = (stepsPerSegment == 1) ? 1 : 3;
            for (int i = 0; i < iterations; ++i)
            {
                Vector3 v0 = EvaluatePosition(start);
                for (float f = start + stepSize; f <= end; f += stepSize)
                {
                    Vector3 v = EvaluatePosition(f);
                    float t = p.ClosestPointOnSegment(v0, v);
                    float d = Vector3.SqrMagnitude(p - Vector3.Lerp(v0, v, t));
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        bestPos = f - (1 - t) * stepSize;
                    }
                    v0 = v;
                }
                start = bestPos - stepSize;
                end = bestPos + stepSize;
                stepSize /= stepsPerSegment;
            }
            return bestPos;
        }

        /// <summary>Get the path position (in native path units) corresponding to the psovided
        /// value, in the units indicated.
        /// If the distance cache is not valid, then calling this will
        /// trigger a potentially costly regeneration of the path distance cache</summary>
        /// <param name="pos">The value to convert from</param>
        /// <param name="units">The units in which pos is expressed</param>
        /// <returns>The length of the path in native units, when sampled at this rate</returns>
        public float ToNativePathUnits(float pos, PositionUnits units)
        {
            if (units == PositionUnits.PathUnits)
                return pos;
            if (m_DistanceCacheSampleStepsPerSegment < 1 || PathLength < UnityVectorExtensions.Epsilon)
                return MinPos;
            if (units == PositionUnits.Normalized)
                pos *= PathLength;
            pos = StandardizePathDistance(pos);
            float d = pos / m_cachedDistanceStepSize;
            int i = Mathf.FloorToInt(d);
            if (i >= m_DistanceToPos.Length - 1)
                return MaxPos;
            float t = d - (float)i;
            return MinPos + Mathf.Lerp(m_DistanceToPos[i], m_DistanceToPos[i + 1], t);
        }

        /// <summary>Get the path position (in path units) corresponding to this distance along the path.
        /// If the distance cache is not valid, then calling this will
        /// trigger a potentially costly regeneration of the path distance cache</summary>
        /// <param name="pos">The value to convert from, in native units</param>
        /// <param name="units">The units to convert toexpressed</param>
        /// <returns>The length of the path in distance units, when sampled at this rate</returns>
        public float FromPathNativeUnits(float pos, PositionUnits units)
        {
            if (units == PositionUnits.PathUnits)
                return pos;
            float length = PathLength;
            if (m_DistanceCacheSampleStepsPerSegment < 1 || length < UnityVectorExtensions.Epsilon)
                return 0;
            pos = StandardizePos(pos);
            float d = pos / m_cachedPosStepSize;
            int i = Mathf.FloorToInt(d);
            if (i >= m_PosToDistance.Length - 1)
                pos = m_PathLength;
            else
            {
                float t = d - (float)i;
                pos = Mathf.Lerp(m_PosToDistance[i], m_PosToDistance[i + 1], t);
            }
            if (units == PositionUnits.Normalized)
                pos /= length;
            return pos;
        }

        private float[] m_DistanceToPos;
        private float[] m_PosToDistance;
        private int m_CachedSampleSteps;
        private float m_PathLength;
        private float m_cachedPosStepSize;
        private float m_cachedDistanceStepSize;

        private void ResamplePath(int stepsPerSegment)
        {
            InvalidateDistanceCache();

            float minPos = MinPos;
            float maxPos = MaxPos;
            float stepSize = 1f / Mathf.Max(1, stepsPerSegment);

            // Sample the positions
            int numKeys = Mathf.RoundToInt((maxPos - minPos) / stepSize) + 1;
            m_PosToDistance = new float[numKeys];
            m_CachedSampleSteps = stepsPerSegment;
            m_cachedPosStepSize = stepSize;

            Vector3 p0 = EvaluatePosition(0);
            m_PosToDistance[0] = 0;
            float pos = minPos;
            for (int i = 1; i < numKeys; ++i)
            {
                pos += stepSize;
                Vector3 p = EvaluatePosition(pos);
                float d = Vector3.Distance(p0, p);
                m_PathLength += d;
                p0 = p;
                m_PosToDistance[i] = m_PathLength;
            }

            // Resample the distances
            m_DistanceToPos = new float[numKeys];
            m_DistanceToPos[0] = 0;
            if (numKeys > 1)
            {
                stepSize = m_PathLength / (numKeys - 1);
                m_cachedDistanceStepSize = stepSize;
                float distance = 0;
                int posIndex = 1;
                for (int i = 1; i < numKeys; ++i)
                {
                    distance += stepSize;
                    float d = m_PosToDistance[posIndex];
                    while (d < distance && posIndex < numKeys - 1)
                        d = m_PosToDistance[++posIndex];
                    float d0 = m_PosToDistance[posIndex - 1];
                    float delta = d - d0;
                    float t = (distance - d0) / delta;
                    m_DistanceToPos[i] = m_cachedPosStepSize * (t + posIndex - 1);
                }
            }
        }
    }
}

