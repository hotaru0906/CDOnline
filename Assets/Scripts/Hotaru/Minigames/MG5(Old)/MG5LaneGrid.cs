using UnityEngine;

public class MG5LaneGrid : MonoBehaviour
{
    [Header("Grid Visual")]
    public bool showGrid = true;
    public int gridCount = 5;
    public float cellSize = 1f;
    public float gridWidth = 1f;   // độ rộng theo trục x
    public float gridHeight = 0f;  // Y offset so với lane
    public Color gridColor = new Color(1f, 1f, 1f, 0.4f);

    private void OnDrawGizmos()
    {
        if (!showGrid) return;

        Gizmos.color = gridColor;

        Vector3 origin = transform.position + Vector3.up * gridHeight;

        // Vẽ các đường dọc (chia ô theo Z)
        for (int i = 0; i <= gridCount; i++)
        {
            Vector3 start = origin + Vector3.forward * (i * cellSize) - Vector3.right * (gridWidth / 2f);
            Vector3 end   = origin + Vector3.forward * (i * cellSize) + Vector3.right * (gridWidth / 2f);
            Gizmos.DrawLine(start, end);
        }

        // Vẽ 2 đường ngang (top/bottom của grid)
        Vector3 leftStart  = origin - Vector3.right * (gridWidth / 2f);
        Vector3 rightStart = origin + Vector3.right * (gridWidth / 2f);
        Gizmos.DrawLine(leftStart,  leftStart  + Vector3.forward * (gridCount * cellSize));
        Gizmos.DrawLine(rightStart, rightStart + Vector3.forward * (gridCount * cellSize));
    }
}