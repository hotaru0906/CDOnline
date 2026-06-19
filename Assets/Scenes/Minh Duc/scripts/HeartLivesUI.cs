using UnityEngine;
using TMPro;

public class HeartLivesUI : MonoBehaviour
{
    public TMP_Text livesText;

    public GameObject heart1;
    public GameObject heart2;
    public GameObject heart3;

    void Update()
    {
        if (livesText == null)
            return;

        int lives = 0;
        int.TryParse(livesText.text, out lives);

        if (heart1 != null) heart1.SetActive(lives >= 1);
        if (heart2 != null) heart2.SetActive(lives >= 2);
        if (heart3 != null) heart3.SetActive(lives >= 3);
    }
}