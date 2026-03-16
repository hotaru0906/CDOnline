using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(-100)] // Run before other scripts
public class TrackSystem : MonoBehaviour
{
    [Header("Track Settings")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [SerializeField] private bool isLoopTrack = false;
    [SerializeField] private bool autoPopulateFromChildren = true;

    [Header("Track Width (for off-track detection)")]
    [SerializeField] private float trackWidth = 10f;
    [SerializeField] private float maxOffTrackDistance = 15f;  // Max distance before considered invalid

    [Header("Checkpoint Settings")]
    [SerializeField] private bool useCheckpoints = true;
    [SerializeField] private int checkpointInterval = 3;  // Every N waypoints is a checkpoint

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color gizmoColor = Color.yellow;
    [SerializeField] private Color checkpointColor = Color.cyan;
    [SerializeField] private Color trackBoundsColor = new Color(1f, 0.5f, 0f, 0.3f);

    public float TrackLength { get; private set; }
    public float Width => trackWidth;

    private float[] _segmentLengths;
    private float[] _cumulativeDistances;
    private List<int> _checkpointIndices = new List<int>();

    private void Awake()
    {
        if (autoPopulateFromChildren)
        {
            PopulateWaypointsFromChildren();
        }
        CalculateTrackLength();
        GenerateCheckpoints();
    }

    public void PopulateWaypointsFromChildren()
    {
        waypoints.Clear();
        foreach (Transform child in transform)
        {
            waypoints.Add(child);
        }
        Debug.Log($"[TrackSystem] Populated {waypoints.Count} waypoints from children.");
    }

    public void CalculateTrackLength()
    {
        if (waypoints == null || waypoints.Count < 2)
        {
            TrackLength = 0f;
            Debug.LogWarning("[TrackSystem] Not enough waypoints to calculate track length!");
            return;
        }

        int segmentCount = isLoopTrack ? waypoints.Count : waypoints.Count - 1;
        _segmentLengths = new float[segmentCount];
        _cumulativeDistances = new float[segmentCount + 1];

        TrackLength = 0f;
        _cumulativeDistances[0] = 0f;

        for (int i = 0; i < segmentCount; i++)
        {
            int nextIndex = (i + 1) % waypoints.Count;
            float segmentLength = Vector3.Distance(waypoints[i].position, waypoints[nextIndex].position);
            _segmentLengths[i] = segmentLength;
            TrackLength += segmentLength;
            _cumulativeDistances[i + 1] = TrackLength;
        }

        Debug.Log($"[TrackSystem] Track length calculated: {TrackLength:F2} units");
    }

    private void GenerateCheckpoints()
    {
        _checkpointIndices.Clear();
        if (!useCheckpoints || waypoints == null) return;

        // First waypoint is always checkpoint 0
        _checkpointIndices.Add(0);

        for (int i = checkpointInterval; i < waypoints.Count; i += checkpointInterval)
        {
            _checkpointIndices.Add(i);
        }

        // Last waypoint is always a checkpoint (finish line)
        if (!isLoopTrack && _checkpointIndices[_checkpointIndices.Count - 1] != waypoints.Count - 1)
        {
            _checkpointIndices.Add(waypoints.Count - 1);
        }

        Debug.Log($"[TrackSystem] Generated {_checkpointIndices.Count} checkpoints.");
    }

    #region Player Progress Tracking (Per-Player State)

    public class PlayerTrackState
    {
        public int LastSegment { get; set; } = 0;
        public int LastCheckpoint { get; set; } = 0;
        public float LastValidDistance { get; set; } = 0f;
        public bool IsOffTrack { get; set; } = false;
        public float OffTrackDistance { get; set; } = 0f;
        public int LapCount { get; set; } = 0;

        public void Reset()
        {
            LastSegment = 0;
            LastCheckpoint = 0;
            LastValidDistance = 0f;
            IsOffTrack = false;
            OffTrackDistance = 0f;
            LapCount = 0;
        }
    }

    public float GetPlayerDistanceOptimized(Vector3 playerPosition, PlayerTrackState state)
    {
        if (waypoints == null || waypoints.Count < 2)
            return 0f;

        int segmentCount = isLoopTrack ? waypoints.Count : waypoints.Count - 1;

        // Search range: previous, current, next segments (and a few more for safety)
        int searchRadius = 3;
        int startSegment = Mathf.Max(0, state.LastSegment - searchRadius);
        int endSegment = Mathf.Min(segmentCount - 1, state.LastSegment + searchRadius);

        float closestDistance = float.MaxValue;
        int closestSegment = state.LastSegment;
        float closestT = 0f;
        Vector3 closestPoint = Vector3.zero;

        // Search nearby segments first
        for (int i = startSegment; i <= endSegment; i++)
        {
            int nextIndex = (i + 1) % waypoints.Count;
            Vector3 segmentStart = waypoints[i].position;
            Vector3 segmentEnd = waypoints[nextIndex].position;

            Vector3 pointOnSegment = GetClosestPointOnSegment(playerPosition, segmentStart, segmentEnd, out float t);
            float distance = Vector3.Distance(playerPosition, pointOnSegment);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestSegment = i;
                closestT = t;
                closestPoint = pointOnSegment;
            }
        }

        // If no good match found nearby, do full search (fallback)
        if (closestDistance > maxOffTrackDistance)
        {
            for (int i = 0; i < segmentCount; i++)
            {
                if (i >= startSegment && i <= endSegment) continue; // Already checked

                int nextIndex = (i + 1) % waypoints.Count;
                Vector3 segmentStart = waypoints[i].position;
                Vector3 segmentEnd = waypoints[nextIndex].position;

                Vector3 pointOnSegment = GetClosestPointOnSegment(playerPosition, segmentStart, segmentEnd, out float t);
                float distance = Vector3.Distance(playerPosition, pointOnSegment);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestSegment = i;
                    closestT = t;
                    closestPoint = pointOnSegment;
                }
            }
        }

        // Update off-track status
        state.OffTrackDistance = closestDistance;
        state.IsOffTrack = closestDistance > trackWidth;

        // Calculate raw distance
        float rawDistance = _cumulativeDistances[closestSegment] + (_segmentLengths[closestSegment] * closestT);

        // Validate checkpoint order (anti-shortcut)
        float validatedDistance = ValidateDistanceWithCheckpoints(rawDistance, closestSegment, state);

        // Update state
        state.LastSegment = closestSegment;
        state.LastValidDistance = validatedDistance;

        return validatedDistance;
    }

    private float ValidateDistanceWithCheckpoints(float rawDistance, int currentSegment, PlayerTrackState state)
    {
        if (!useCheckpoints || _checkpointIndices.Count == 0)
            return rawDistance;

        // Find which checkpoint zone player is in
        int currentCheckpointIndex = 0;
        for (int i = _checkpointIndices.Count - 1; i >= 0; i--)
        {
            if (currentSegment >= _checkpointIndices[i])
            {
                currentCheckpointIndex = i;
                break;
            }
        }

        // Check if player skipped checkpoints
        if (currentCheckpointIndex > state.LastCheckpoint + 1)
        {
            // Player skipped checkpoint(s) - likely shortcut!
            Debug.LogWarning($"[TrackSystem] Shortcut detected! Player jumped from checkpoint {state.LastCheckpoint} to {currentCheckpointIndex}");

            // Return last valid distance (don't reward cheating)
            return state.LastValidDistance;
        }

        // Check for backwards movement (more than 1 checkpoint back)
        if (currentCheckpointIndex < state.LastCheckpoint - 1)
        {
            // Could be lap completion in loop track
            if (isLoopTrack && state.LastCheckpoint == _checkpointIndices.Count - 1 && currentCheckpointIndex == 0)
            {
                state.LapCount++;
                state.LastCheckpoint = 0;
                Debug.Log($"[TrackSystem] Lap completed! Total laps: {state.LapCount}");
            }
            else
            {
                // Significant backwards movement - use last valid
                return state.LastValidDistance;
            }
        }

        // Valid progression - update checkpoint
        state.LastCheckpoint = currentCheckpointIndex;
        return Mathf.Clamp(rawDistance, 0f, TrackLength);
    }

    public float GetPlayerProgressOptimized(Vector3 playerPosition, PlayerTrackState state)
    {
        if (TrackLength <= 0f) return 0f;
        return GetPlayerDistanceOptimized(playerPosition, state) / TrackLength;
    }

    public bool IsPlayerOffTrack(Vector3 playerPosition, PlayerTrackState state)
    {
        // Just update state and return
        GetPlayerDistanceOptimized(playerPosition, state);
        return state.IsOffTrack;
    }

    public float GetOffTrackDistance(PlayerTrackState state)
    {
        return Mathf.Max(0f, state.OffTrackDistance - trackWidth);
    }

    #endregion

    #region Original Methods (Backwards Compatible)

    public float GetPlayerDistance(Vector3 playerPosition)
    {
        if (waypoints == null || waypoints.Count < 2)
            return 0f;

        float closestDistance = float.MaxValue;
        int closestSegment = 0;
        float closestT = 0f;

        int segmentCount = isLoopTrack ? waypoints.Count : waypoints.Count - 1;

        // Find closest point on track
        for (int i = 0; i < segmentCount; i++)
        {
            int nextIndex = (i + 1) % waypoints.Count;
            Vector3 segmentStart = waypoints[i].position;
            Vector3 segmentEnd = waypoints[nextIndex].position;

            Vector3 closestPoint = GetClosestPointOnSegment(playerPosition, segmentStart, segmentEnd, out float t);
            float distance = Vector3.Distance(playerPosition, closestPoint);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestSegment = i;
                closestT = t;
            }
        }

        // Calculate total distance along track
        float totalDistance = _cumulativeDistances[closestSegment] + (_segmentLengths[closestSegment] * closestT);
        return Mathf.Clamp(totalDistance, 0f, TrackLength);
    }

    public float GetPlayerProgress(Vector3 playerPosition)
    {
        if (TrackLength <= 0f) return 0f;
        return GetPlayerDistance(playerPosition) / TrackLength;
    }

    #endregion

    #region Utility Methods

    private Vector3 GetClosestPointOnSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd, out float t)
    {
        Vector3 segment = segmentEnd - segmentStart;
        float sqrLength = segment.sqrMagnitude;

        if (sqrLength < 0.0001f)
        {
            t = 0f;
            return segmentStart;
        }

        t = Mathf.Clamp01(Vector3.Dot(point - segmentStart, segment) / sqrLength);
        return segmentStart + t * segment;
    }
    public Vector3 GetPositionAtDistance(float distance)
    {
        if (waypoints == null || waypoints.Count < 2 || TrackLength <= 0f)
            return Vector3.zero;

        distance = Mathf.Clamp(distance, 0f, TrackLength);

        // Find which segment this distance falls into
        for (int i = 0; i < _cumulativeDistances.Length - 1; i++)
        {
            if (distance <= _cumulativeDistances[i + 1])
            {
                float segmentProgress = (distance - _cumulativeDistances[i]) / _segmentLengths[i];
                int nextIndex = (i + 1) % waypoints.Count;
                return Vector3.Lerp(waypoints[i].position, waypoints[nextIndex].position, segmentProgress);
            }
        }

        return waypoints[waypoints.Count - 1].position;
    }

    public Vector3 GetDirectionAtDistance(float distance)
    {
        if (waypoints == null || waypoints.Count < 2 || TrackLength <= 0f)
            return Vector3.forward;

        distance = Mathf.Clamp(distance, 0f, TrackLength);

        for (int i = 0; i < _cumulativeDistances.Length - 1; i++)
        {
            if (distance <= _cumulativeDistances[i + 1])
            {
                int nextIndex = (i + 1) % waypoints.Count;
                return (waypoints[nextIndex].position - waypoints[i].position).normalized;
            }
        }

        int lastIndex = waypoints.Count - 1;
        return (waypoints[lastIndex].position - waypoints[lastIndex - 1].position).normalized;
    }

    public Transform GetWaypoint(int index)
    {
        if (waypoints == null || index < 0 || index >= waypoints.Count)
            return null;
        return waypoints[index];
    }

    public IReadOnlyList<int> GetCheckpointIndices() => _checkpointIndices;
    public int WaypointCount => waypoints?.Count ?? 0;
    public Vector3 StartPosition => waypoints != null && waypoints.Count > 0 ? waypoints[0].position : Vector3.zero;
    public Vector3 EndPosition => waypoints != null && waypoints.Count > 0 ? waypoints[waypoints.Count - 1].position : Vector3.zero;

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmos || waypoints == null || waypoints.Count < 2)
            return;

        int segmentCount = isLoopTrack ? waypoints.Count : waypoints.Count - 1;

        // Draw track bounds
        Gizmos.color = trackBoundsColor;
        for (int i = 0; i < segmentCount; i++)
        {
            int nextIndex = (i + 1) % waypoints.Count;
            if (waypoints[i] != null && waypoints[nextIndex] != null)
            {
                Vector3 start = waypoints[i].position;
                Vector3 end = waypoints[nextIndex].position;
                Vector3 dir = (end - start).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, dir) * trackWidth;

                // Draw track width boundaries
                Gizmos.DrawLine(start + right, end + right);
                Gizmos.DrawLine(start - right, end - right);
            }
        }

        // Draw track centerline
        Gizmos.color = gizmoColor;
        for (int i = 0; i < segmentCount; i++)
        {
            int nextIndex = (i + 1) % waypoints.Count;
            if (waypoints[i] != null && waypoints[nextIndex] != null)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[nextIndex].position);
            }
        }

        // Draw waypoint spheres
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;

            bool isCheckpoint = _checkpointIndices != null && _checkpointIndices.Contains(i);
            bool isStart = i == 0;
            bool isEnd = i == waypoints.Count - 1 && !isLoopTrack;

            if (isStart)
                Gizmos.color = Color.green;
            else if (isEnd)
                Gizmos.color = Color.red;
            else if (isCheckpoint)
                Gizmos.color = checkpointColor;
            else
                Gizmos.color = gizmoColor;

            float size = isCheckpoint ? 0.8f : 0.5f;
            Gizmos.DrawSphere(waypoints[i].position, size);

            // Draw checkpoint number
            if (isCheckpoint && _checkpointIndices != null)
            {
                int cpIndex = _checkpointIndices.IndexOf(i);
                UnityEditor.Handles.Label(waypoints[i].position + Vector3.up * 2f, $"CP{cpIndex}");
            }
        }
    }

    private void OnValidate()
    {
        // Recalculate when values change in editor
        if (waypoints != null && waypoints.Count >= 2)
        {
            CalculateTrackLength();
            GenerateCheckpoints();
        }
    }
#endif
}
