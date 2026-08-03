using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BoardDirectionChoice : MonoBehaviour
{
    [Header("Choice Settings")]
    [SerializeField] private int branchIndex = 0;
    [SerializeField] private BoardNode targetNode;
    [SerializeField] private bool useHighlight = true;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = Color.yellow;
    [SerializeField] private Color selectedColor = Color.green;

    [Header("Optional")]
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private Material[] materialsToTint;

    private Material[] _originalMaterials;
    private bool _isSelected;

    private void Awake()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponentInChildren<MeshRenderer>();

        if (meshRenderer != null)
        {
            _originalMaterials = meshRenderer.materials;
        }
    }

    private void OnMouseEnter()
    {
        if (!useHighlight || _isSelected)
            return;

        SetColor(hoverColor);
    }

    private void OnMouseExit()
    {
        if (!useHighlight || _isSelected)
            return;

        SetColor(normalColor);
    }

    private void OnMouseDown()
    {
        if (_isSelected)
            return;

        if (BoardManager.Instance == null)
            return;

        BoardManager.Instance.SelectBranch(branchIndex);
        SetSelected(true);
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        SetColor(selected ? selectedColor : normalColor);
    }

    public void SetBranchIndex(int index)
    {
        branchIndex = index;
    }

    public void SetTargetNode(BoardNode node)
    {
        targetNode = node;
    }

    private void SetColor(Color color)
    {
        if (!useHighlight || meshRenderer == null)
            return;

        Material[] mats = new Material[meshRenderer.materials.Length];
        for (int i = 0; i < mats.Length; i++)
        {
            if (i < _originalMaterials?.Length)
            {
                mats[i] = new Material(_originalMaterials[i]);
                mats[i].color = color;
            }
            else
            {
                mats[i] = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mats[i].color = color;
            }
        }

        meshRenderer.materials = mats;
    }
}
