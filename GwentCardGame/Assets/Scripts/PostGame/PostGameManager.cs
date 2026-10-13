using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PostGameManager : MonoBehaviour
{
    public Image outcomeImage;
    public List<Sprite> outComes;
    public List<TextMeshProUGUI> playerScores;
    public List<TextMeshProUGUI> enemyScores;
    // Themed title over a soft glow (outcomeImage); without it, the old outcome sprites are shown
    public TextMeshProUGUI outcomeTitle;


    public void ShowScores(List<int> pl_scores, List<int> opp_scores)
    {
        for (int i = 0; i < 3; i++)
        {
            if (i < pl_scores.Count)
                playerScores[i].text = pl_scores[i].ToString();

            if (i < opp_scores.Count)
                enemyScores[i].text = opp_scores[i].ToString();

            // The round's loser is dimmed, the winner stays gold
            if (i < pl_scores.Count && i < opp_scores.Count)
                if (pl_scores[i] < opp_scores[i])
                    playerScores[i].color = UITheme.Current.textMuted;
                else
                    enemyScores[i].color = UITheme.Current.textMuted;
        }
    }

    public void ShowDraw()
    {
        ShowOutcome(0, "Draw", UITheme.Current.textSecondary);
    }

    public void ShowWin()
    {
        ShowOutcome(1, "Victory", UITheme.Current.goldBright);
    }

    public void ShowLose()
    {
        ShowOutcome(2, "Defeat", UITheme.Current.danger);
    }

    private void ShowOutcome(int index, string title, Color color)
    {
        if (outcomeTitle == null)
        {
            outcomeImage.sprite = outComes[index];
            return;
        }
        outcomeTitle.text = title;
        outcomeTitle.color = color;
        outcomeImage.color = UITheme.WithAlpha(color, 0.2f);
    }
    
    // Reference: Button
    public void ToMainMenu()
    {
        GameAudio.PlaySfx("ui_click");
        SceneManager.LoadScene(SceneNames.Menu);
    }
}
