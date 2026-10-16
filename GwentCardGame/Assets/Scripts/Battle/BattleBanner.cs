using System.Collections.Generic;
using Gwent.Core;
using Gwent.Rules;
using Gwent.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gwent.Battle
{
    // The Witcher 3's notices across the middle of the screen ("Your turn!", "Your opponent has passed", "You won the
    // round!"): a translucent dark band, the game's bronze emblem left of centre (overflowing the band) and the message
    // in gold beside it. Notices queue up and show one after another, and the band never catches a click. Runs on game
    // time, so pausing the game holds it.
    public class BattleBanner : MonoBehaviour
    {
        // How long a notice stays, fades included (as measured in The Witcher 3)
        public const float Turn = 1.9f;
        public const float Short = Turn;
        public const float Long = 3.2f;
        private const float Fade = 0.2f;

        // Emblems (Resources/W3/Notifications)
        public const string YourTurn = "me_turn";
        public const string OpponentsTurn = "op_turn";
        public const string RoundStart = "round_start";
        public const string Passed = "round_passed";
        public const string RoundWon = "win_round";
        public const string RoundLost = "lose_round";
        public const string RoundDrawn = "draw_round";
        public const string YouFirst = "me_coin";
        public const string OpponentFirst = "op_coin";

        private const float BandHeight = 150;
        private const float EmblemHeight = 300;
        private const float EmblemX = -300;  // the emblem's centre, from the middle of the screen
        private const float TextX = -105;    // where the message starts
        private const float TextWidth = 900; // up to 75 from the right edge of the 1920 reference

        private struct Notice
        {
            public string Emblem;
            public string Text;
            public float Duration;
        }

        private static BattleBanner instance;
        private readonly Queue<Notice> queue = new Queue<Notice>();
        private CanvasGroup group;
        private Image emblem;
        private TextMeshProUGUI text;
        private float age = -1; // -1: nothing showing
        private float duration;

        // True while a notice shows or waits for its turn
        public static bool Busy { get { return instance != null && (instance.age >= 0 || instance.queue.Count > 0); } }

        // The faction's crest, for its perk's notice
        public static string FactionEmblem(Faction faction)
        {
            switch (faction)
            {
                case Gwent.Rules.Faction.NorthernRealms: return "north";
                case Gwent.Rules.Faction.Nilfgaard: return "nilfgaard";
                case Gwent.Rules.Faction.Monsters: return "monsters";
                case Gwent.Rules.Faction.Scoiatael: return "scoiatael";
                case Gwent.Rules.Faction.Skellige: return "skellige";
                default: return null;
            }
        }

        public static void Show(string emblem, string text, float duration = Turn)
        {
            if (instance == null)
                instance = Create();
            instance.queue.Enqueue(new Notice { Emblem = emblem, Text = text, Duration = duration });
            GameLog.Event(LogCategory.Ui, "notice", "text", text);
        }

        // Drops every notice (the battle is over)
        public static void Clear()
        {
            if (instance == null)
                return;
            instance.queue.Clear();
            instance.age = -1;
            instance.group.alpha = 0;
        }

        private static BattleBanner Create()
        {
            RectTransform root = Band("BattleBanner", UIOverlay.FixedRoot);
            BattleBanner banner = root.gameObject.AddComponent<BattleBanner>();
            banner.emblem = Emblem(root, EmblemX);
            banner.text = Text(root);
            banner.text.alignment = TextAlignmentOptions.MidlineLeft;
            banner.text.rectTransform.pivot = new Vector2(0, 0.5f);
            banner.text.rectTransform.anchoredPosition = new Vector2(TextX, 0);
            banner.text.rectTransform.sizeDelta = new Vector2(TextWidth, BandHeight);
            banner.text.textWrappingMode = TextWrappingModes.Normal; // a long notice takes two lines, as in The Witcher 3

            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>())
                graphic.raycastTarget = false;
            banner.group = root.gameObject.AddComponent<CanvasGroup>();
            banner.group.alpha = 0;
            banner.group.blocksRaycasts = false;
            banner.group.interactable = false;
            return banner;
        }

        // A notice's band across the middle of the parent (the battle's loading screen has one too)
        public static RectTransform Band(string name, Transform parent)
        {
            UITheme theme = UITheme.Current;
            RectTransform root = UIKit.Rect(name, parent);
            root.anchorMin = new Vector2(0, 0.5f);
            root.anchorMax = new Vector2(1, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(0, BandHeight);

            // A translucent band, a little darker through its middle: two halves of GradientV (opaque at its top), the
            // upper one turned over, darkest where they meet
            Image band = UIKit.NewImage(root, "Band", null, new Color(0, 0, 0, 0.5f));
            band.rectTransform.Stretch();
            for (int half = 0; half < 2; half++)
            {
                Image core = UIKit.NewImage(root, "Core", theme.gradientV, new Color(0, 0, 0, 0.25f));
                RectTransform rect = core.rectTransform;
                rect.anchorMin = new Vector2(0, 0.5f);
                rect.anchorMax = new Vector2(1, 0.5f);
                rect.pivot = new Vector2(0.5f, 1);
                rect.sizeDelta = new Vector2(0, BandHeight / 2 - 18);
                rect.anchoredPosition = Vector2.zero;
                if (half == 0)
                    rect.localEulerAngles = new Vector3(0, 0, 180);
            }
            return root;
        }

        // An emblem on a band, its centre that far from the middle (overflowing the band)
        public static Image Emblem(RectTransform band, float x, Sprite sprite = null)
        {
            Image emblem = UIKit.NewImage(band, "Emblem", sprite, Color.white);
            emblem.preserveAspect = true;
            emblem.rectTransform.anchorMin = emblem.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            emblem.rectTransform.anchoredPosition = new Vector2(x, 0);
            emblem.rectTransform.sizeDelta = new Vector2(EmblemHeight * 1.4f, EmblemHeight);
            emblem.enabled = sprite != null;
            return emblem;
        }

        // A notice's message on a band, in gold, centred on its middle
        public static TextMeshProUGUI Text(RectTransform band, string message = "")
        {
            UITheme theme = UITheme.Current;
            TextMeshProUGUI text = UIKit.Text(band, message, UIKit.TextStyle.Body, TextAlignmentOptions.Center);
            if (theme.bodyFont != null)
                text.font = theme.bodyFont;
            text.fontStyle = FontStyles.Bold;
            text.fontSize = 34;
            text.characterSpacing = 0;
            text.color = new Color32(0xE2, 0xBD, 0x7C, 0xFF);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.rectTransform.sizeDelta = new Vector2(TextWidth, BandHeight);
            return text;
        }

        private void Update()
        {
            if (age < 0)
            {
                if (queue.Count == 0)
                    return;
                Notice next = queue.Dequeue();
                Sprite sprite = next.Emblem != null ? SpriteCache.Load("W3/Notifications/" + next.Emblem) : null;
                emblem.sprite = sprite;
                emblem.enabled = sprite != null;
                text.text = next.Text;
                duration = next.Duration;
                age = 0;
            }

            age += Time.deltaTime;
            group.alpha = age < Fade ? age / Fade : Mathf.Clamp01((duration - age) / Fade);
            if (age >= duration)
            {
                age = -1;
                group.alpha = 0;
            }
        }
    }
}
