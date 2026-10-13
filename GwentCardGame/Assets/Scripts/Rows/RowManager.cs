using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RowManager : MonoBehaviour
{
    public GameObject CardPrefab;

    // This is the main field_go, either Close / Range / Siege (which has a unit and a special child)
    public void ClearUnitObject()
    {
        GameObject unit_go = gameObject.transform.Find("Unit").gameObject;
        for (int i = 0; i < unit_go.transform.childCount; i++)
        {
            Destroy(unit_go.transform.GetChild(i).gameObject);
        }
        // Do not remove this, otherwise count will still get the previous frame's count (NOT 0);
        unit_go.transform.DetachChildren();
    }
    public void ClearSpecialObject()
    {
        GameObject special_go = gameObject.transform.Find("Special").gameObject;
        for (int i = 0; i < special_go.transform.childCount; i++)
        {
            Destroy(special_go.transform.GetChild(i).gameObject);
        }
        special_go.transform.DetachChildren();
    }

    //
    public void ResizeUnitObject(int numberOfCards)
    {
        GameObject unit_go = gameObject.transform.Find("Unit").gameObject;
        // Initial Spacing
        unit_go.GetComponent<GridLayoutGroup>().spacing = new Vector2(6, 0);

        float maxWidth = unit_go.GetComponent<RectTransform>().rect.width;
        float spacingX = unit_go.GetComponent<GridLayoutGroup>().spacing.x;
        float cardWidth = 87;

        float rowWidth = numberOfCards * cardWidth + (numberOfCards - 1) * spacingX;
        float offset = 100;
        if (numberOfCards > 1 && rowWidth > maxWidth - offset)
        {
            // n cards + (n - 1) gaps must fit in the row
            spacingX = (maxWidth - offset - numberOfCards * cardWidth) / (numberOfCards - 1);
            unit_go.GetComponent<GridLayoutGroup>().spacing = new Vector2(spacingX, 0);
        }
    }
    // Updates the whole row's score
    public void UpdateRowScore(List<int> row_list)
    {
        int row_strength = 0;
        foreach (int number in row_list)
            row_strength += number;
        transform.Find("ScoreCount").Find("Number").GetComponent<TextMeshProUGUI>().text = row_strength.ToString();
    }

    // Updates each individual unit's strength based on
    // E.G: CloseList && CloseStrengthList
    public IEnumerator UIUpdateUnitsStrength(List<int> orig_strengths, List<int> mod_strengths)
    {
        GameObject unit_go = gameObject.transform.Find("Unit").gameObject;

        // This line is a must else it'll get the previous frame's child (which was destroyed)
        // Hence null
        //yield return new WaitForEndOfFrame(); // This gives a slight perception of the new frame
        yield return null; // This updates the new frame without eye perception (directly)

        // Only card children: the row also holds the weather overlay
        List<CardDisplay> cards = new List<CardDisplay>();
        foreach (Transform child in unit_go.transform)
        {
            CardDisplay display = child.GetComponent<CardDisplay>();
            if (display != null)
                cards.Add(display);
        }

        if (cards.Count == orig_strengths.Count && orig_strengths.Count == mod_strengths.Count)
        {
            for (int i = 0; i < orig_strengths.Count; i++)
            {
                TextMeshProUGUI strength_text = cards[i].strengthImage.GetComponent<TextMeshProUGUI>();
                if (orig_strengths[i] > mod_strengths[i])
                    strength_text.color = new Color32(188, 0, 0, 255);
                else if (orig_strengths[i] < mod_strengths[i])
                    strength_text.color = new Color32(0, 188, 0, 255);
                strength_text.text = mod_strengths[i].ToString();
            }
        }
        else
        {
            Debug.LogError("UIUpdateUnitsStrength: params mismatch (cards " + cards.Count
                + ", original " + orig_strengths.Count + ", modified " + mod_strengths.Count + ")");
        }
    }
}
