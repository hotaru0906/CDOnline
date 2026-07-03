using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-1000)]
public class DirectionSelectionUI : MonoBehaviour
{
    public static DirectionSelectionUI Instance { get; private set; }
    [Header("UI")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform buttonRoot;
    [SerializeField] private Button directionButtonPrefab;
    
    [SerializeField] private Sprite leftArrow;
    [SerializeField] private Sprite rightArrow;
    [SerializeField] private Sprite upArrow;
    [SerializeField] private Sprite downArrow;

    private readonly List<Button> spawnedButtons = new();

    private void Awake()
    {
        Instance = this;
        Debug.LogError("===== DIRECTION UI AWAKE =====");

        panel.SetActive(false);
    }

    private void Start()
    {
        Debug.LogError("===== DIRECTION UI START =====");

        StartCoroutine(RegisterBoardManager());
    }

    private System.Collections.IEnumerator RegisterBoardManager()
    {
        while (BoardManager.Instance == null)
            yield return null;

        Debug.Log("[DirectionUI] BoardManager found");

        BoardManager.Instance.OnDirectionSelectionRequested += ShowDirectionUI;

        Debug.Log("[DirectionUI] Event Registered");

        RectTransform rt = panel.GetComponent<RectTransform>();

        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(500, 300);

        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (BoardManager.Instance != null)
        {
            BoardManager.Instance.OnDirectionSelectionRequested -= ShowDirectionUI;
        }
    }

    private Sprite GetArrowSprite(BoardNode from, BoardNode to)
    {
        Camera cam = Camera.main;

        if (cam == null)
            return rightArrow;

        Vector3 fromScreen = cam.WorldToScreenPoint(from.transform.position);
        Vector3 toScreen   = cam.WorldToScreenPoint(to.transform.position);

        Vector2 dir = (toScreen - fromScreen);

        Debug.Log(
            $"Node {from.nodeID} -> {to.nodeID} " +
            $" ScreenDir = {dir}"
        );

        // Hướng ngang chiếm ưu thế
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            return dir.x > 0 ? rightArrow : leftArrow;
        }
        // Hướng dọc chiếm ưu thế
        else
        {
            return dir.y > 0 ? upArrow : downArrow;
        }
    }

    public void ShowDirectionUI(BoardNode node)
    {
        Hide();

        panel.SetActive(true);

        for (int i = 0; i < node.nextNodes.Count; i++)
        {
            int index = i;

            Button btn = Instantiate(directionButtonPrefab, buttonRoot);

            Debug.Log(btn.transform.Find("ArrowImage"));

            spawnedButtons.Add(btn);

            btn.onClick.AddListener(() =>
            {
                int branch = (index == 0) ? 1 : 0;

                Debug.Log("Click Direction " + branch);

                BoardManager.Instance.SelectBranch(branch);

                Hide();
            });

            TMP_Text text = btn.GetComponentInChildren<TMP_Text>();

            if (text != null)
            {
                text.gameObject.SetActive(false);
            }

            Image arrow = btn.transform.Find("ArrowImage").GetComponent<Image>();

            if (arrow != null)
            {
                arrow.gameObject.SetActive(true);

                if (index == 0)
                    arrow.sprite = leftArrow;
                else
                    arrow.sprite = rightArrow;

                RectTransform rt = arrow.rectTransform;
                rt.sizeDelta = new Vector2(60, 60);
            }
        }
    }

    public void Hide()
    {
        panel.SetActive(false);

        foreach (var b in spawnedButtons)
        {
            if (b != null)
                Destroy(b.gameObject);
        }

        spawnedButtons.Clear();
    }
}