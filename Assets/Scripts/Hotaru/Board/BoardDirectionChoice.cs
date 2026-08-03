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
    [SerializeField] private Color hintColor = new Color(0.2f, 0.8f, 1f, 1f);
    [SerializeField] private Color selectedColor = Color.green;

    [Header("Optional")]
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private Material[] materialsToTint;

    private Material[] _originalMaterials;
    private bool _isSelected;
    private bool _isInteractable;

    private void Awake()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponentInChildren<MeshRenderer>();

        if (meshRenderer != null)
        {
            _originalMaterials = meshRenderer.materials;
        }

        _isInteractable = false;
        SetColor(normalColor);
    }

    private void OnMouseUpAsButton()
    {
        if (!_isInteractable || _isSelected)
            return;

        if (BoardManager.Instance == null)
            return;

        int targetNodeId = targetNode != null ? targetNode.nodeID : -1;
        BoardManager.Instance.SelectBranch(branchIndex, targetNodeId);
        SetSelected(true);
        DirectionSelectionUI.Instance?.Hide();
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        SetColor(selected ? selectedColor : normalColor);
    }

    public void SetInteractable(bool interactable)
    {
        _isInteractable = interactable;

        if (TryGetComponent<Collider>(out var collider))
            collider.enabled = interactable;

        if (!_isInteractable)
        {
            _isSelected = false;
            SetColor(normalColor);
        }
        else if (!_isSelected)
        {
            SetColor(hintColor);
        }
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
