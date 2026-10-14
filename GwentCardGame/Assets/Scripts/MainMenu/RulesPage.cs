using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "How to play" (main menu): the rules of The Witcher 3's Gwent, one topic at a time, each on a wooden rule card in the
// spirit of the printed Gwent rule cards (matt77hias/Gwent): a title over a rule line, then each rule beside the game's
// own pieces (faction shields, the hand count, row emblems, the abilities' medallions, life gems, The Witcher 3's
// notices, cards, keys). Topics on the left; a card scrolls when it is long. The factions' card shows each faction's
// banner, perk and leaders (hover a leader, or hold a finger on it, to read its ability).
public class RulesPage : MonoBehaviour
{
    // A rule: its pieces (see Piece), a term and its explanation
    private struct Entry
    {
        public string[] Pieces;
        public string Title;
        public string Text;
    }

    private struct Topic
    {
        public string Name;     // the tab
        public string Heading;  // the card's title
        public string Intro;
        public Entry[] Entries;
        public bool Factions;
    }

    private struct Faction
    {
        public string Code;
        public string Name;
        public string Perk;
        public Color Banner;
        public int[] Leaders;
    }

    private static Entry E(string title, string text, params string[] pieces)
    {
        return new Entry { Title = title, Text = text, Pieces = pieces };
    }

    // Built when the page opens, so the controls follow the current input (touch or not)
    private static Topic[] Topics()
    {
        bool touch = GameInput.IsTouch;
        return new[]
        {
            new Topic
            {
                Name = "The battle",
                Heading = "Gwent",
                Intro = "Gwent is a card game for two players. Win two rounds out of three: a round goes to the side whose cards on the board add up to more.",
                Entries = new[]
                {
                    E("Factions", "Each deck belongs to one of four factions, with its own perk and cards; neutral cards fit any deck.", "shield:NR", "shield:SC", "shield:M", "shield:NF"),
                    E("Hand cards", "You draw 10 cards and may redraw up to 2 of them before the first round. Nobody draws between rounds, so every card you play is one fewer for the rounds ahead; only spies, a few leaders and the Northern Realms perk bring more.", "cards", "num:10"),
                    E("Your turn", "Do one thing: play a card, use your leader's ability, or pass. Playing a card ends your turn.", "notice:me_turn"),
                    E("Combat rows", "Units go on the close combat, ranged or siege row, as shown on the card.", "emblem:0", "emblem:1", "emblem:2"),
                    E("Special cards", "Weather, Commander's Horn, Decoy and Scorch, and your leader's ability, can turn a round around.", "medal:biting_frost", "medal:decoy", "medal:scorch"),
                    E("Passing", "Passing is final for the round: you play nothing more in it, while your opponent may go on playing. Once both sides have passed, the higher total wins the round. A side with no card and no leader ability it could use passes on its own, and a side that starts a round with no card sits it out.", "notice:round_passed"),
                    E("Life", "Each side has two gems and loses one with each round it loses; a drawn round costs both (unless Nilfgaard plays). Lose both and the battle is lost. The winner of a round starts the next one, and the board is cleared in between.", "gem", "gem"),
                    E("Playing well", "Winning a round by a mile wastes cards. Letting a round go and keeping a strong hand for the other two is often the better play.", "notice:win_round"),
                },
            },
            new Topic
            {
                Name = "The board",
                Heading = "The board",
                Intro = "Each side has three rows; yours are the lower half of the table.",
                Entries = new[]
                {
                    E("Rows", "Close combat, ranged and siege, from the middle of the table out. A unit goes on the row shown on its card.", "emblem:0", "emblem:1", "emblem:2"),
                    E("Strength", "A unit's strength is the number in its corner. Each row's total is on its left, each side's total by its portrait, the side ahead in a laurel.", "score:24", "total:61"),
                    E("Agile", "An agile unit can go on the close combat or the ranged row.", "medal:agile"),
                    E("Horn slot", "The slot at the left of each row takes a Commander's Horn, one per row.", "medal:commander_horn"),
                    E("Weather board", "Weather cards and Scorch are played on the weather board, at the left of the table. Weather affects both sides.", "medal:biting_frost", "medal:impenetrable_fog", "medal:torrential_rain"),
                    E("Whose turn", "The panel of the side whose turn it is is edged with glowing gold lines; a side that has passed is greyed out.", "notice:op_turn"),
                    E("Discard piles", "Cards that leave the board go to their owner's discard pile, next to the deck. " + (touch ? "Tap" : "Click") + " a pile to look through it.", "back:NR", "back:M"),
                },
            },
            new Topic
            {
                Name = "Cards",
                Heading = "Cards",
                Intro = "A deck has one leader, at least 22 unit cards and at most 10 special cards, and holds each card at most as many times as it exists.",
                Entries = new[]
                {
                    E("Heroes", "The gold-framed units. Nothing changes them: no weather, horn, morale boost, scorch, decoy or medic. A hero can be in a deck only once.", "card:67", "card:14"),
                    E("Units", "Everything else on the rows. Most exist once; a few, such as the Blue Stripes Commando, several times.", "card:7", "card:4"),
                    E("Special cards", "Weather, Commander's Horn, Decoy and Scorch: they shape the board instead of adding strength.", "card:202", "card:203"),
                    E("Leader", "One ability you can use once per battle; its crown is lit while you still can. A few leaders' abilities are always in effect instead.", "card:51", "crown"),
                },
            },
            new Topic
            {
                Name = "Abilities",
                Heading = "Card abilities",
                Intro = "Shown by the icons on a unit card.",
                Entries = new[]
                {
                    E("Spy", "Goes on your opponent's side (adding to their total) and you draw 2 cards from your deck.", "medal:spy"),
                    E("Medic", "Brings a unit back from your discard pile and plays it at once (no heroes or special cards). A medic brought back heals again.", "medal:medic"),
                    E("Muster", "Plays every card of the same family from your deck and your hand at once.", "medal:muster"),
                    E("Tight bond", "Copies of this card on the same row multiply each other's strength: two double it, three triple it.", "medal:tight_bond"),
                    E("Morale boost", "Adds 1 to every other unit on its row.", "medal:morale_boost"),
                    E("Commander's horn", "Doubles the other units on its row, unless the row already has a Commander's Horn.", "medal:commander_horn"),
                    E("Scorch", "Destroys your opponent's strongest unit(s) on that row, if the row totals 10 or more.", "medal:scorch"),
                    E("Agile", "Close combat or ranged, your choice.", "medal:agile"),
                    E("Avenger", "The Cow: when it goes to the discard pile, the Bovine Defense Force joins your close combat row for the next round.", "card:16"),
                },
            },
            new Topic
            {
                Name = "Special cards",
                Heading = "Special cards",
                Intro = "Weather lowers every non-hero unit of its rows to 1, on both sides.",
                Entries = new[]
                {
                    E("Biting Frost", "Weather on both close combat rows.", "medal:biting_frost"),
                    E("Impenetrable Fog", "Weather on both ranged rows.", "medal:impenetrable_fog"),
                    E("Torrential Rain", "Weather on both siege rows.", "medal:torrential_rain"),
                    E("Clear Weather", "Removes all weather.", "medal:clear_weather"),
                    E("Commander's Horn", "Doubles the strength of the units on a row of yours, one horn per row.", "medal:commander_horn"),
                    E("Decoy", "Takes the place of one of your units, which goes back to your hand to be played again (not a hero).", "medal:decoy"),
                    E("Scorch", "Destroys the strongest unit(s) on the whole battlefield, yours included (not heroes).", "medal:scorch"),
                },
            },
            new Topic
            {
                Name = "Factions",
                Heading = "Factions",
                Intro = "Every faction has its own perk, always in effect, and five leaders to choose from. " + (touch ? "Hold a finger on" : "Rest the pointer on") + " a leader to read its ability.",
                Factions = true,
            },
            new Topic
            {
                Name = "Controls",
                Heading = "Controls",
                Intro = touch ? "Everything is done with taps; hold a finger on a card to look at it." : "The mouse does everything, and so do the keys.",
                Entries = touch ? new[]
                {
                    E("Play a card", "Tap it in your hand (it shows large on the right), then tap where it lights up.", "card:7"),
                    E("Leader", "Tap the leader: it shows on the right and lights where it acts; tap it again, or the lit place, to use it. Hold a finger on its crown to see whether it can still be used.", "crown"),
                    E("Pass", "Hold the Hold to pass button.", "key:Hold to pass"),
                    E("Look", "Hold a finger on any card (on the board, in your hand, a leader) to see it large until you lift it; tap a discard pile to look through it.", "card:14"),
                    E("Pause", "The back button or the menu button: resume, settings, or give up the battle.", "key:Back"),
                } : new[]
                {
                    E("Play a card", "Click it in your hand, then click where it lights up; the cursor shows a red sign where it can't go. With the keys: Left/Right go through your hand, Enter takes the card, Up/Down choose where it goes, Enter plays it, Esc puts it back.", "key:<", "key:>", "key:Enter"),
                    E("Leader", "Click the leader (or its crown): it shows on the right and lights where it acts; click it again, or the lit place, to use it. Or X, then X again or Enter.", "key:X"),
                    E("Pass", "Hold Space, or hold the Hold to pass button.", "key:Space"),
                    E("Look", "Hover a card to see it large; click a discard pile to look through it.", "card:14"),
                    E("Pause", "Esc: resume, settings, or give up the battle.", "key:Esc"),
                },
            },
        };
    }

    private static readonly Faction[] FactionList =
    {
        new Faction { Code = "NR", Name = "Northern Realms", Perk = "Draws a card from the deck each time it wins a round.", Banner = new Color32(0x2B, 0x4F, 0xA8, 0xFF), Leaders = new[] { 51, 52, 53, 54, 55 } },
        new Faction { Code = "NF", Name = "Nilfgaardian Empire", Perk = "Wins every round that ends in a draw (against anyone but Nilfgaard).", Banner = new Color32(0x2A, 0x26, 0x1E, 0xFF), Leaders = new[] { 33, 34, 35, 36, 37 } },
        new Faction { Code = "SC", Name = "Scoia'tael", Perk = "Decides who goes first in the battle.", Banner = new Color32(0x2A, 0x6B, 0x34, 0xFF), Leaders = new[] { 57, 58, 59, 60, 61 } },
        new Faction { Code = "M", Name = "Monsters", Perk = "One random unit stays on the board after each round.", Banner = new Color32(0x86, 0x1E, 0x1E, 0xFF), Leaders = new[] { 40, 41, 42, 43, 44 } },
    };

    // The leaders as The Witcher 3's cards name and describe them
    private static readonly Dictionary<int, string[]> LeaderText = new Dictionary<int, string[]>
    {
        { 51, new[] { "Foltest: King of Temeria", "Pick an Impenetrable Fog card from your deck and play it instantly." } },
        { 52, new[] { "Foltest: Lord Commander of the North", "Clear any weather effects in play." } },
        { 53, new[] { "Foltest: Son of Medell", "Destroy your enemy's strongest Ranged Combat unit(s) if their combined Ranged Combat strength is 10 or more." } },
        { 54, new[] { "Foltest: The Siegemaster", "Double the strength of all your Siege units (unless a Commander's Horn is also present on that row)." } },
        { 55, new[] { "Foltest: The Steel-Forged", "Destroy your enemy's strongest Siege unit(s) if their combined Siege strength is 10 or more." } },
        { 33, new[] { "Emhyr var Emreis: Emperor of Nilfgaard", "Look at 3 random cards from your opponent's hand." } },
        { 34, new[] { "Emhyr var Emreis: His Imperial Majesty", "Pick a Torrential Rain card from your deck and play it instantly." } },
        { 35, new[] { "Emhyr var Emreis: Invader of the North", "Abilities that restore a unit restore a randomly chosen one. Affects both players." } },
        { 36, new[] { "Emhyr var Emreis: The Relentless", "Draw a card from your opponent's discard pile." } },
        { 37, new[] { "Emhyr var Emreis: The White Flame", "Cancel your opponent's Leader Ability." } },
        { 57, new[] { "Francesca Findabair: Daisy of the Valley", "Draw an extra card at the beginning of the battle." } },
        { 58, new[] { "Francesca Findabair: Hope of the Aen Seidhe", "Move agile units to whichever valid row maximizes their strength (units already in their best row stay)." } },
        { 59, new[] { "Francesca Findabair: Pureblood Elf", "Pick a Biting Frost card from your deck and play it instantly." } },
        { 60, new[] { "Francesca Findabair: Queen of Dol Blathanna", "Destroy your enemy's strongest Close Combat unit(s) if their combined Close Combat strength is 10 or more." } },
        { 61, new[] { "Francesca Findabair: The Beautiful", "Double the strength of all your Ranged Combat units (unless a Commander's Horn is also present on that row)." } },
        { 40, new[] { "Eredin Bréacc Glas: The Treacherous", "Doubles the strength of all spy cards (affects both players)." } },
        { 41, new[] { "Eredin: Bringer of Death", "Restore a card from your discard pile to your hand." } },
        { 42, new[] { "Eredin: Commander of the Red Riders", "Double the strength of all your Close Combat units (unless a Commander's Horn is also present on that row)." } },
        { 43, new[] { "Eredin: Destroyer of Worlds", "Discard 2 cards and draw 1 card of your choice from your deck." } },
        { 44, new[] { "Eredin: King of the Wild Hunt", "Pick any weather card from your deck and play it instantly." } },
    };

    private const float ClusterWidth = 240;
    private const float ClusterHeight = 104;
    private static readonly Color Parchment = new Color(0.97f, 0.94f, 0.87f);
    private static readonly Color Prose = new Color(0.87f, 0.83f, 0.76f);
    private static readonly Color Bronze = new Color(0.62f, 0.45f, 0.24f, 0.95f);

    private Topic[] topics;
    private readonly Dictionary<LayoutElement, float> clusters = new Dictionary<LayoutElement, float>(); // the topic's, with their pieces' width
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
        topics = Topics();
        RectTransform panel = UIKit.Panel(dimmer, "Panel").Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1640, 940)).FitInside(dimmer);
        TextMeshProUGUI title = UIKit.Text(panel, "How to play", UIKit.TextStyle.Heading, TextAlignmentOptions.Center);
        title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(1200, 64));
        UIKit.Ornament(panel, new Vector2(0.5f, 1), new Vector2(0, -100), 420);

        // Topics
        for (int i = 0; i < topics.Length; i++)
        {
            int index = i;
            Button tab = UIKit.MenuItem(panel, topics[i].Name, () => Show(index), false, new Vector2(340, 64));
            ((RectTransform)tab.transform).Place(new Vector2(0, 1), new Vector2(40, -146 - i * 74), new Vector2(340, 64), new Vector2(0, 1));
            Image marker = UIKit.NewImage(tab.transform, "Current", null, theme.gold);
            marker.rectTransform.Place(new Vector2(0, 0.5f), Vector2.zero, new Vector2(4, 44), new Vector2(0, 0.5f));
            markers.Add(marker);
            tabs.Add(tab);
        }

        // The rule card: dark wood in a bronze frame
        RectTransform card = UIKit.Rect("RuleCard", panel).Place(new Vector2(0, 1), new Vector2(408, -138), new Vector2(1196, 700), new Vector2(0, 1));
        Image wood = UIKit.NewImage(card, "Wood", theme.table, new Color(0.8f, 0.74f, 0.68f));
        wood.rectTransform.Stretch();
        UIKit.NewImage(card, "Shade", null, new Color(0, 0, 0, 0.22f)).rectTransform.Stretch();
        UIKit.Frame(card, "Edge", new Color(0.06f, 0.04f, 0.02f, 0.9f), 2, 2);
        UIKit.Frame(card, "Bronze", Bronze, 2, 0);

        // Its rules, in a scroll view
        RectTransform viewport = UIKit.Rect("Viewport", card).Stretch(6, 6, 6, 6);
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
        column.spacing = 16;
        column.padding = new RectOffset(44, 52, 26, 34);
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

        Button close = UIKit.Button(panel, "Close", UIKit.ButtonStyle.Secondary, () => UIOverlay.CloseModal(gameObject), new Vector2(220, 58));
        ((RectTransform)close.transform).Place(new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(220, 58));

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

        Topic topic = topics[index];
        AddHeading(topic.Heading);
        if (!string.IsNullOrEmpty(topic.Intro))
        {
            TextMeshProUGUI intro = UIKit.Text(content, topic.Intro, UIKit.TextStyle.Body, TextAlignmentOptions.TopLeft);
            intro.fontSize = 28;
            intro.color = Parchment;
            intro.textWrappingMode = TextWrappingModes.Normal;
            Spacer(6);
        }
        int order = 0;
        clusters.Clear();
        if (topic.Factions)
            foreach (Faction faction in FactionList)
                FadeIn(AddFaction(faction), order++);
        else
            foreach (Entry entry in topic.Entries)
                FadeIn(AddEntry(entry), order++);
        // One column for the topic's pieces, as wide as its widest group: the texts line up, close to their pieces
        float widest = 0;
        foreach (float width in clusters.Values)
            widest = Mathf.Max(widest, width);
        foreach (LayoutElement cluster in clusters.Keys)
            cluster.minWidth = cluster.preferredWidth = Mathf.Clamp(widest + 16, 112, ClusterWidth);
        scroll.verticalNormalizedPosition = 1;
    }

    // The card's title, light over a fine rule, as on The Witcher 3's rule cards
    private void AddHeading(string text)
    {
        UITheme theme = UITheme.Current;
        TextMeshProUGUI heading = UIKit.Text(content, text, UIKit.TextStyle.Title, TextAlignmentOptions.Center);
        heading.fontSize = 54;
        heading.color = Parchment;
        RectTransform rule_holder = UIKit.Rect("Rule", content);
        rule_holder.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
        Image rule = UIKit.NewImage(rule_holder, "Line", theme.rule, UITheme.WithAlpha(Parchment, 0.6f));
        rule.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 16));
    }

    private void Spacer(float height)
    {
        UIKit.Rect("Space", content).gameObject.AddComponent<LayoutElement>().preferredHeight = height;
    }

    // The rule's pieces on the left, centred in their column; the term and its explanation on the right
    private RectTransform AddEntry(Entry entry)
    {
        RectTransform row = Row("Entry " + entry.Title);
        Cluster(row, entry.Pieces);
        TextMeshProUGUI text = UIKit.Text(row, "<color=#" + ColorUtility.ToHtmlStringRGB(UITheme.Current.goldBright) + "><b>" + entry.Title + ":</b></color> " + entry.Text,
            UIKit.TextStyle.Body, TextAlignmentOptions.MidlineLeft);
        text.fontSize = 27;
        text.color = Prose;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        return row;
    }

    private RectTransform Row(string name)
    {
        RectTransform row = UIKit.Rect(name, content);
        HorizontalLayoutGroup line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        line.spacing = 28;
        line.childAlignment = TextAnchor.MiddleLeft;
        line.childControlWidth = true;
        line.childControlHeight = true;
        line.childForceExpandWidth = false;
        line.childForceExpandHeight = false;
        return row;
    }

    private RectTransform Cluster(Transform row, string[] pieces)
    {
        RectTransform cluster = UIKit.Rect("Pieces", row);
        LayoutElement size = cluster.gameObject.AddComponent<LayoutElement>();
        size.minWidth = size.preferredWidth = ClusterWidth;
        size.minHeight = size.preferredHeight = ClusterHeight;

        // Side by side, overlapping a little where they do on the reference cards, the whole group centred
        List<RectTransform> made = new List<RectTransform>();
        List<float> advance = new List<float>();
        float total = 0;
        foreach (string spec in pieces)
        {
            float overlap;
            RectTransform piece = Piece(cluster, spec, out overlap);
            if (piece == null)
                continue;
            if (made.Count > 0)
                total -= overlap;
            made.Add(piece);
            advance.Add(overlap);
            total += piece.sizeDelta.x;
        }
        float x = -total / 2;
        for (int i = 0; i < made.Count; i++)
        {
            if (i > 0)
                x -= advance[i];
            made[i].anchoredPosition = new Vector2(x + made[i].sizeDelta.x / 2, made[i].anchoredPosition.y);
            x += made[i].sizeDelta.x;
        }
        clusters[size] = total;
        // Drawn left to right, the first on top (as the reference's fanned shields and medallions)
        for (int i = made.Count - 1; i >= 0; i--)
            made[i].SetAsLastSibling();
        return cluster;
    }

    // One piece, from its description; overlap: how far it slides under the piece before it
    private static RectTransform Piece(RectTransform parent, string spec, out float overlap)
    {
        UITheme theme = UITheme.Current;
        string kind = spec, arg = "";
        int colon = spec.IndexOf(':');
        if (colon > 0)
        {
            kind = spec.Substring(0, colon);
            arg = spec.Substring(colon + 1);
        }
        overlap = 0;
        switch (kind)
        {
            case "shield":
                {
                    // Fanned, every other one a little higher, as on the reference card
                    overlap = 24;
                    RectTransform shield = Picture(parent, SpriteCache.Load("Stats/faction/" + arg), new Vector2(76, 85));
                    if (shield != null)
                        shield.anchoredPosition = new Vector2(0, parent.childCount % 2 == 0 ? -5 : 5);
                    return shield;
                }
            case "cards":
                return Picture(parent, SpriteCache.Load("Stats/cards"), new Vector2(46, 63));
            case "num":
                {
                    overlap = -10;
                    TextMeshProUGUI number = UIKit.Text(parent, arg, UIKit.TextStyle.Title, TextAlignmentOptions.Center);
                    number.fontSize = 66;
                    number.color = theme.goldBright;
                    number.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96, 80));
                    return number.rectTransform;
                }
            case "emblem":
                {
                    // The row's engraved disc, ringed in bronze so it stands out from the card's wood
                    overlap = 2;
                    RectTransform emblem = Picture(parent, theme.rowEmblems[int.Parse(arg)], new Vector2(74, 74));
                    Image ring = UIKit.NewImage(emblem, "Ring", theme.ring, Bronze);
                    ring.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(66, 66));
                    return emblem;
                }
            case "medal":
                overlap = 22;
                return Picture(parent, SpriteCache.Load("UI/Rules/" + arg), new Vector2(98, 98));
            case "gem":
                overlap = -8;
                return Picture(parent, theme.gem, new Vector2(58, 59));
            case "notice":
                return Picture(parent, SpriteCache.Load("W3/Notifications/" + arg), new Vector2(126, 90));
            case "card":
                overlap = -8;
                return CardPicture(parent, SpriteCache.Load("Cards/List/135x87/" + arg), new Vector2(64, 99));
            case "back":
                overlap = 38;
                return CardPicture(parent, SpriteCache.Load("Cards/Back/" + arg), new Vector2(64, 99));
            case "crown":
                return Picture(parent, theme.leaderReady, new Vector2(68, 68));
            case "score":
                {
                    overlap = -10;
                    RectTransform disc = Picture(parent, theme.scoreDisc, new Vector2(60, 61));
                    Number(disc, arg, 30, new Color(0.24f, 0.15f, 0.07f));
                    return disc;
                }
            case "total":
                {
                    RectTransform laurel = Picture(parent, SpriteCache.Load("Various/pass"), new Vector2(104, 90));
                    RectTransform disc = Picture(laurel, theme.scoreTotal, new Vector2(80, 80));
                    disc.anchoredPosition = new Vector2(0, 2);
                    Number(disc, arg, 32, new Color(0.11f, 0.08f, 0.05f));
                    return laurel;
                }
            case "key":
                overlap = -10;
                return Key(parent, arg);
        }
        return null;
    }

    private static RectTransform Picture(Transform parent, Sprite sprite, Vector2 size)
    {
        if (sprite == null)
            return null;
        Image image = UIKit.NewImage(parent, sprite.name, sprite, Color.white);
        image.preserveAspect = true;
        image.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, size);
        return image.rectTransform;
    }

    // A card, with the soft shadow the board gives its cards
    private static RectTransform CardPicture(Transform parent, Sprite sprite, Vector2 size)
    {
        RectTransform card = Picture(parent, sprite, size);
        if (card == null)
            return null;
        Image shadow = UIKit.NewImage(card, "Shadow", UITheme.Current.softShadow, new Color(0, 0, 0, 0.6f));
        shadow.rectTransform.Stretch(-9, -9, -6, -12);
        shadow.transform.SetAsFirstSibling();
        // The shadow is drawn before the card: the card's own image is its parent, so give the shadow a canvas order of
        // its own by moving the art into a child above it
        Image art = card.GetComponent<Image>();
        Image front = UIKit.NewImage(card, "Art", art.sprite, Color.white);
        front.preserveAspect = true;
        front.rectTransform.Stretch();
        art.enabled = false;
        return card;
    }

    private static void Number(RectTransform disc, string text, float size, Color color)
    {
        TextMeshProUGUI number = UIKit.Text(disc, text, UIKit.TextStyle.Title, TextAlignmentOptions.Center);
        number.fontSize = size;
        number.color = color;
        number.rectTransform.Stretch();
    }

    // A key of the keyboard: a dark key with a gold outline; < > ^ v show as chevrons
    private static RectTransform Key(Transform parent, string label)
    {
        UITheme theme = UITheme.Current;
        Sprite chevron = label == "<" ? theme.chevronLeft : label == ">" ? theme.chevronRight : label == "v" || label == "^" ? theme.chevronDown : null;
        float width = chevron != null ? 58 : Mathf.Max(58, 30 + label.Length * 15);
        Image key = UIKit.NewImage(parent, "Key " + label, theme.panelSprite, new Color(0.1f, 0.08f, 0.06f, 0.95f));
        key.type = Image.Type.Sliced;
        key.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 54));
        Image outline = UIKit.NewImage(key.transform, "Outline", theme.panelOutline, theme.gold);
        outline.type = Image.Type.Sliced;
        outline.rectTransform.Stretch();
        // A darker lower edge: the key stands up from the table
        Image edge = UIKit.NewImage(key.transform, "Edge", theme.gradientV, new Color(0, 0, 0, 0.35f));
        edge.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 2), new Vector2(width - 10, 14), new Vector2(0.5f, 0));
        edge.rectTransform.localEulerAngles = new Vector3(0, 0, 180);
        if (chevron != null)
        {
            Image glyph = UIKit.NewImage(key.transform, "Glyph", chevron, theme.goldBright);
            glyph.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28, 28));
            if (label == "^")
                glyph.rectTransform.localEulerAngles = new Vector3(0, 0, 180);
        }
        else
        {
            TextMeshProUGUI text = UIKit.Text(key.transform, label, UIKit.TextStyle.Body, TextAlignmentOptions.Center);
            text.font = theme.bodyMediumFont != null ? theme.bodyMediumFont : text.font;
            text.fontSize = 24;
            text.color = theme.goldBright;
            text.rectTransform.Stretch();
        }
        return key.rectTransform;
    }

    //----------------------------------------Factions----------------------------------------//
    // A faction: its shield on a hanging banner in its colours, its name, its perk and its five leaders
    private RectTransform AddFaction(Faction faction)
    {
        UITheme theme = UITheme.Current;
        RectTransform row = Row("Faction " + faction.Name);

        RectTransform banner = UIKit.Rect("Banner", row);
        LayoutElement size = banner.gameObject.AddComponent<LayoutElement>();
        size.minWidth = size.preferredWidth = 150;
        size.minHeight = size.preferredHeight = 196;
        Image ribbon = UIKit.NewImage(banner, "Ribbon", SpriteCache.Load("UI/Rules/Banner"), faction.Banner);
        ribbon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(76, 180));
        RectTransform shield = Picture(banner, SpriteCache.Load("Stats/faction/" + faction.Code), new Vector2(100, 112));
        shield.anchoredPosition = new Vector2(0, 30);

        RectTransform column = UIKit.Rect("About", row);
        VerticalLayoutGroup lines = column.gameObject.AddComponent<VerticalLayoutGroup>();
        lines.spacing = 8;
        lines.childAlignment = TextAnchor.MiddleLeft;
        lines.childControlWidth = true;
        lines.childControlHeight = true;
        lines.childForceExpandWidth = true;
        lines.childForceExpandHeight = false;
        column.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        TextMeshProUGUI name = UIKit.Text(column, faction.Name, UIKit.TextStyle.Title, TextAlignmentOptions.MidlineLeft);
        name.fontSize = 36;
        name.color = Parchment;
        TextMeshProUGUI perk = UIKit.Text(column, "<color=#" + ColorUtility.ToHtmlStringRGB(theme.goldBright) + "><b>Perk:</b></color> " + faction.Perk, UIKit.TextStyle.Body, TextAlignmentOptions.MidlineLeft);
        perk.fontSize = 27;
        perk.color = Prose;
        perk.textWrappingMode = TextWrappingModes.Normal;

        RectTransform leaders = UIKit.Rect("Leaders", column);
        HorizontalLayoutGroup portraits = leaders.gameObject.AddComponent<HorizontalLayoutGroup>();
        portraits.spacing = 12;
        portraits.childAlignment = TextAnchor.MiddleLeft;
        portraits.childControlWidth = false;
        portraits.childControlHeight = false;
        portraits.childForceExpandWidth = false;
        portraits.childForceExpandHeight = false;
        leaders.gameObject.AddComponent<LayoutElement>().preferredHeight = 72;
        foreach (int leader in faction.Leaders)
            LeaderPortrait(leaders, leader);
        return row;
    }

    // A round portrait of the leader in a gold ring; its ability shows over it on hover (a held finger with touch)
    private static void LeaderPortrait(Transform parent, int leader_id)
    {
        UITheme theme = UITheme.Current;
        RectTransform holder = UIKit.Rect("Leader " + leader_id, parent);
        holder.sizeDelta = new Vector2(68, 68);
        Image mask = UIKit.NewImage(holder, "Portrait", theme.circle, Color.white, true);
        mask.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        Sprite art = SpriteCache.Load("Cards/List/591x380/" + leader_id);
        if (art != null)
        {
            Image portrait = UIKit.NewImage(mask.transform, "Art", art, Color.white);
            portrait.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104, 104 * 591f / 380f), new Vector2(0.5f, 0.72f));
        }
        Image ring = UIKit.NewImage(holder, "Ring", theme.ring, theme.gold);
        ring.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(68, 68));
        string[] text;
        if (LeaderText.TryGetValue(leader_id, out text))
            mask.gameObject.AddComponent<UITooltip>().text = () => "<b>" + text[0] + "</b>\n" + text[1];
    }

    //----------------------------------------A little life----------------------------------------//
    // The rules come in one after another when a topic opens
    private static void FadeIn(RectTransform row, int order)
    {
        RulesFadeIn fade = row.gameObject.AddComponent<RulesFadeIn>();
        fade.delay = 0.04f + order * 0.05f;
    }
}

// Fades a rule in after a delay (unscaled time; the layout keeps its place all along)
public class RulesFadeIn : MonoBehaviour
{
    private const float Duration = 0.28f;
    public float delay;
    private CanvasGroup group;
    private float age;

    private void Awake()
    {
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01((age - delay) / Duration);
        group.alpha = t * t * (3 - 2 * t);
        if (t >= 1)
            enabled = false;
    }
}
