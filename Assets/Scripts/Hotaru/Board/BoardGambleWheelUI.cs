using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BoardGambleWheelUI : MonoBehaviour
{
    [Header("Wheel References")]
    [SerializeField] private RectTransform wheelRoot;
    [SerializeField] private RectTransform arrowRoot;
    [SerializeField] private Image[] segmentImages;
    [SerializeField] private TMP_Text statusText;

    [Header("Spin Settings")]
    [SerializeField] private float spinDuration = 2.4f;
    [SerializeField] private float resultDisplayDuration = 2f;
    [SerializeField] private float extraSpinDegrees = 1080f;
    [SerializeField] private float wheelSlotAngle = 60f;
    [SerializeField] private float arrowOffsetAngle = 90f;
    [SerializeField] private float segmentRadius = 120f;
    [SerializeField] private float segmentSize = 90f;

    [Header("Visuals")]
    [SerializeField] private bool useOverrideSprites = false;
    [SerializeField] private Sprite[] overrideSprites;

    private Coroutine spinRoutine;
    private System.Action<int> onCompleted;

    private void Awake()
    {
        EnsureWheelSetup();
        gameObject.SetActive(false);
    }

    public void ShowWheel(int resultIndex, System.Action<int> onComplete = null)
    {
        if (spinRoutine != null)
        {
            StopCoroutine(spinRoutine);
            spinRoutine = null;
        }

        onCompleted = onComplete;
        EnsureWheelSetup();
        PopulateSegments();
        gameObject.SetActive(true);
        spinRoutine = StartCoroutine(RunSpin(resultIndex));
    }

    private IEnumerator RunSpin(int resultIndex)
    {
        if (wheelRoot == null)
        {
            spinRoutine = null;
            yield break;
        }

        float startAngle = wheelRoot.localEulerAngles.z;
        float targetRotation = startAngle + extraSpinDegrees + GetTargetRotationForResult(resultIndex);

        if (statusText != null)
            statusText.text = "Spinning...";

        float elapsed = 0f;
        while (elapsed < spinDuration)
        {
            float t = elapsed / spinDuration;
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            float angle = Mathf.Lerp(startAngle, targetRotation, easedT);
            wheelRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
            elapsed += Time.deltaTime;
            yield return null;
        }

        wheelRoot.localRotation = Quaternion.Euler(0f, 0f, targetRotation);

        bool isGain = resultIndex == 0 || resultIndex == 2 || resultIndex == 4;
        if (statusText != null)
            statusText.text = isGain ? "GAIN ITEM" : "LOSE ITEM";

        yield return new WaitForSeconds(resultDisplayDuration);

        onCompleted?.Invoke(resultIndex);
        gameObject.SetActive(false);
        spinRoutine = null;
    }

    private float GetTargetRotationForResult(int resultIndex)
    {
        if (resultIndex < 0 || resultIndex >= 6)
            resultIndex = 0;

        float slotAngle = wheelSlotAngle;
        float baseOffset = 360f - (resultIndex * slotAngle);
        return baseOffset + arrowOffsetAngle;
    }

    private void PopulateSegments()
    {
        if (segmentImages == null || segmentImages.Length == 0)
            return;

        for (int i = 0; i < segmentImages.Length; i++)
        {
            var image = segmentImages[i];
            if (image == null)
                continue;

            image.gameObject.SetActive(true);
            bool isGain = i == 0 || i == 2 || i == 4;
            image.color = isGain ? new Color(0.3f, 0.85f, 0.4f, 1f) : new Color(0.95f, 0.35f, 0.35f, 1f);

            if (useOverrideSprites && overrideSprites != null && i < overrideSprites.Length && overrideSprites[i] != null)
            {
                image.sprite = overrideSprites[i];
            }
            else
            {
                image.sprite = CreateSegmentSprite(isGain ? "GAIN" : "LOSE");
            }

            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            image.rectTransform.localScale = Vector3.one;
        }
    }

    private Sprite CreateSegmentSprite(string label)
    {
        Texture2D tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        Color bg = label == "GAIN" ? new Color(0.28f, 0.85f, 0.37f, 1f) : new Color(0.95f, 0.35f, 0.35f, 1f);

        for (int x = 0; x < tex.width; x++)
        {
            for (int y = 0; y < tex.height; y++)
            {
                Vector2 p = new Vector2(x - tex.width / 2f, y - tex.height / 2f);
                float dist = p.magnitude;
                bool inside = dist < 54f;
                tex.SetPixel(x, y, inside ? bg : Color.clear);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    private void EnsureWheelSetup()
    {
        if (wheelRoot == null)
        {
            GameObject wheelGo = new GameObject("WheelRoot");
            wheelGo.transform.SetParent(transform, false);
            wheelRoot = wheelGo.AddComponent<RectTransform>();
            wheelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            wheelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            wheelRoot.sizeDelta = new Vector2(300f, 300f);
            wheelRoot.anchoredPosition = Vector2.zero;
        }

        if (arrowRoot == null)
        {
            GameObject arrowGo = new GameObject("Arrow");
            arrowGo.transform.SetParent(transform, false);
            arrowRoot = arrowGo.AddComponent<RectTransform>();
            arrowRoot.anchorMin = new Vector2(0.5f, 0.5f);
            arrowRoot.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRoot.sizeDelta = new Vector2(40f, 80f);
            arrowRoot.anchoredPosition = new Vector2(0f, 180f);

            var arrowImage = arrowGo.AddComponent<Image>();
            arrowImage.color = Color.yellow;
        }

        if (segmentImages == null || segmentImages.Length == 0)
        {
            List<Image> images = new List<Image>();
            for (int i = 0; i < 6; i++)
            {
                GameObject segGo = new GameObject($"Segment{i}");
                segGo.transform.SetParent(wheelRoot, false);

                RectTransform segRect = segGo.AddComponent<RectTransform>();
                segRect.anchorMin = new Vector2(0.5f, 0.5f);
                segRect.anchorMax = new Vector2(0.5f, 0.5f);
                segRect.sizeDelta = new Vector2(segmentSize, segmentSize);
                segRect.anchoredPosition = Vector2.zero;

                float angle = i * wheelSlotAngle;
                Vector2 pos = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * segmentRadius;
                segRect.anchoredPosition = pos;

                var img = segGo.AddComponent<Image>();
                bool isGain = i == 0 || i == 2 || i == 4;
                img.color = isGain ? new Color(0.3f, 0.85f, 0.4f, 1f) : new Color(0.95f, 0.35f, 0.35f, 1f);
                images.Add(img);
            }

            segmentImages = images.ToArray();
        }

        if (statusText == null)
        {
            GameObject textGo = new GameObject("StatusText");
            textGo.transform.SetParent(transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.1f);
            textRect.anchorMax = new Vector2(0.5f, 0.1f);
            textRect.sizeDelta = new Vector2(220f, 40f);
            textRect.anchoredPosition = Vector2.zero;
            statusText = textGo.AddComponent<TextMeshProUGUI>();
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.fontSize = 24;
            statusText.color = Color.white;
        }
    }
}
