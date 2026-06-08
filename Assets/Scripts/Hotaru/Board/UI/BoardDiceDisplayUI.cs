using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Hiện số xúc xắc ở giữa màn hình rồi tự fade out.
/// SETUP:
///   1. Tạo TMP_Text ở center màn hình, font size lớn (80-120)
///   2. Attach script này vào cùng GameObject, gán diceText
///   3. Set alpha = 0 ban đầu
/// </summary>
public class BoardDiceDisplayUI : MonoBehaviour
{
    [SerializeField] private TMP_Text diceText;

    [Header("Timing")]
    [SerializeField] private float displayDuration = 1.5f;
    [SerializeField] private float fadeDuration    = 0.5f;

    private Coroutine _routine;

    private void Awake()
    {
        if (diceText != null) diceText.alpha = 0f;
    }

    public void ShowRoll(string playerName, int result)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowRoutine(result));
    }

    private IEnumerator ShowRoutine(int result)
    {
        diceText.text  = result.ToString();
        diceText.alpha = 1f;

        yield return new WaitForSeconds(displayDuration);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed       += Time.deltaTime;
            diceText.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }

        diceText.alpha = 0f;
        _routine = null;
    }
}