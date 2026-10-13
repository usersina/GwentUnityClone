using System;
using System.Collections.Generic;
using Gwent.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Before a battle: who to play against. Every AI deck (its leader's portrait, the deck's name, faction and leader)
// and a random opponent, two per row. Choosing one remembers it and starts the battle.
public class OpponentPicker : MonoBehaviour
{
    private const float EntryWidth = 600;
    private const float EntryHeight = 104;
    private const float RowStep = 116;

    public static void Open(Action on_close)
    {
        RectTransform dimmer = UIKit.Dimmer(UIOverlay.Root, "OpponentPicker");
        dimmer.gameObject.AddComponent<OpponentPicker>().Build(dimmer);
        UIOverlay.PushModal(dimmer.gameObject, on_close);
    }

    private void Build(RectTransform dimmer)
    {
        CardDatabase db = CardLibrary.Get();
        List<TextAsset> decks = OpponentDecks.All();
        int rows = (decks.Count + 2) / 2;
        RectTransform panel = UIKit.Panel(dimmer, "Panel").Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300, 230 + rows * RowStep + 70));

        TextMeshProUGUI title = UIKit.Text(panel, "Choose your opponent", UIKit.TextStyle.Heading, TextAlignmentOptions.Center);
        title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -64), new Vector2(1100, 64));
        UIKit.Ornament(panel, new Vector2(0.5f, 1), new Vector2(0, -112), 420);

        string chosen = OpponentDecks.Chosen;
        Button focus = Entry(panel, 0, null, "Random opponent", "A different deck every battle", "", chosen == "");
        for (int i = 0; i < decks.Count; i++)
        {
            Deck deck = CardLibrary.ParseDeck(decks[i].text, decks[i].name);
            if (deck == null)
                continue;
            string leader = db.Contains(deck.Leader) ? db.Get(deck.Leader).DisplayName : "";
            Sprite portrait = SpriteCache.Load("Cards/List/591x380/" + deck.Leader);
            Button entry = Entry(panel, i + 1, portrait, deck.Name, Factions.DisplayName(deck.Faction) + "  ·  " + leader, decks[i].name, decks[i].name == chosen);
            if (decks[i].name == chosen)
                focus = entry;
        }

        Button back = UIKit.Button(panel, "Back", UIKit.ButtonStyle.Secondary, () => UIOverlay.CloseModal(gameObject), new Vector2(240, 64));
        ((RectTransform)back.transform).Place(new Vector2(0.5f, 0), new Vector2(0, 58), new Vector2(240, 64));
        UIKit.Focus(focus);
    }

    // One opponent: portrait in a gold ring, name, faction and leader; lights up like a menu entry
    private Button Entry(RectTransform panel, int index, Sprite portrait, string name, string detail, string deck, bool current)
    {
        UITheme theme = UITheme.Current;
        Vector2 at = new Vector2(index % 2 == 0 ? -310 : 310, -190 - (index / 2) * RowStep);
        RectTransform root = UIKit.Rect("Opponent " + name, panel).Place(new Vector2(0.5f, 1), at, new Vector2(EntryWidth, EntryHeight));
        Image hit = root.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        Image band = UIKit.NewImage(root, "Band", theme.gradientH, Color.clear);
        band.rectTransform.Stretch();
        Image rule = UIKit.NewImage(root, "Rule", null, Color.clear);
        rule.rectTransform.anchorMin = new Vector2(0, 0.12f);
        rule.rectTransform.anchorMax = new Vector2(0, 0.88f);
        rule.rectTransform.pivot = new Vector2(0, 0.5f);
        rule.rectTransform.sizeDelta = new Vector2(4, 0);
        rule.rectTransform.anchoredPosition = Vector2.zero;

        Image mask = UIKit.NewImage(root, "Portrait", theme.circle, portrait != null ? Color.white : theme.panelRaised);
        mask.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(64, 0), new Vector2(80, 80));
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = portrait == null;
        if (portrait != null)
        {
            Image art = UIKit.NewImage(mask.transform, "Art", portrait, Color.white);
            art.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 140 * 591f / 380f), new Vector2(0.5f, 0.72f));
        }
        else
        {
            TextMeshProUGUI question = UIKit.Text(mask.transform, "?", UIKit.TextStyle.Heading, TextAlignmentOptions.Center);
            question.rectTransform.Stretch(0, 0, 0, 4);
        }
        Image ring = UIKit.NewImage(root, "Ring", theme.ring, theme.gold);
        ring.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(64, 0), new Vector2(88, 88));

        TextMeshProUGUI title = UIKit.Text(root, name, UIKit.TextStyle.Heading, TextAlignmentOptions.BottomLeft);
        title.fontSize = 30;
        title.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(124, 2), new Vector2(EntryWidth - 180, 44), new Vector2(0, 0));
        TextMeshProUGUI subtitle = UIKit.Text(root, detail, UIKit.TextStyle.Caption, TextAlignmentOptions.TopLeft);
        subtitle.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(124, -4), new Vector2(EntryWidth - 180, 34), new Vector2(0, 1));
        subtitle.textWrappingMode = TextWrappingModes.NoWrap;
        subtitle.overflowMode = TextOverflowModes.Ellipsis;
        if (current)
        {
            Image check = UIKit.NewImage(root, "Chosen", theme.iconCheck, theme.gold);
            check.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(-34, 0), new Vector2(32, 32));
        }

        Button button = root.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
        UIStateColors states = root.gameObject.AddComponent<UIStateColors>();
        states.selectOnHover = true;
        states.Add(band, UITheme.WithAlpha(theme.gold, 0), UITheme.WithAlpha(theme.gold, 0.18f), UITheme.WithAlpha(theme.gold, 0.28f));
        states.Add(rule, UITheme.WithAlpha(theme.gold, 0), theme.gold, theme.goldBright);
        states.Add(title, theme.text, theme.goldBright, theme.goldBright);
        button.onClick.AddListener(() =>
        {
            GameAudio.PlaySfx("ui_click");
            OpponentDecks.Chosen = deck;
            SceneManager.LoadScene(SceneNames.Game);
        });
        return button;
    }
}
