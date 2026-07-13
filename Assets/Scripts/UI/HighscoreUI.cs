using UnityEngine;
using UnityEngine.UI;

public class HighscoreUI : MonoBehaviour
{
    public Text number;
    public Text playerName;
    public InputField inputName;
    public Text score;

    public void SetData(int rank, string displayName, int scoreValue)
    {
        if (number != null)
            number.text = rank.ToString();

        if (playerName != null)
            playerName.text = string.IsNullOrEmpty(displayName) ? "Player" : displayName;

        if (inputName != null)
            inputName.text = string.IsNullOrEmpty(displayName) ? "Player" : displayName;

        if (score != null)
            score.text = scoreValue.ToString();
    }
}
