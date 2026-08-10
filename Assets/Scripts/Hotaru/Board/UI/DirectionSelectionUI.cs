using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class DirectionSelectionUI : MonoBehaviour
{
    public static DirectionSelectionUI Instance { get; private set; }

    [Header("Fallback UI")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text fallbackText;

    private readonly List<BoardDirectionChoice> spawnedChoices = new();

    private void Awake()
    {
        Instance = this;
        if (panel != null)
            panel.SetActive(false);
    }

    private void Start()
    {
        StartCoroutine(RegisterBoardManager());
    }

    private System.Collections.IEnumerator RegisterBoardManager()
    {
        while (BoardManager.Instance == null)
            yield return null;

        BoardManager.Instance.OnDirectionSelectionRequested += ShowDirectionUI;

        if (panel != null)
        {
            RectTransform rt = panel.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(500, 300);
            }
            panel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (BoardManager.Instance != null)
        {
            BoardManager.Instance.OnDirectionSelectionRequested -= ShowDirectionUI;
        }
    }

    public void ShowDirectionUI(BoardNode node)
    {
        Hide();

        if (fallbackText != null)
        {
            fallbackText.text = "Choose a path";
            if (panel != null)
                panel.SetActive(true);
        }

        if (node == null || node.nextNodes == null || node.nextNodes.Count == 0)
            return;

        for (int i = 0; i < node.nextNodes.Count; i++)
        {
            if (node.nextNodes[i] == null)
                continue;

            GameObject choiceObj = new GameObject($"Choice_{i}");
            choiceObj.transform.position = node.transform.position + Vector3.up * 1.5f + Vector3.forward * (0.8f + i * 0.5f);
            choiceObj.transform.SetParent(node.transform, true);

            var collider = choiceObj.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.8f, 0.8f, 0.8f);

            var choice = choiceObj.AddComponent<BoardDirectionChoice>();
            choice.SetBranchIndex(i);
            choice.SetTargetNode(node.nextNodes[i]);
            //choice.SetInteractable(true);
            choice.SetSelected(false);
            spawnedChoices.Add(choice);
        }
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);

        foreach (var choice in spawnedChoices)
        {
            if (choice != null)
            {
                //choice.SetInteractable(false);
                choice.SetSelected(false);
                Destroy(choice.gameObject);
            }
        }

        spawnedChoices.Clear();
    }
}