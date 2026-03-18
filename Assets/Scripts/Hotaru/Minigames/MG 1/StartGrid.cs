using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines the starting grid for race minigames.
/// Contains spawn points with position and rotation aligned to track direction.
/// </summary>
public class StartGrid : MonoBehaviour
{
    [Header("Grid Configuration")]
    [SerializeField] private int gridRows = 2;
    [SerializeField] private int gridColumns = 2;
    [SerializeField] private float rowSpacing = 3f;      // Distance between rows (front to back)
    [SerializeField] private float columnSpacing = 2.5f; // Distance between columns (side to side)

    [Header("Track Alignment")]
    [SerializeField] private TrackSystem trackSystem;
    [SerializeField] private bool autoAlignToTrack = true;
    [SerializeField] private float trackOffset = 2f;     // Distance behind track start

    [Header("Manual Spawn Points (Optional)")]
    [SerializeField] private List<Transform> manualSpawnPoints = new List<Transform>();
    [SerializeField] private bool useManualSpawnPoints = false;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color gizmoColor = Color.cyan;
    [SerializeField] private float gizmoSize = 0.5f;

    /// <summary>
    /// Cached spawn point data.
    /// </summary>
    public struct SpawnPointData
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public int GridRow;
        public int GridColumn;

        public SpawnPointData(Vector3 pos, Quaternion rot, int row, int col)
        {
            Position = pos;
            Rotation = rot;
            GridRow = row;
            GridColumn = col;
        }
    }

    private List<SpawnPointData> _spawnPoints = new List<SpawnPointData>();
    private Vector3 _trackDirection = Vector3.forward;
    private Vector3 _gridCenter;
    private bool _isInitialized = false;

    /// <summary>
    /// Maximum number of spawn points available.
    /// </summary>
    public int MaxSpawnPoints => useManualSpawnPoints ? manualSpawnPoints.Count : (gridRows * gridColumns);

    private void Awake()
    {
        // Don't auto-initialize here - TrackSystem may not be ready
        // Initialize() will be called lazily when needed or explicitly by RaceSpawnManager
    }

    /// <summary>
    /// Initialize the start grid. Call this if track system wasn't available at Awake.
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;

        if (useManualSpawnPoints)
        {
            InitializeFromManualPoints();
        }
        else
        {
            InitializeFromGrid();
        }

        _isInitialized = true;
        Debug.Log($"[StartGrid] Initialized with {_spawnPoints.Count} spawn points.");
    }

    /// <summary>
    /// Initialize using manual spawn point transforms.
    /// </summary>
    private void InitializeFromManualPoints()
    {
        _spawnPoints.Clear();

        // Check if manual spawn points list is empty or null
        if (manualSpawnPoints == null || manualSpawnPoints.Count == 0)
        {
            Debug.LogWarning("[StartGrid] Manual spawn points list is empty! Falling back to grid generation.");
            InitializeFromGrid();
            return;
        }

        for (int i = 0; i < manualSpawnPoints.Count; i++)
        {
            if (manualSpawnPoints[i] != null)
            {
                _spawnPoints.Add(new SpawnPointData(
                    manualSpawnPoints[i].position,
                    manualSpawnPoints[i].rotation,
                    i / gridColumns,
                    i % gridColumns
                ));
            }
        }

        // Check if any valid spawn points were added
        if (_spawnPoints.Count == 0)
        {
            Debug.LogWarning("[StartGrid] All manual spawn point transforms are null! Falling back to grid generation.");
            InitializeFromGrid();
        }
    }

    /// <summary>
    /// Initialize using auto-generated grid based on track direction.
    /// </summary>
    private void InitializeFromGrid()
    {
        _spawnPoints.Clear();

        // Get track direction and start position
        if (autoAlignToTrack && trackSystem != null)
        {
            _gridCenter = trackSystem.StartPosition - (trackSystem.GetDirectionAtDistance(0f) * trackOffset);
            _trackDirection = trackSystem.GetDirectionAtDistance(0f);
        }
        else
        {
            _gridCenter = transform.position;
            _trackDirection = transform.forward;
        }

        // Calculate grid (Cross product: trackDirection x Up = Right)
        Vector3 right = Vector3.Cross(_trackDirection, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.001f)
        {
            right = Vector3.right;
        }

        // Calculate starting corner (offset to center the grid)
        float totalWidth = (gridColumns - 1) * columnSpacing;
        float totalDepth = (gridRows - 1) * rowSpacing;
        Vector3 startCorner = _gridCenter - (right * totalWidth * 0.5f) + (_trackDirection * totalDepth * 0.5f);

        // Create spawn points in grid pattern
        // Row 0 = front (closest to track start), Row N = back
        // Column layout: alternating pattern for racing (staggered start)
        int spawnIndex = 0;
        for (int row = 0; row < gridRows; row++)
        {
            for (int col = 0; col < gridColumns; col++)
            {
                // Stagger columns for odd rows (racing grid pattern)
                float colOffset = (row % 2 == 1) ? columnSpacing * 0.5f : 0f;

                Vector3 position = startCorner
                    + (right * (col * columnSpacing + colOffset))
                    - (_trackDirection * row * rowSpacing);

                Quaternion rotation = Quaternion.LookRotation(_trackDirection, Vector3.up);

                _spawnPoints.Add(new SpawnPointData(position, rotation, row, col));
                spawnIndex++;
            }
        }
    }

    /// <summary>
    /// Get spawn point data for a specific player index (0-based).
    /// Wraps around if playerIndex exceeds spawn point count.
    /// </summary>
    /// <param name="playerIndex">Player index (0 = first player, 1 = second, etc.)</param>
    /// <returns>SpawnPointData with position and rotation.</returns>
    public SpawnPointData GetSpawnPoint(int playerIndex)
    {
        if (!_isInitialized)
        {
            Initialize();
        }

        // Check if we have any spawn points after initialization
        if (_spawnPoints.Count == 0)
        {
            Debug.LogError("[StartGrid] No spawn points available! Returning safe fallback.");
            // Return a safe fallback at the grid position (not Vector3.zero)
            Vector3 safePosition = transform.position; // Use this object's position as fallback
            return new SpawnPointData(
                safePosition,
                Quaternion.LookRotation(transform.forward, Vector3.up),
                0, 0
            );
        }

        // Handle negative index
        if (playerIndex < 0)
        {
            Debug.LogWarning($"[StartGrid] Negative player index {playerIndex}. Using index 0.");
            playerIndex = 0;
        }

        // Wrap around if index exceeds count (allows more players than spawn points)
        int wrappedIndex = playerIndex % _spawnPoints.Count;
        
        return _spawnPoints[wrappedIndex];
    }

    /// <summary>
    /// Get spawn position for a player.
    /// </summary>
    public Vector3 GetSpawnPosition(int playerIndex)
    {
        return GetSpawnPoint(playerIndex).Position;
    }

    /// <summary>
    /// Get spawn rotation for a player.
    /// </summary>
    public Quaternion GetSpawnRotation(int playerIndex)
    {
        return GetSpawnPoint(playerIndex).Rotation;
    }

    /// <summary>
    /// Get all spawn points.
    /// </summary>
    public IReadOnlyList<SpawnPointData> GetAllSpawnPoints()
    {
        if (!_isInitialized)
        {
            Initialize();
        }
        return _spawnPoints;
    }

    /// <summary>
    /// Recalculate spawn points (call if track changes at runtime).
    /// </summary>
    public void Recalculate()
    {
        _isInitialized = false;
        Initialize();
    }

    /// <summary>
    /// Set track system reference at runtime.
    /// </summary>
    public void SetTrackSystem(TrackSystem track)
    {
        trackSystem = track;
        if (autoAlignToTrack)
        {
            Recalculate();
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Draw spawn points
        List<SpawnPointData> points;

        if (Application.isPlaying && _isInitialized)
        {
            points = _spawnPoints;
        }
        else
        {
            // Generate preview in editor
            points = GeneratePreviewPoints();
        }

        for (int i = 0; i < points.Count; i++)
        {
            var point = points[i];

            // Color based on position (front = bright, back = dim)
            float brightness = 1f - (point.GridRow * 0.2f);
            Gizmos.color = new Color(gizmoColor.r * brightness, gizmoColor.g * brightness, gizmoColor.b * brightness, 1f);

            // Draw spawn position
            Gizmos.DrawSphere(point.Position, gizmoSize);

            // Draw direction arrow
            Gizmos.DrawRay(point.Position, point.Rotation * Vector3.forward * 2f);

            // Draw player number
            UnityEditor.Handles.Label(point.Position + Vector3.up * 1.5f, $"P{i + 1}");
        }

        // Draw grid outline
        if (points.Count > 0)
        {
            Gizmos.color = gizmoColor * 0.5f;
            Vector3 center = Vector3.zero;
            foreach (var p in points) center += p.Position;
            center /= points.Count;

            Gizmos.DrawWireCube(center + Vector3.up * 0.1f, 
                new Vector3((gridColumns - 1) * columnSpacing + 2f, 0.2f, (gridRows - 1) * rowSpacing + 2f));
        }
    }

    private List<SpawnPointData> GeneratePreviewPoints()
    {
        var preview = new List<SpawnPointData>();

        if (useManualSpawnPoints)
        {
            for (int i = 0; i < manualSpawnPoints.Count; i++)
            {
                if (manualSpawnPoints[i] != null)
                {
                    preview.Add(new SpawnPointData(
                        manualSpawnPoints[i].position,
                        manualSpawnPoints[i].rotation,
                        i / gridColumns,
                        i % gridColumns
                    ));
                }
            }
        }
        else
        {
            Vector3 gridCenter;
            Vector3 trackDir;

            if (autoAlignToTrack && trackSystem != null)
            {
                gridCenter = trackSystem.StartPosition - (trackSystem.GetDirectionAtDistance(0f) * trackOffset);
                trackDir = trackSystem.GetDirectionAtDistance(0f);
            }
            else
            {
                gridCenter = transform.position;
                trackDir = transform.forward;
            }

            Vector3 right = Vector3.Cross(trackDir, Vector3.up).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;

            float totalWidth = (gridColumns - 1) * columnSpacing;
            float totalDepth = (gridRows - 1) * rowSpacing;
            Vector3 startCorner = gridCenter - (right * totalWidth * 0.5f) + (trackDir * totalDepth * 0.5f);

            for (int row = 0; row < gridRows; row++)
            {
                for (int col = 0; col < gridColumns; col++)
                {
                    float colOffset = (row % 2 == 1) ? columnSpacing * 0.5f : 0f;
                    Vector3 position = startCorner
                        + (right * (col * columnSpacing + colOffset))
                        - (trackDir * row * rowSpacing);

                    preview.Add(new SpawnPointData(
                        position,
                        Quaternion.LookRotation(trackDir, Vector3.up),
                        row, col
                    ));
                }
            }
        }

        return preview;
    }

    private void OnValidate()
    {
        gridRows = Mathf.Max(1, gridRows);
        gridColumns = Mathf.Max(1, gridColumns);
        rowSpacing = Mathf.Max(0.5f, rowSpacing);
        columnSpacing = Mathf.Max(0.5f, columnSpacing);
    }
#endif
}
