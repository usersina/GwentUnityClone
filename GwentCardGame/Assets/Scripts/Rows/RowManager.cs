using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RowManager : MonoBehaviour
{
    public GameObject CardPrefab;

    private GameObject unitRow;
    private GameObject specialRow;
    private TextMeshProUGUI scoreText;

    private GameObject UnitRow { get { CacheReferences(); return unitRow; } }
    private GameObject SpecialRow { get { CacheReferences(); return specialRow; } }

    private void CacheReferences()
    {
        if (unitRow != null)
            return;
        unitRow = transform.Find("Unit").gameObject;
        specialRow = transform.Find("Special").gameObject;
        scoreText = transform.Find("ScoreCount").Find("Number").GetComponent<TextMeshProUGUI>();
    }


    //
    public void UpdateRowScore(List<int> row_list)
    {
        int row_strength = 0;
        foreach (int number in row_list)
            row_strength += number;
        CacheReferences();
        scoreText.text = row_strength.ToString();
    }

    // Updates each individual unit's strength based on
    // E.G: CloseList && CloseStrengthList
    // The strength labels of the row's cards. The row shows its cards sorted (The Witcher 3's order) while the
    // game keeps them in play order, so each card takes the strength of a game entry with its id.
    public IEnumerator UIUpdateUnitsStrength(List<int> card_ids, List<int> orig_strengths, List<int> mod_strengths)
    {
        GameObject unit_go = UnitRow;

        // This line is a must else it'll get the previous frame's child (which was destroyed)
        // Hence null
        //yield return new WaitForEndOfFrame(); // This gives a slight perception of the new frame
        yield return null; // This updates the new frame without eye perception (directly)

        // Only card children: the row also holds the weather overlay
        List<CardFace> cards = new List<CardFace>();
        foreach (Transform child in unit_go.transform)
        {
            CardFace display = child.GetComponent<CardFace>();
            if (display != null)
                cards.Add(display);
        }

        if (cards.Count != card_ids.Count || orig_strengths.Count != card_ids.Count || mod_strengths.Count != card_ids.Count)
        {
            Debug.LogError("UIUpdateUnitsStrength: params mismatch (cards " + cards.Count + ", ids " + card_ids.Count
                + ", original " + orig_strengths.Count + ", modified " + mod_strengths.Count + ")");
            yield break;
        }

        bool[] used = new bool[card_ids.Count];
        foreach (CardFace card in cards)
        {
            int id = card.cardStats.GetComponent<CardStats>()._id;
            int i = 0;
            while (i < card_ids.Count && (used[i] || card_ids[i] != id))
                i++;
            if (i == card_ids.Count)
                continue;
            used[i] = true;
            TextMeshProUGUI strength_text = card.strengthImage.GetComponent<TextMeshProUGUI>();
            // Red when weakened, green when boosted, else the card's own colour: card objects are reused
            // between refreshes, so a card back to its base strength must lose the red / green
            if (orig_strengths[i] > mod_strengths[i])
                strength_text.color = new Color32(188, 0, 0, 255);
            else if (orig_strengths[i] < mod_strengths[i])
                strength_text.color = new Color32(0, 188, 0, 255);
            else
                strength_text.color = card.cardStats.GetComponent<CardStats>().unique ? Color.white : Color.black;
            strength_text.text = mod_strengths[i].ToString();
        }
    }
}
