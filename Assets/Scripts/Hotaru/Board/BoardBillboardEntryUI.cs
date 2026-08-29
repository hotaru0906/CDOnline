using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;

public class BoardBillboardEntryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image characterIcon;
    [SerializeField] private Image backgroundImage;

    [Header("Character Icons")]
    [Tooltip("Index khớp với CharacterIndex (0-3)")]
    [SerializeField] private Sprite[] characterIcons;

    [Header("Character Backgrounds")]
    [Tooltip("Index khớp với CharacterIndex (0-3), background tương ứng với từng avatar")]
    [SerializeField] private Sprite[] characterBackgrounds;

    [Header("First Turn Glow")]
    [SerializeField] private Image borderGlow; // Image viền, để sẵn alpha=0 hoặc màu trung tính lúc chưa glow
    [SerializeField] private Color glowColor = new Color(1f, 0.85f, 0.1f, 1f); // vàng lấp lánh
    [SerializeField] private float glowPulseDuration = 0.5f;

    [Header("Retry")]
    [SerializeField] private float retryTimeout = 4f;
    [SerializeField] private float retryInterval = 0.15f;

    [Header("Turn Highlight")]
    [SerializeField] private Vector3 normalScale = Vector3.one;
    [SerializeField] private Vector3 activeScale = new Vector3(1.08f, 1.08f, 1.08f);
    [SerializeField] private float turnScaleDuration = 0.18f;

    private int _playerId = -1;
    private Coroutine _refreshRoutine;
    private Sequence _glowSequence;
    private bool _isActiveTurn;

    public void SetPlayerId(int playerId)
    {
        _playerId = playerId;
        ResolveReferences();

        if (_refreshRoutine != null)
            StopCoroutine(_refreshRoutine);

        ApplyFallback();
        _refreshRoutine = StartCoroutine(RefreshRoutine());
    }

    private void ResolveReferences()
    {
        if (characterIcon == null)
        {
            var icon = transform.Find("Player Icon")?.GetComponent<Image>();
            if (icon != null)
                characterIcon = icon;
        }

        if (backgroundImage == null)
        {
            var bg = transform.Find("Background")?.GetComponent<Image>();
            if (bg == null)
                bg = transform.Find("Background Image")?.GetComponent<Image>();

            if (bg == null)
            {
                foreach (var image in GetComponentsInChildren<Image>(true))
                {
                    if (image != null && image != characterIcon)
                    {
                        bg = image;
                        break;
                    }
                }
            }

            backgroundImage = bg;
        }
    }

    private void ApplyFallback()
    {
        if (nameText != null)
            nameText.text = BoardHUDController.Instance != null
                ? BoardHUDController.Instance.GetPlayerName(_playerId)
                : $"Player {_playerId}";

        if (transform.localScale == Vector3.zero)
            transform.localScale = normalScale;
    }

    public void SetTurnActive(bool active)
    {
        _isActiveTurn = active;

        if (gameObject == null) return;

        var targetScale = active ? activeScale : normalScale;
        transform.DOScale(targetScale, turnScaleDuration).SetEase(Ease.OutBack);
    }

    private IEnumerator RefreshRoutine()
    {
        float elapsed = 0f;

        while (elapsed < retryTimeout)
        {
            if (TryApplyRealData())
                yield break;

            elapsed += retryInterval;
            yield return new WaitForSeconds(retryInterval);
        }

        Debug.LogWarning($"[BoardBillboardEntryUI] Timeout tìm data cho P{_playerId}, giữ fallback.");
    }

    private bool TryApplyRealData()
    {
        var playerData = FindPlayerData(_playerId);
        if (playerData == null || playerData.Object == null || !playerData.Object.IsValid)
            return false;

        string playerName = playerData.PlayerName.ToString();
        if (nameText != null)
            nameText.text = !string.IsNullOrWhiteSpace(playerName) ? playerName : $"Player {_playerId}";

        int characterIndex = GameManager.Instance != null
            ? GameManager.Instance.GetPlayerCharacter(_playerId)
            : 0;

        if (characterIcon != null && characterIcons != null &&
            characterIndex >= 0 && characterIndex < characterIcons.Length &&
            characterIcons[characterIndex] != null)
        {
            characterIcon.sprite = characterIcons[characterIndex];
        }

        if (backgroundImage != null && characterBackgrounds != null &&
            characterIndex >= 0 && characterIndex < characterBackgrounds.Length &&
            characterBackgrounds[characterIndex] != null)
        {
            backgroundImage.sprite = characterBackgrounds[characterIndex];
            backgroundImage.enabled = true;
        }
        else if (backgroundImage != null)
        {
            backgroundImage.sprite = null;
            backgroundImage.enabled = false;
        }

        return true;
    }

    private PlayerNetworkData FindPlayerData(int playerId)
    {
        var all = FindObjectsByType<PlayerNetworkData>(FindObjectsSortMode.None);
        foreach (var p in all)
        {
            if (p == null || p.Object == null) continue;
            if (p.Object.InputAuthority.PlayerId == playerId)
                return p;
        }
        return null;
    }

    // =====================================================================
    // FIRST TURN GLOW
    // =====================================================================

    public void StartFirstTurnGlow()
    {
        if (borderGlow == null) return;

        StopFirstTurnGlow(); // đảm bảo không chồng sequence cũ

        borderGlow.gameObject.SetActive(true);
        borderGlow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);

        _glowSequence = DOTween.Sequence();
        _glowSequence.Append(borderGlow.DOFade(1f, glowPulseDuration).SetEase(Ease.InOutSine));
        _glowSequence.Append(borderGlow.DOFade(0.3f, glowPulseDuration).SetEase(Ease.InOutSine));
        _glowSequence.SetLoops(-1); // lặp vô hạn cho tới khi StopFirstTurnGlow() gọi
    }

    public void StopFirstTurnGlow()
    {
        if (_glowSequence != null)
        {
            _glowSequence.Kill();
            _glowSequence = null;
        }

        if (borderGlow != null)
        {
            borderGlow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
            borderGlow.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        _glowSequence?.Kill();
    }
}