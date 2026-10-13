using System.Collections.Generic;
using Gwent.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A player's panel on the battle board: portrait, name, faction, hand count, life gems and total score
public class StatsManager : MonoBehaviour
{
    public Sprite redRuby;
    public Sprite greyRuby;
    public Sprite avatar;
    public List<Sprite> factionShields; // Monsters, Nilfgaard, Northern Realms, Scoia'tael

    private Image factionShield;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI deckText;
    private TextMeshProUGUI totalText;
    private TextMeshProUGUI handCountText;
    private Image rubyLeft;
    private Image rubyRight;
    private Image portrait;

    // Looked up once instead of on every board refresh
    private void CacheReferences()
    {
        if (totalText != null)
            return;
        Transform avatar_t = transform.Find("Avatar");
        factionShield = avatar_t.Find("faction").GetComponent<Image>();
        Transform portrait_t = avatar_t.Find("Image").Find("Portrait");
        portrait = portrait_t != null ? portrait_t.GetComponent<Image>() : null;
        nameText = transform.Find("TextInfo").Find("Name").GetComponent<TextMeshProUGUI>();
        deckText = transform.Find("TextInfo").Find("Deck").GetComponent<TextMeshProUGUI>();
        totalText = transform.Find("Total").Find("Number").GetComponent<TextMeshProUGUI>();
        handCountText = transform.Find("HandCount").Find("Number").GetComponent<TextMeshProUGUI>();
        rubyLeft = transform.Find("RubyL").GetComponent<Image>();
        rubyRight = transform.Find("RubyR").GetComponent<Image>();
    }

    public void UISetGeneral(string faction)
    {
        CacheReferences();
        string faction_name = faction;
        switch (faction)
        {
            case Factions.Monsters:
                faction_name = "Monsters";
                factionShield.sprite = factionShields[0];
                break;
            case Factions.Nilfgaard:
                faction_name = "Nilfgaard";
                factionShield.sprite = factionShields[1];
                break;
            case Factions.NorthernRealms:
                faction_name = "Northern Realms";
                factionShield.sprite = factionShields[2];
                break;
            case Factions.Scoiatael:
                faction_name = "Scoia'tael";
                factionShield.sprite = factionShields[3];
                break;
        }
        deckText.text = faction_name;
    }

    // Player name, and the leader card's art in the round portrait frame (Avatar/Image masks Avatar/Image/Portrait)
    public void UISetPlayer(string player_name, Sprite portrait_sprite)
    {
        CacheReferences();
        nameText.text = player_name;
        if (portrait == null || portrait_sprite == null)
            return;
        portrait.sprite = portrait_sprite;
        portrait.enabled = true;
    }

    public void UIUpdateTotalScore(List<int> close_list, List<int> range_list, List<int> siege_list)
    {
        CacheReferences();
        int total = 0;
        foreach (int number in close_list)
            total += number;
        foreach (int number in range_list)
            total += number;
        foreach (int number in siege_list)
            total += number;
        totalText.text = total.ToString();
    }

    public void UIUpdateHandCount(int cards_number)
    {
        CacheReferences();
        handCountText.text = cards_number.ToString();
    }

    // Red gems for the lives left, grey for the lost ones
    // The gem a side loses when it is down to this many lives (the left one goes first)
    public Image LostGem(int lives)
    {
        CacheReferences();
        return lives >= 1 ? rubyLeft : rubyRight;
    }

    public void UIUpdateGemLives(int lives)
    {
        CacheReferences();
        if (lives < 0 || lives > 2)
        {
            Debug.LogError("UIUpdateGemLives: Unexpected value: " + lives);
            return;
        }
        rubyLeft.sprite = lives >= 2 ? redRuby : greyRuby;
        rubyRight.sprite = lives >= 1 ? redRuby : greyRuby;
    }
}
