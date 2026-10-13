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
    private Image portraitCircle; // the round, masked picture
    private Image border;         // the silver ring around it
    private Image turnGlow;
    private Image passedVeil;
    private int shownTurnState = -1;

    private const float TurnPulseSeconds = 1.4f;
    private static readonly Color RingGold = new Color(1f, 0.72f, 0.16f);
    private static readonly Color RingGoldBright = new Color(1f, 0.9f, 0.45f);
    private static readonly Color PassedGrey = new Color(0.55f, 0.55f, 0.55f);

    // Looked up once instead of on every board refresh
    private void CacheReferences()
    {
        if (totalText != null)
            return;
        Transform avatar_t = transform.Find("Avatar");
        factionShield = avatar_t.Find("faction").GetComponent<Image>();
        Transform portrait_t = avatar_t.Find("Image").Find("Portrait");
        portrait = portrait_t != null ? portrait_t.GetComponent<Image>() : null;
        portraitCircle = avatar_t.Find("Image").GetComponent<Image>();
        border = avatar_t.Find("Border").GetComponent<Image>();
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
    // Whose turn it is shows on the portrait: its ring turns gold and glows, breathing. A side that has passed is
    // greyed out until the round ends.
    public void UISetTurnState(bool active, bool passed)
    {
        CacheReferences();
        if (turnGlow == null)
            CreateTurnMarkers();
        int state = passed ? 2 : active ? 1 : 0;
        if (state == 1)
        {
            float breath = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2 / TurnPulseSeconds);
            border.color = Color.Lerp(RingGold, RingGoldBright, breath);
            turnGlow.color = new Color(1f, 0.68f, 0.12f, Mathf.Lerp(0.55f, 1f, breath));
        }
        if (state == shownTurnState)
            return;
        shownTurnState = state;
        turnGlow.enabled = state == 1;
        passedVeil.enabled = state == 2;
        if (state != 1)
            border.color = state == 2 ? PassedGrey : Color.white;
        Color picture = state == 2 ? PassedGrey : Color.white;
        portraitCircle.color = picture;
        if (portrait != null)
            portrait.color = picture;
    }

    private void CreateTurnMarkers()
    {
        // A warm glow behind the ring, centred on the picture
        RectTransform circle = portraitCircle.rectTransform;
        turnGlow = UIKit.NewImage(border.transform.parent, "TurnGlow", UITheme.Current.radial, Color.clear);
        RectTransform glow = turnGlow.rectTransform;
        glow.anchorMin = glow.anchorMax = glow.pivot = new Vector2(0.5f, 0.5f);
        glow.sizeDelta = circle.rect.size * 2.5f;
        glow.position = circle.TransformPoint(circle.rect.center);
        glow.SetAsFirstSibling();
        turnGlow.enabled = false;

        // A grey veil over the picture of a side that has passed (inside its round mask)
        passedVeil = UIKit.NewImage(circle, "PassedVeil", null, new Color(0.28f, 0.28f, 0.3f, 0.55f));
        passedVeil.rectTransform.Stretch();
        passedVeil.transform.SetAsLastSibling();
        passedVeil.enabled = false;
    }

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
