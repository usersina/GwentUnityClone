using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatsManager : MonoBehaviour
{
    public Sprite redRuby;
    public Sprite greyRuby;
    public Sprite avatar;
    public List<Sprite> factionShields;

    public void UISetGeneral(string deck_name)
    {
        switch (deck_name)
        {
            case "M":
                deck_name = "Monsters";
                transform.Find("Avatar").Find("faction").GetComponent<Image>().sprite = factionShields[0];
                break;
            case "NF":
                deck_name = "Nilfgaard";
                transform.Find("Avatar").Find("faction").GetComponent<Image>().sprite = factionShields[1];
                break;
            case "NR":
                deck_name = "Northern Realms";
                transform.Find("Avatar").Find("faction").GetComponent<Image>().sprite = factionShields[2];
                break;
            case "SC":
                deck_name = "Scoia'tel";
                transform.Find("Avatar").Find("faction").GetComponent<Image>().sprite = factionShields[3];
                break;
            //case "SK":
            //    deck_name = "Skellige";
            //    shield_faction = factionShields[4];
            //    break;

        }
        transform.Find("TextInfo").Find("Deck").GetComponent<TextMeshProUGUI>().text = deck_name;
    }

    public void UIUpdateTotalScore(List<int> close_list, List<int> range_list, List<int> siege_list)
    {
        int TotalScore = 0;
        foreach (int number in close_list)
            TotalScore += number;
        foreach (int number in range_list)
            TotalScore += number;
        foreach (int number in siege_list)
            TotalScore += number;

        //Debug.Log("Updating total score");
        transform.Find("Total").Find("Number").GetComponent<TextMeshProUGUI>().text = TotalScore.ToString();
    }

    public void UIUpdateHandCount(int cards_number)
    {
        transform.Find("HandCount").Find("Number").GetComponent<TextMeshProUGUI>().text = cards_number.ToString();
    }

    // Updates the gem lives
    public void UIUpdateGemLives(int lives)
    {
        Debug.Log("Updating the gem lives");
        switch (lives)
        {
            case 0:
                transform.Find("RubyL").GetComponent<Image>().sprite = greyRuby;
                transform.Find("RubyR").GetComponent<Image>().sprite = greyRuby;
                break;
            case 1:
                transform.Find("RubyL").GetComponent<Image>().sprite = greyRuby;
                transform.Find("RubyR").GetComponent<Image>().sprite = redRuby;
                break;
            case 2:
                transform.Find("RubyL").GetComponent<Image>().sprite = redRuby;
                transform.Find("RubyR").GetComponent<Image>().sprite = redRuby;
                break;
            default:
                Debug.LogError("UIUpdateGemLives: Unexpected value: " + lives);
                break;
        }
    }
}
