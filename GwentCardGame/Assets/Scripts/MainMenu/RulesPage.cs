using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "How to play" (main menu): the rules of The Witcher 3's Gwent, one topic at a time, with the game's own icons.
// Topics on the left, the topic's text on the right (it scrolls when long).
public class RulesPage : MonoBehaviour
{
    private struct Entry
    {
        public string Icon;  // Resources path, or null
        public string Title;
        public string Text;

        public Entry(string icon, string title, string text)
        {
            Icon = icon;
            Title = title;
            Text = text;
        }
    }

    private struct Topic
    {
        public string Name;
        public string Intro;
        public Entry[] Entries;
    }

    private static readonly Topic[] Topics =
    {
        new Topic
        {
            Name = "The battle",
            Intro = "Two players, up to three rounds: win two rounds to win the battle.",
            Entries = new[]
            {
                new Entry(null, "Your hand", "You draw 10 cards and may redraw up to 2 of them before the first round. Nobody draws between rounds, so every card you play is one fewer for the rounds ahead; only spies, a few leaders and the Northern Realms perk bring more."),
                new Entry(null, "Your turn", "Do one thing: play a card, use your leader's ability, or pass. Playing a card ends your turn."),
                new Entry(null, "Passing", "Passing is final for the round: you play nothing more in it, while your opponent may go on playing. Once both sides have passed, the higher total wins the round. A side with no cards and no leader ability left passes on its own."),
                new Entry(null, "Rounds and gems", "Each side has two gems and loses one with each round it loses; a drawn round costs both (unless Nilfgaard plays). The winner of a round starts the next one, and the board is cleared in between."),
                new Entry(null, "Playing well", "Winning a round by a mile wastes cards. Letting a round go and keeping a strong hand for the other two is often the better play."),
            },
        },
        new Topic
        {
            Name = "The board",
            Intro = "Each side has three rows; your rows are the lower half of the table.",
            Entries = new[]
            {
                new Entry("Cards/Row/close", "Close combat, ranged, siege", "A unit goes to the row shown on its card. Its strength is the number in the corner; each row's total is on its left, each side's total next to its portrait. The portrait of the side whose turn it is glows gold; a side that has passed is greyed out."),
                new Entry("Cards/Row/close_range", "Agile", "Can go to the close combat or the ranged row."),
                new Entry("Cards/Special/commander_horn", "Horn slot", "The slot at the left of each row takes a Commander's Horn, one per row."),
                new Entry("Cards/Special/biting_frost", "Weather board", "Weather cards and Scorch are played on the weather board, at the left of the table. Weather affects both sides."),
                new Entry(null, "Discard piles", "Cards that leave the board go to their owner's discard pile, next to the deck. Click a pile to look through it."),
            },
        },
        new Topic
        {
            Name = "Cards",
            Intro = "A deck has one leader, at least 22 unit cards and at most 10 special cards. It holds each card at most as many times as it exists: most cards once, a few up to three times or more.",
            Entries = new[]
            {
                new Entry("Cards/Nature/unique", "Heroes", "The gold-framed units. Nothing changes them: no weather, horn, morale boost, scorch, decoy or medic. A hero can be in a deck only once."),
                new Entry("Cards/Nature/not_unique", "Units", "Everything else on the rows. Most exist once; a few, such as the Mahakaman Defender, several times."),
                new Entry("Cards/Special/decoy", "Special cards", "Weather, Commander's Horn, Decoy and Scorch: they shape the board instead of adding strength."),
                new Entry(null, "Leader", "One ability you can use once per battle. A few leaders' abilities are always in effect instead."),
            },
        },
        new Topic
        {
            Name = "Abilities",
            Intro = "Shown by the icon at the bottom of a unit card.",
            Entries = new[]
            {
                new Entry("Cards/Ability/spy", "Spy", "Goes on your opponent's side (adding to their total) and you draw 2 cards."),
                new Entry("Cards/Ability/medic", "Medic", "Brings a unit back from your discard pile and plays it (no heroes). A medic brought back heals again."),
                new Entry("Cards/Ability/muster", "Muster", "Plays every card of the same family from your deck and hand at once."),
                new Entry("Cards/Ability/tight_bond", "Tight bond", "Copies of this card on the same row multiply each other's strength."),
                new Entry("Cards/Ability/morale_boost", "Morale boost", "Adds 1 to every other unit on its row."),
                new Entry("Cards/Ability/commander_horn", "Commander's horn", "Doubles the other units on its row, unless the row already has a Commander's Horn."),
                new Entry("Cards/Ability/scorch", "Scorch", "Destroys your opponent's strongest unit(s) on that row, if the row totals 10 or more."),
                new Entry("Cards/Ability/agile", "Agile", "Close combat or ranged, your choice."),
                new Entry(null, "Avenger", "The Cow: when it goes to the discard pile, the Bovine Defense Force joins your close combat row for the next round."),
            },
        },
        new Topic
        {
            Name = "Special cards",
            Intro = "Weather lowers every non-hero unit of its rows to 1, on both sides.",
            Entries = new[]
            {
                new Entry("Cards/Special/biting_frost", "Biting Frost", "Weather on both close combat rows."),
                new Entry("Cards/Special/impenetrable_fog", "Impenetrable Fog", "Weather on both ranged rows."),
                new Entry("Cards/Special/torrential_rain", "Torrential Rain", "Weather on both siege rows."),
                new Entry("Cards/Special/clear_weather", "Clear Weather", "Removes all weather."),
                new Entry("Cards/Special/commander_horn", "Commander's Horn", "Doubles the strength of the units on a row of yours (one horn per row)."),
                new Entry("Cards/Special/decoy", "Decoy", "Takes the place of one of your units, which goes back to your hand to be played again (not a hero)."),
                new Entry("Cards/Special/scorch", "Scorch", "Destroys the strongest unit(s) on the whole battlefield, yours included (not heroes)."),
            },
        },
        new Topic
        {
            Name = "Factions",
            Intro = "Every faction has its own perk, always in effect.",
            Entries = new[]
            {
                new Entry("Cards/Back/NR", "Northern Realms", "Draw a card each time you win a round."),
                new Entry("Cards/Back/NF", "Nilfgaardian Empire", "Win every round that ends in a draw (against anyone but Nilfgaard)."),
                new Entry("Cards/Back/SC", "Scoia'tael", "Decide who goes first in the battle."),
                new Entry("Cards/Back/M", "Monsters", "One random unit stays on the board after each round."),
            },
        },
        new Topic
        {
            Name = "Controls",
            Intro = Application.isMobilePlatform ? "Everything is done with taps." : "The mouse does everything; the keyboard has shortcuts.",
            Entries = Application.isMobilePlatform ? new[]
            {
                new Entry(null, "Play a card", "Tap it in your hand (it shows large on the right), then tap where it lights up."),
                new Entry(null, "Leader", "Tap the leader's button; hold a finger on it to see whether it can still be used."),
                new Entry(null, "Pass", "Hold the Hold to pass button."),
                new Entry(null, "Look", "Hold a finger on any card (on the board, in your hand, a leader) to see it large until you lift it; tap a discard pile to look through it."),
                new Entry(null, "Pause", "The back button or the menu button: resume, settings, or give up the battle."),
            } : new[]
            {
                new Entry(null, "Play a card", "Click it in your hand, then click where it lights up (Enter does it when only one place fits). The cursor shows a red sign where it can't go."),
                new Entry(null, "Leader", "Click the leader's button, or press X twice."),
                new Entry(null, "Pass", "Hold Space, or hold the Hold to pass button."),
                new Entry(null, "Look", "Hover a card to see it large; click a discard pile to look through it."),
                new Entry(null, "Pause", "Esc: resume, settings, or give up the battle."),
            },
        },
    };

    private const float IconSize = 64;
    private readonly List<Image> markers = new List<Image>();
    private readonly List<Button> tabs = new List<Button>();
    private RectTransform content;
    private ScrollRect scroll;

    public static void Open(Action on_close)
    {
        RectTransform dimmer = UIKit.Dimmer(UIOverlay.Root, "RulesPage");
        dimmer.gameObject.AddComponent<RulesPage>().Build(dimmer);
        UIOverlay.PushModal(dimmer.gameObject, on_close);
    }

    private void Build(RectTransform dimmer)
    {
        UITheme theme = UITheme.Current;
        RectTransform panel = UIKit.Panel(dimmer, "Panel").Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 900)).FitInside(dimmer);
        TextMeshProUGUI title = UIKit.Text(panel, "How to play", UIKit.TextStyle.Heading, TextAlignmentOptions.Center);
        title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(1200, 64));
        UIKit.Ornament(panel, new Vector2(0.5f, 1), new Vector2(0, -106), 420);

        // Topics
        for (int i = 0; i < Topics.Length; i++)
        {
            int index = i;
            Button tab = UIKit.MenuItem(panel, Topics[i].Name, () => Show(index), false, new Vector2(360, 66));
            ((RectTransform)tab.transform).Place(new Vector2(0, 1), new Vector2(50, -150 - i * 76), new Vector2(360, 66), new Vector2(0, 1));
            Image marker = UIKit.NewImage(tab.transform, "Current", null, theme.gold);
            marker.rectTransform.Place(new Vector2(0, 0.5f), Vector2.zero, new Vector2(4, 44), new Vector2(0, 0.5f));
            markers.Add(marker);
            tabs.Add(tab);
        }

        // The topic, in a scroll view
        RectTransform viewport = UIKit.Rect("Viewport", panel).Place(new Vector2(0, 1), new Vector2(450, -150), new Vector2(1000, 620), new Vector2(0, 1));
        viewport.gameObject.AddComponent<RectMask2D>();
        Image hit = viewport.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        content = UIKit.Rect("Topic", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero; // exactly the viewport's width (the fitter sets the height)
        VerticalLayoutGroup column = content.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = 22;
        column.padding = new RectOffset(0, 24, 0, 24);
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;

        Button close = UIKit.Button(panel, "Close", UIKit.ButtonStyle.Secondary, () => UIOverlay.CloseModal(gameObject), new Vector2(220, 60));
        ((RectTransform)close.transform).Place(new Vector2(0.5f, 0), new Vector2(0, 56), new Vector2(220, 60));

        Show(0);
        UIKit.Focus(tabs[0]);
    }

    private void Show(int index)
    {
        for (int i = 0; i < markers.Count; i++)
            markers[i].enabled = i == index;
        foreach (Transform child in content)
            Destroy(child.gameObject);
        content.DetachChildren();

        Topic topic = Topics[index];
        TextMeshProUGUI intro = UIKit.Text(content, topic.Intro, UIKit.TextStyle.Body, TextAlignmentOptions.TopLeft);
        intro.textWrappingMode = TextWrappingModes.Normal;
        intro.color = UITheme.Current.textSecondary;
        foreach (Entry entry in topic.Entries)
            AddEntry(entry);
        scroll.verticalNormalizedPosition = 1;
    }

    // An icon (when the entry has one) beside a gold title and its text
    private void AddEntry(Entry entry)
    {
        UITheme theme = UITheme.Current;
        RectTransform row = UIKit.Rect("Entry " + entry.Title, content);
        HorizontalLayoutGroup line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        line.spacing = 22;
        line.childAlignment = TextAnchor.UpperLeft;
        line.childControlWidth = true;
        line.childControlHeight = true;
        line.childForceExpandWidth = false;
        line.childForceExpandHeight = false;

        Sprite icon = entry.Icon != null ? SpriteCache.Load(entry.Icon) : null;
        Image image = UIKit.NewImage(row, "Icon", icon, icon != null ? Color.white : Color.clear);
        image.preserveAspect = true;
        LayoutElement icon_size = image.gameObject.AddComponent<LayoutElement>();
        icon_size.preferredWidth = icon_size.minWidth = IconSize;
        icon_size.preferredHeight = icon_size.minHeight = IconSize;

        RectTransform text = UIKit.Rect("Text", row);
        VerticalLayoutGroup lines = text.gameObject.AddComponent<VerticalLayoutGroup>();
        lines.spacing = 4;
        lines.childControlWidth = true;
        lines.childControlHeight = true;
        lines.childForceExpandWidth = true;
        lines.childForceExpandHeight = false;
        text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        TextMeshProUGUI title = UIKit.Text(text, entry.Title, UIKit.TextStyle.Heading, TextAlignmentOptions.TopLeft);
        title.fontSize = 28;
        TextMeshProUGUI body = UIKit.Text(text, entry.Text, UIKit.TextStyle.Body, TextAlignmentOptions.TopLeft);
        body.fontSize = 27;
        body.color = theme.text;
        body.textWrappingMode = TextWrappingModes.Normal;
    }
}
