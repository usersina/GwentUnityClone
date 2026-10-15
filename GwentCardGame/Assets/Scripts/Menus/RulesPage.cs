using System;
using System.Collections.Generic;
using Gwent.Battle;
using Gwent.Core;
using Gwent.Rules;
using Gwent.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gwent.Menus
{
    // "How to play" (main menu): the rules of The Witcher 3's Gwent, one topic at a time. Topics on the left, the topic's
    // rules on the right (they scroll when long), each beside the game's own pieces as on the printed Gwent rule cards
    // (matt77hias/Gwent): faction shields, the hand count, row emblems, the abilities' medallions, life gems, The Witcher 3's
    // notices, cards, keys. The factions show their perk and leaders (hover a leader, or hold a finger on it, to read its
    // ability). A UI Toolkit dialog: Assets/UI/Toolkit/RulesPage.uxml, the topics and their rules made here.
    public static class RulesPage
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
            public string Name;
            public string Intro;
            public Entry[] Entries;
            public bool Factions;
        }

        private struct FactionTopic
        {
            public Faction Faction;
            public string Perk;
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
                    Intro = "Gwent is a card game for two players. Win two rounds out of three: a round goes to the side whose cards on the board add up to more.",
                    Entries = new[]
                    {
                        E("Factions", "Each deck belongs to one of five factions, with its own perk and cards; neutral cards fit any deck.", "shield:NR", "shield:SC", "shield:M", "shield:NF", "shield:SK"),
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
                    Intro = "Each side has three rows; yours are the lower half of the table.",
                    Entries = new[]
                    {
                        E("Rows", "Close combat, ranged and siege, from the middle of the table out. A unit goes on the row shown on its card.", "emblem:0", "emblem:1", "emblem:2"),
                        E("Strength", "A unit's strength is the number in its corner. Each row's total is on its left, each side's total by its portrait, the side ahead in a laurel.", "score:24", "total:61"),
                        E("Agile", "An agile unit can go on the close combat or the ranged row.", "medal:agile"),
                        E("Horn slot", "The slot at the left of each row takes one card: a Commander's Horn, or Skellige's Mardroeme.", "medal:commander_horn", "medal:mardroeme"),
                        E("Weather board", "Weather cards and Scorch are played on the weather board, at the left of the table. Weather affects both sides.", "medal:biting_frost", "medal:impenetrable_fog", "medal:torrential_rain"),
                        E("Whose turn", "The panel of the side whose turn it is is edged with glowing gold lines; a side that has passed is greyed out.", "notice:op_turn"),
                        E("Discard piles", "Cards that leave the board go to their owner's discard pile, next to the deck. " + (touch ? "Tap" : "Click") + " a pile to look through it.", "back:NR", "back:M"),
                    },
                },
                new Topic
                {
                    Name = "Cards",
                    Intro = "A deck has one leader, at least 22 unit cards and at most 10 special cards, and holds each card at most as many times as it exists.",
                    Entries = new[]
                    {
                        E("Heroes", "The gold-framed units. Nothing changes them: no weather, horn, morale boost, scorch, decoy or medic. A hero can be in a deck only once.", "card:67", "card:14"),
                        E("Units", "Everything else on the rows. Most exist once; a few, such as the Blue Stripes Commando, several times.", "card:7", "card:4"),
                        E("Special cards", "Weather, Commander's Horn, Decoy and Scorch: they shape the board instead of adding strength.", "card:202", "card:203"),
                        E("Leader", "One ability you can use once per battle: lift the leader card, and its crown glows; the crown uses it. The crown turns grey once used. A few leaders' abilities are always in effect instead.", "card:51", "crown"),
                    },
                },
                new Topic
                {
                    Name = "Abilities",
                    Intro = "Shown by the icons on a unit card.",
                    Entries = new[]
                    {
                        E("Spy", "Goes on your opponent's side (adding to their total) and you draw 2 cards from your deck.", "medal:spy"),
                        E("Medic", "Brings a unit back from your discard pile and plays it at once (no heroes or special cards). A medic brought back heals again.", "medal:medic"),
                        E("Muster", "Plays every card of the same family from your deck and your hand at once.", "medal:muster"),
                        E("Tight bond", "Copies of this card on the same row multiply each other's strength: two double it, three triple it.", "medal:tight_bond"),
                        E("Morale boost", "Adds 1 to every other unit on its row.", "medal:morale_boost"),
                        E("Commander's horn", "Doubles the other units on its row, unless the row already has a Commander's Horn.", "medal:commander_horn"),
                        E("Scorch", "Destroys your opponent's strongest unit(s) on that row, if the row totals 10 or more. The Clan Dimun Pirate's scorch is the Scorch card's: the strongest unit(s) on the whole battlefield as he lands (never himself).", "medal:scorch"),
                        E("Agile", "Close combat or ranged, your choice.", "medal:agile"),
                        E("Berserker", "Skellige's Berserkers turn into bears on a row with a Mardroeme (the special card in its horn slot, or Ermion on it), whether it came first or they did: the Berserker (4) into the Transformed Vildkaarl (14, morale boost), the Young Berserker (2) into the Transformed Young Vildkaarl (8, tight bond). When a bear leaves the board (scorched, or at the end of the round), it leaves the game and its Berserker goes to your discard pile.", "medal:berserker", "medal:mardroeme"),
                        E("Avenger", "The Cow and Skellige's Kambi: killed by a scorch, they are avenged at once, the Bovine Defense Force (8) or Hemdall (11, a hero) taking the field in your close combat row. Discarded any other way (cleared at the end of a round, or from your hand by Destroyer of Worlds), the avenger joins your close combat row at the start of the next round; if the battle ends first, it never comes. A decoy taking them back to your hand calls nothing. Avengers leave the game at the end of their round.", "card:16", "card:164", "medal:avenger"),
                    },
                },
                new Topic
                {
                    Name = "Special cards",
                    Intro = "Weather lowers every non-hero unit of its rows to 1, on both sides.",
                    Entries = new[]
                    {
                        E("Biting Frost", "Weather on both close combat rows.", "medal:biting_frost"),
                        E("Impenetrable Fog", "Weather on both ranged rows.", "medal:impenetrable_fog"),
                        E("Torrential Rain", "Weather on both siege rows.", "medal:torrential_rain"),
                        E("Skellige Storm", "Weather on both ranged and siege rows.", "medal:skellige_storm"),
                        E("Clear Weather", "Removes all weather.", "medal:clear_weather"),
                        E("Commander's Horn", "Doubles the strength of the units on a row of yours, one horn per row.", "medal:commander_horn"),
                        E("Decoy", "Takes the place of one of your units, which goes back to your hand to be played again (not a hero).", "medal:decoy"),
                        E("Scorch", "Destroys the strongest unit(s) on the whole battlefield, yours included (not heroes).", "medal:scorch"),
                        E("Mardroeme", "Skellige's own: in a row's horn slot, it turns that row's Berserkers into bears.", "medal:mardroeme"),
                    },
                },
                new Topic
                {
                    Name = "Factions",
                    Intro = "Every faction has its own perk, always in effect, and its leaders to choose from (Skellige's two came with Blood and Wine). " + (touch ? "Hold a finger on" : "Rest the pointer on") + " a leader to read its ability.",
                    Factions = true,
                },
                new Topic
                {
                    Name = "Controls",
                    Intro = touch ? "Everything is done with taps; hold a finger on a card to look at it." : "The mouse does everything, and so do the keys.",
                    Entries = touch ? new[]
                    {
                        E("Play a card", "Tap it in your hand (it shows large on the right), then tap where it lights up.", "card:7"),
                        E("Leader", "Tap the leader: it leaves its place for the right, shows where it acts, and its crown glows; tap the crown to use it, or the empty place to put it back. Hold a finger on the crown to see whether it can still be used.", "crown"),
                        E("Pass", "Hold the Hold to pass button.", "key:Hold to pass"),
                        E("Look", "Hold a finger on any card (on the board, in your hand, a leader) to see it large until you lift it; tap a discard pile to look through it.", "card:14"),
                        E("Pause", "The menu button (or the Android app's back button): resume or settings.", "key:Back"),
                        E("Give up", "Lay your hand on your deck: in your turn, hold a finger on it, then confirm. It counts as a loss.", "back:NR"),
                    } : new[]
                    {
                        E("Play a card", "Click it in your hand, then click where it lights up; the cursor shows a red sign where it can't go. With the keys: Left/Right go through your hand, Enter takes the card, Up/Down choose where it goes, Enter plays it, Esc puts it back.", "key:<", "key:>", "key:Enter"),
                        E("Leader", "Click the leader: it leaves its place for the right, shows where it acts, and its crown glows; click the crown to use it, or the empty place (or right-click, Esc) to put it back. Or X, then X again or Enter.", "key:X", "crown"),
                        E("Pass", "Hold Space, or hold the Hold to pass button.", "key:Space"),
                        E("Look", "Hover a card to see it large; click a discard pile to look through it.", "card:14"),
                        E("Pause", "Esc or the menu button: resume, settings, or quit the game (outside the browser).", "key:Esc"),
                        E("Give up", "Lay your hand on your deck: in your turn, the cursor over it turns into a hand; click, then confirm. It counts as a loss.", "back:NR"),
                    },
                },
            };
        }

        private static readonly FactionTopic[] FactionList =
        {
            new FactionTopic { Faction = Faction.NorthernRealms, Perk = "Draws a card from the deck each time it wins a round.", Leaders = new[] { 51, 52, 53, 54, 55 } },
            new FactionTopic { Faction = Faction.Nilfgaard, Perk = "Wins every round that ends in a draw (against anyone but Nilfgaard).", Leaders = new[] { 33, 34, 35, 36, 37 } },
            new FactionTopic { Faction = Faction.Scoiatael, Perk = "Decides who goes first in the battle.", Leaders = new[] { 57, 58, 59, 60, 61 } },
            new FactionTopic { Faction = Faction.Monsters, Perk = "One random unit stays on the board after each round.", Leaders = new[] { 40, 41, 42, 43, 44 } },
            new FactionTopic { Faction = Faction.Skellige, Perk = "Two random units of its discard pile (no heroes or spies) return to the board at the start of the third round.", Leaders = new[] { 157, 165 } },
        };

        // The leaders as The Witcher 3's cards name them (their abilities: CardDescription, as on their cards)
        private static readonly Dictionary<int, string> LeaderNames = new Dictionary<int, string>
        {
            { 51, "Foltest: King of Temeria" },
            { 52, "Foltest: Lord Commander of the North" },
            { 53, "Foltest: Son of Medell" },
            { 54, "Foltest: The Siegemaster" },
            { 55, "Foltest: The Steel-Forged" },
            { 33, "Emhyr var Emreis: Emperor of Nilfgaard" },
            { 34, "Emhyr var Emreis: His Imperial Majesty" },
            { 35, "Emhyr var Emreis: Invader of the North" },
            { 36, "Emhyr var Emreis: The Relentless" },
            { 37, "Emhyr var Emreis: The White Flame" },
            { 57, "Francesca Findabair: Daisy of the Valley" },
            { 58, "Francesca Findabair: Hope of the Aen Seidhe" },
            { 59, "Francesca Findabair: Pureblood Elf" },
            { 60, "Francesca Findabair: Queen of Dol Blathanna" },
            { 61, "Francesca Findabair: The Beautiful" },
            { 40, "Eredin Bréacc Glas: The Treacherous" },
            { 41, "Eredin: Bringer of Death" },
            { 42, "Eredin: Commander of the Red Riders" },
            { 43, "Eredin: Destroyer of Worlds" },
            { 44, "Eredin: King of the Wild Hunt" },
            { 157, "Crach an Craite" },
            { 165, "King Bran" },
        };

        // The pieces are drawn at this scale, so they sit in the page's icon column as its icons did
        private const float PieceScale = 0.7f;
        private const float ClusterHeight = 104;

        public static void Open(Action on_close)
        {
            UIScreens.ShowDialog("RulesPage", UITheme.Current.rulesPage, Bind, on_close);
        }

        private static void Bind(VisualElement root)
        {
            Topic[] topics = Topics();
            ScrollView view = root.Q<ScrollView>("Topic");
            view.mouseWheelScrollSize = 40;
            view.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            VisualElement list = root.Q("Topics");
            List<MenuEntry> tabs = new List<MenuEntry>();
            for (int i = 0; i < topics.Length; i++)
            {
                int index = i;
                MenuEntry tab = new MenuEntry { name = "Item " + topics[i].Name, text = topics[i].Name };
                tab.AddToClassList("rules__topic");
                tab.clicked += () => Show(view, topics[index], tabs, index);
                list.Add(tab);
                tabs.Add(tab);
            }
            root.Q<Button>("Button Close").clicked += () => UIScreens.Close(root);

            Show(view, topics[0], tabs, 0);
            root.schedule.Execute(() => UIScreens.Focus(tabs[0]));
        }

        // The pieces' columns of a topic, to be made as wide as its widest group
        private class Columns
        {
            public readonly List<VisualElement> All = new List<VisualElement>();
            public float Widest;
        }

        private static void Show(ScrollView view, Topic topic, List<MenuEntry> tabs, int index)
        {
            for (int i = 0; i < tabs.Count; i++)
                tabs[i].EnableInClassList("rules__topic--current", i == index);
            view.Clear();
            Label intro = new Label(topic.Intro);
            intro.AddToClassList("rules__intro");
            view.Add(intro);

            Columns columns = new Columns();
            int order = 0;
            if (topic.Factions)
                foreach (FactionTopic faction in FactionList)
                    FadeIn(AddFaction(view, faction, columns), order++);
            else
                foreach (Entry entry in topic.Entries)
                {
                    VisualElement text;
                    FadeIn(AddEntry(view, entry.Pieces, entry.Title, entry.Text, columns, out text), order++);
                }
            // One column for the topic's pieces, as wide as its widest group: the texts line up, close to their pieces
            foreach (VisualElement column in columns.All)
                column.style.width = Mathf.Max(64, columns.Widest);
            view.scrollOffset = Vector2.zero;
        }

        // The rule's pieces beside its gold title and its text; text: the text's column (for more below it)
        private static VisualElement AddEntry(VisualElement parent, string[] pieces, string title_text, string text_text, Columns columns, out VisualElement text)
        {
            VisualElement row = new VisualElement { name = "Entry " + title_text };
            row.AddToClassList("rules__row");
            Cluster(row, pieces, columns);

            text = new VisualElement();
            text.AddToClassList("rules__text");
            Label title = new Label(title_text);
            title.AddToClassList("rules__title");
            Label body = new Label(text_text);
            body.AddToClassList("rules__line");
            text.Add(title);
            text.Add(body);
            row.Add(text);
            parent.Add(row);
            return row;
        }

        // The pieces side by side, overlapping a little where they do on the printed rule cards, the group centred in its
        // column and drawn at PieceScale (sizes below are the pieces' own)
        private static void Cluster(VisualElement row, string[] pieces, Columns columns)
        {
            VisualElement column = new VisualElement { name = "Pieces" };
            column.AddToClassList("rules__pieces");
            VisualElement group = new VisualElement { name = "Group", pickingMode = PickingMode.Ignore };

            List<VisualElement> made = new List<VisualElement>();
            float total = 0;
            foreach (string spec in pieces)
            {
                float overlap, rise;
                Vector2 size;
                VisualElement piece = Piece(spec, made.Count, out size, out overlap, out rise);
                if (piece == null)
                    continue;
                if (made.Count > 0)
                    total -= overlap;
                piece.style.left = total;
                piece.style.top = (ClusterHeight - size.y) / 2 - rise;
                piece.style.width = size.x;
                piece.style.height = size.y;
                made.Add(piece);
                total += size.x;
            }
            // Drawn left to right, the first on top (as the printed cards' fanned shields)
            for (int i = made.Count - 1; i >= 0; i--)
                group.Add(made[i]);

            group.style.position = Position.Absolute;
            group.style.width = total;
            group.style.height = ClusterHeight;
            group.style.left = Length.Percent(50);
            group.style.marginLeft = -total / 2;
            group.style.top = (ClusterHeight * PieceScale - ClusterHeight) / 2;
            group.style.scale = new Scale(new Vector2(PieceScale, PieceScale));
            column.Add(group);
            row.Add(column);
            columns.All.Add(column);
            columns.Widest = Mathf.Max(columns.Widest, total * PieceScale);
        }

        // One piece, from its description, at its own size; index: its place in the group; overlap: how far it slides
        // under the piece before it; rise: how much higher than the middle it sits
        private static VisualElement Piece(string spec, int index, out Vector2 size, out float overlap, out float rise)
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
            rise = 0;
            size = Vector2.zero;
            switch (kind)
            {
                case "shield":
                    // Fanned, every other one a little higher
                    overlap = 24;
                    rise = index % 2 == 0 ? 5 : -5;
                    return Picture(SpriteCache.Load("Stats/faction/" + arg), size = new Vector2(76, 85));
                case "cards":
                    return Picture(SpriteCache.Load("Stats/cards"), size = new Vector2(46, 63));
                case "num":
                    {
                        overlap = -10;
                        VisualElement number = Box("Number", size = new Vector2(96, 80));
                        number.Add(Number(arg, 66, theme.goldBright));
                        return number;
                    }
                case "emblem":
                    overlap = 4;
                    return Picture(theme.rowEmblems[int.Parse(arg)], size = new Vector2(78, 78));
                case "medal":
                    overlap = 22;
                    return Picture(SpriteCache.Load("UI/Rules/" + arg), size = new Vector2(98, 98));
                case "gem":
                    overlap = -8;
                    return Picture(theme.gem, size = new Vector2(58, 59));
                case "notice":
                    return Picture(SpriteCache.Load("W3/Notifications/" + arg), size = new Vector2(126, 90));
                case "card":
                    overlap = -8;
                    return CardPicture(SpriteCache.Load("Cards/List/135x87/" + arg), size = new Vector2(64, 99));
                case "back":
                    overlap = 38;
                    return CardPicture(SpriteCache.Load("Cards/Back/" + arg), size = new Vector2(64, 99));
                case "crown":
                    return Picture(theme.leaderReady, size = new Vector2(76, 76));
                case "score":
                    {
                        overlap = -10;
                        VisualElement disc = Picture(theme.scoreDisc, size = new Vector2(64, 65));
                        if (disc != null)
                            disc.Add(Number(arg, 32, new Color(0.24f, 0.15f, 0.07f)));
                        return disc;
                    }
                case "total":
                    {
                        VisualElement laurel = Picture(SpriteCache.Load("Various/pass"), size = new Vector2(112, 97));
                        VisualElement disc = Picture(theme.scoreTotal, new Vector2(86, 86));
                        if (laurel == null || disc == null)
                            return laurel;
                        disc.style.left = (112 - 86) / 2f;
                        disc.style.top = (97 - 86) / 2f - 2;
                        disc.style.width = 86;
                        disc.style.height = 86;
                        disc.Add(Number(arg, 34, new Color(0.11f, 0.08f, 0.05f)));
                        laurel.Add(disc);
                        return laurel;
                    }
                case "key":
                    overlap = -10;
                    return Key(arg, out size);
            }
            return null;
        }

        private static VisualElement Box(string name, Vector2 size)
        {
            VisualElement box = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            box.AddToClassList("piece");
            return box;
        }

        // A picture of the game's, kept in shape (none: no piece)
        private static VisualElement Picture(Sprite sprite, Vector2 size)
        {
            if (sprite == null)
                return null;
            VisualElement picture = Box(sprite.name, size);
            picture.style.backgroundImage = new StyleBackground(sprite);
            return picture;
        }

        // A card, with the soft shadow the board gives its cards
        private static VisualElement CardPicture(Sprite sprite, Vector2 size)
        {
            if (sprite == null)
                return null;
            VisualElement card = Box(sprite.name, size);
            VisualElement shadow = Box("Shadow", size);
            shadow.style.left = -9;
            shadow.style.right = -9;
            shadow.style.top = -6;
            shadow.style.bottom = -12;
            shadow.style.backgroundImage = new StyleBackground(UITheme.Current.softShadow);
            shadow.style.unityBackgroundImageTintColor = new Color(0, 0, 0, 0.6f);
            shadow.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100)); // stretched, not kept in shape
            VisualElement art = Box("Art", size);
            art.style.left = 0;
            art.style.right = 0;
            art.style.top = 0;
            art.style.bottom = 0;
            art.style.backgroundImage = new StyleBackground(sprite);
            card.Add(shadow);
            card.Add(art);
            return card;
        }

        private static Label Number(string text, float size, Color color)
        {
            Label number = new Label(text) { pickingMode = PickingMode.Ignore };
            number.AddToClassList("piece__number");
            number.style.fontSize = size;
            number.style.color = color;
            return number;
        }

        // A key of the keyboard: a dark key with a gold outline; < and > show as chevrons
        private static VisualElement Key(string label, out Vector2 size)
        {
            UITheme theme = UITheme.Current;
            Sprite chevron = label == "<" ? theme.chevronLeft : label == ">" ? theme.chevronRight : null;
            size = new Vector2(chevron != null ? 62 : Mathf.Max(62, 34 + label.Length * 19), 60);
            VisualElement key = Box("Key " + label, size);
            key.AddToClassList("piece--key");
            if (chevron != null)
            {
                VisualElement glyph = new VisualElement { pickingMode = PickingMode.Ignore };
                glyph.style.width = 34;
                glyph.style.height = 34;
                glyph.style.backgroundImage = new StyleBackground(chevron);
                glyph.style.unityBackgroundImageTintColor = theme.goldBright;
                key.Add(glyph);
            }
            else
            {
                Label text = new Label(label) { pickingMode = PickingMode.Ignore };
                text.AddToClassList("piece__key-label");
                text.style.fontSize = 30;
                key.Add(text);
            }
            return key;
        }

        //----------------------------------------Factions----------------------------------------//
        // A faction like any rule (its shield, its name, its perk), its leaders below the perk
        private static VisualElement AddFaction(VisualElement parent, FactionTopic faction, Columns columns)
        {
            VisualElement text;
            VisualElement row = AddEntry(parent, new[] { "shield:" + faction.Faction.Code() }, faction.Faction.DisplayName(), faction.Perk, columns, out text);
            VisualElement leaders = new VisualElement { name = "Leaders" };
            leaders.AddToClassList("rules__leaders");
            foreach (int leader in faction.Leaders)
                leaders.Add(LeaderPortrait(leader));
            text.Add(leaders);
            return row;
        }

        // A round portrait of the leader in a gold ring; its ability shows over it on hover (a held finger with touch)
        private static VisualElement LeaderPortrait(int leader_id)
        {
            VisualElement holder = new VisualElement { name = "Leader " + leader_id };
            holder.AddToClassList("rules__leader");
            VisualElement art = new VisualElement { name = "Art", pickingMode = PickingMode.Ignore };
            art.AddToClassList("rules__leader-art");
            CardArt.Show(art, leader_id);
            holder.Add(art);
            string name;
            string ability = CardDescription.LeaderAbility(leader_id);
            if (LeaderNames.TryGetValue(leader_id, out name) && ability != null)
                ElementTooltip.Attach(holder, () => "<b>" + name + "</b>\n" + ability);
            return holder;
        }

        //----------------------------------------A little life----------------------------------------//
        // The rules come in one after another when a topic opens
        private static void FadeIn(VisualElement row, int order)
        {
            row.schedule.Execute(() => row.AddToClassList("rules__row--shown")).StartingIn((long)((0.04f + order * 0.05f) * 1000));
        }
    }
}
