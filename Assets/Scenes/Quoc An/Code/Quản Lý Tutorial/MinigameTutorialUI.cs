using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

public class MinigameTutorialUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RawImage videoDisplay;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private Transform controlContainer;
    [SerializeField] private GameObject controlLinePrefab;

    [Header("Test Data — Inspector Only")]
    [SerializeField] private MinigameTutorialData testData;

    // ─── TEST ─────────────────────────────────────────
    [ContextMenu("TEST — Preview Tutorial")]
    private void PreviewInEditor() => Show(testData);

    [ContextMenu("TEST — Hide Tutorial")]
    private void HideInEditor() => Hide();

    // ─── PUBLIC API ───────────────────────────────────
    public void Show(MinigameTutorialData data)
    {
        if (data == null) return;

        SetupVideo(data.tutorialVideo);
        SetupControls(data.controls);
        SetVisible(true);
    }

    public void Hide()
    {
        videoPlayer.Stop();
        SetVisible(false);
    }

    // ─── PRIVATE ──────────────────────────────────────
    private void SetupVideo(VideoClip clip)
    {
        if (clip == null) return;

        videoPlayer.clip = clip;

        // Render video vào RenderTexture rồi gán vào RawImage
        var rt = new RenderTexture((int)clip.width, (int)clip.height, 0);
        videoPlayer.targetTexture = rt;
        videoDisplay.texture = rt;

        videoPlayer.isLooping = true;
        videoPlayer.Play();
    }

    private void SetupControls(List<TutorialControlData> controls)
    {
        // Xóa row cũ
        foreach (Transform child in controlContainer)
        {
#if UNITY_EDITOR
            DestroyImmediate(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }

        if (controls == null) return;

        foreach (var data in controls)
        {
            var go = Instantiate(controlLinePrefab, controlContainer);
            SetupControlLine(go, data);
        }
    }

    private void SetupControlLine(GameObject go, TutorialControlData data)
    {
        // KeyButton — Image background
        var keyButton = go.transform.Find("KeyButton");
        if (keyButton != null)
        {
            var bg = keyButton.GetComponent<Image>();
            if (bg != null && data.keyIcon != null)
                bg.sprite = data.keyIcon;

            var keyTMP = keyButton.GetComponentInChildren<TextMeshProUGUI>();
            if (keyTMP != null)
                keyTMP.text = data.keyLabel;
        }

        // Text_Explanation
        var explanation = go.transform.Find("Text_Explanation");
        if (explanation != null)
        {
            var tmp = explanation.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
                tmp.text = data.explanation;
        }
    }

    private void SetVisible(bool visible)
    {
        canvasGroup.alpha          = visible ? 1f : 0f;
        canvasGroup.interactable   = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    // ─── PHOTON FUSION (stub) ─────────────────────────
    // Không cần sync tutorial qua network —
    // mỗi client tự load data theo minigame scene hiện tại.
}