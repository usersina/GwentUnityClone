using Gwent.Core;
using Gwent.Rules;
using Gwent.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gwent.Battle
{
    // A card's description box (CardDescription), drawn live as The Witcher 3's: near black with a thin warm edge, the
    // ability's icon on a cream disc at the top left, the title and the text in muted gold, centred. It covers the Image
    // that holds its place (the hover preview's, the showcase's, the deck builder's, the carousel's, a leader window's),
    // laid out at 380 x 315 and scaled as a whole to fit it, so it looks the same everywhere.
    public class DescriptionBox : MonoBehaviour
    {
        public const float Width = 380, Height = 315;
        private const float Margin = 22, Top = 18, Bottom = 20, IconSize = 60, Gap = 16;

        private static readonly Color Fill = new Color32(22, 23, 25, 255);
        private static readonly Color Edge = new Color32(84, 74, 62, 255);
        private static readonly Color Disc = new Color32(236, 226, 204, 255);
        private static readonly Color TitleGold = new Color32(222, 190, 128, 255);
        private static readonly Color TextGold = new Color32(204, 180, 136, 255);

        private RectTransform content;
        private RectTransform group; // the icon, the title and the text
        private Image icon;
        private TextMeshProUGUI title;
        private TextMeshProUGUI text;

        // Shows the card's description over the holder, or hides the box for a card that has none (a plain unit).
        // Whether there is one.
        public static bool Show(Image holder, Card card)
        {
            CardDescription description = card != null ? CardDescription.For(card) : null;
            holder.enabled = false; // only its place: the box draws itself
            DescriptionBox box = holder.GetComponentInChildren<DescriptionBox>(true);
            if (box == null)
            {
                if (description == null)
                    return false;
                box = Create(holder);
            }
            box.gameObject.SetActive(description != null);
            if (description != null)
                box.Set(description);
            return description != null;
        }

        private static DescriptionBox Create(Image holder)
        {
            RectTransform root = UIKit.Rect("Description", holder.transform).Stretch();
            DescriptionBox box = root.gameObject.AddComponent<DescriptionBox>();

            box.content = UIKit.Rect("Content", root).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, Height));
            UIKit.NewImage(box.content, "Fill", null, Fill).rectTransform.Stretch();
            UIKit.Frame(box.content, "Edge", Edge, 2);
            box.group = UIKit.Rect("Group", box.content).Stretch();

            box.icon = UIKit.NewImage(box.group, "Icon", null, Disc);
            box.icon.rectTransform.Place(new Vector2(0, 1), new Vector2(Margin, -Top), new Vector2(IconSize, IconSize), new Vector2(0, 1));
            box.icon.preserveAspect = true;

            UITheme theme = UITheme.Current;
            box.title = UIKit.Text(box.group, "", UIKit.TextStyle.Body, TextAlignmentOptions.Center); // one line, beside the icon
            box.title.rectTransform.Stretch(Margin, Margin, Top, Height - Top - IconSize);
            box.title.font = theme.bodyMediumFont != null ? theme.bodyMediumFont : box.title.font;
            box.title.fontStyle = FontStyles.Bold;
            box.title.color = TitleGold;
            box.title.enableAutoSizing = true;
            box.title.fontSizeMin = 22;
            box.title.fontSizeMax = 34;

            box.text = UIKit.Text(box.group, "", UIKit.TextStyle.Body, TextAlignmentOptions.Top); // right under the title
            box.text.rectTransform.Stretch(Margin + 4, Margin + 4, Top + IconSize + Gap, Bottom);
            box.text.color = TextGold;
            box.text.textWrappingMode = TextWrappingModes.Normal;
            box.text.enableAutoSizing = true;
            box.text.fontSizeMin = 18;
            box.text.fontSizeMax = 27;
            box.text.lineSpacing = 6;

            // Only to read: never catches clicks meant for what is under it
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            box.Fit();
            return box;
        }

        private void Set(CardDescription description)
        {
            Sprite sprite = description.Icon != null ? SpriteCache.Load("Cards/DescriptionIcon/" + description.Icon) : null;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            // The title centred across the box (The Witcher 3), or in the room beside the icon when it is too long for that
            title.text = description.Title;
            float beside_icon = Margin + IconSize + 8;
            bool fits_centred = sprite == null || title.GetPreferredValues(description.Title).x <= Width - 2 * beside_icon;
            title.rectTransform.offsetMin = new Vector2(fits_centred ? (sprite != null ? beside_icon : Margin) : beside_icon, title.rectTransform.offsetMin.y);
            title.rectTransform.offsetMax = new Vector2(fits_centred && sprite != null ? -beside_icon : -Margin, title.rectTransform.offsetMax.y);
            text.text = description.Text;

            // The title and the text as one block, centred in the box (a short text leaves no empty bottom). Measured from
            // the font alone: a box filled before it joins a canvas (a leader window) has no text mesh yet.
            float text_height = string.IsNullOrEmpty(description.Text) ? 0
                : text.GetPreferredValues(description.Text, Width - 2 * (Margin + 4), 0).y;
            float room = Height - (Top + IconSize + Gap + text_height + Bottom);
            group.anchoredPosition = new Vector2(0, float.IsFinite(room) ? -Mathf.Clamp(room / 2, 0, Height / 2) : 0);
        }

        // The box at its size, scaled to fit its holder
        private void Fit()
        {
            if (content == null)
                return;
            Vector2 room = ((RectTransform)transform).rect.size;
            float scale = Mathf.Min(room.x / Width, room.y / Height);
            content.localScale = Vector3.one * (scale > 0 ? scale : 1);
        }

        private void OnRectTransformDimensionsChange()
        {
            Fit();
        }
    }
}
