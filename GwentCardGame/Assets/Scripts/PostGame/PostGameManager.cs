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


    public void ShowScores(List<int> pl_scores, List<int> opp_scores)
    {
        for (int i = 0; i < 3; i++)
        {
            if (i < pl_scores.Count)
                playerScores[i].text = pl_scores[i].ToString();

            if (i < opp_scores.Count)
                enemyScores[i].text = opp_scores[i].ToString();

            if (i < pl_scores.Count && i < opp_scores.Count)
                if (pl_scores[i] < opp_scores[i])
                    playerScores[i].color = new Color32(255, 255, 255, 255);
                else
                    enemyScores[i].color = new Color32(255, 255, 255, 255);
        }
    }

    public void ShowDraw()
    {
        outcomeImage.sprite = outComes[0];
    }

    public void ShowWin()
    {
        outcomeImage.sprite = outComes[1];
    }

    public void ShowLose()
    {
        outcomeImage.sprite = outComes[2];
    }
    
    // Reference: Button
    public void ToMainMenu()
    {
        SceneManager.LoadScene(0);
    }
}
