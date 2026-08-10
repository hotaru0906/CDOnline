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

    [Header("Character Icons")]
    [Tooltip("Index khớp với CharacterIndex (0-3)")]
    [SerializeField] private Sprite[] characterIcons;

    [Header("First Turn Glow")]
    [SerializeField] private Image borderGlow; // Image viền, để sẵn alpha=0 hoặc màu trung tính lúc chưa glow
    [SerializeField] private Color glowColor = new Color(1f, 0.85f, 0.1f, 1f); // vàng lấp lánh
    [SerializeField] private float glowPulseDuration = 0.5f;

    [Header("Retry")]
    [SerializeField] private float retryTimeout = 4f;
    [SerializeField] private float retryInterval = 0.15f;

    private int _playerId = -1;
    private Coroutine _refreshRoutine;
    private Sequence _glowSequence;

    public void SetPlayerId(int playerId)
    {
        _playerId = playerId;

        if (_refreshRoutine != null)
            StopCoroutine(_refreshRoutine);

        ApplyFallback();
        _refreshRoutine = StartCoroutine(RefreshRoutine());
    }

    private void ApplyFallback()
    {
        if (nameText != null)
            nameText.text = BoardHUDController.Instance != null
                ? BoardHUDController.Instance.GetPlayerName(_playerId)
                : $"Player {_playerId}";
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