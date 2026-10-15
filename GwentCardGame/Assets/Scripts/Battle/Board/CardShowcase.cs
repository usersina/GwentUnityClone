using System;
using System.Collections;
using Gwent.Core;
using Gwent.Rules;
using Gwent.UI;
using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace Gwent.Battle
{
    // The Witcher 3's showcase: the card being played, large on the right of the board with its description under it
    // (where hovering a card previews it). The player's selected card pops out of the hand and glides there while its
    // place is chosen: its slot in the hand stays open, and clicking it puts the card back. The opponent's card comes in
    // from above the board and stays a moment before it is played. While a card is shown, hovering does not preview.
    public static class CardShowcase
    {
        public const float GlideSeconds = 0.4f;
        public const float HoldSeconds = 1.2f;
        private const float ReturnSeconds = 0.25f;
        private const float Pop = 2.2f; // how much larger a selected card pops out of its hand slot

        private static RectTransform shown; // the showcased card on the overlay (its glow, its art)
        private static GameObject box;      // its description
        private static GameObject handCard; // hidden in the hand while it is shown
        private static MotionHandle glide;  // the showcased card's glide to the right

        public static bool IsShown { get { return shown != null; } }

        // The player's selected hand card, popped out of its slot. The leader stays in its place (a second click on it
        // uses it).
        public static void Select(GameObject hand_card, Card card, bool leaves_slot = true)
        {
            Clear();
            Rect slot = CardMotion.ScreenRect((RectTransform)hand_card.transform);
            Vector2 size = slot.size * Pop;
            handCard = hand_card;
            if (leaves_slot)
                CardMotion.Hide(hand_card, true, true); // its empty slot puts it back when clicked
            Show(card, new Rect(slot.center.x - size.x / 2, slot.yMin, size.x, size.y), true);
        }

        // A card chosen from a pile (medic, weather from the deck), coming from that pile
        public static void Pick(Card card, Rect from)
        {
            Clear();
            Show(card, from, true);
        }

        // The opponent's card: in from above the board, then a moment in the showcase
        public static IEnumerator Opponent(Card card, Rect from)
        {
            Clear();
            Show(card, from, false);
            yield return new WaitForSeconds(GlideSeconds + HoldSeconds);
        }

        // The showcase's size on screen
        public static Vector2 ScreenSize()
        {
            return Layout(true).sizeDelta * UIOverlay.FixedRoot.GetComponent<Canvas>().scaleFactor;
        }

        // Deselected: the card glides back into its slot in the hand
        public static void Deselect()
        {
            GameObject slot = handCard;
            handCard = null;
            if (shown == null || slot == null)
            {
                Clear();
                CardMotion.Hide(slot, false);
                return;
            }
            RectTransform card = shown;
            shown = null;
            DestroyBox();
            Rect last = CardMotion.ScreenRect(card);
            glide.TryCancel();
            CardMotion.GlideOver(card, () => slot != null ? last = CardMotion.ScreenRect((RectTransform)slot.transform) : last, ReturnSeconds, () =>
            {
                if (handCard != slot) // selected again meanwhile: it stays in the showcase
                    CardMotion.Hide(slot, false);
                UnityEngine.Object.Destroy(card.gameObject);
            });
        }

        // Gone at once (played: its flight from the board's edge takes over)
        public static void Clear()
        {
            CardMotion.Hide(handCard, false);
            handCard = null;
            if (shown != null)
                UnityEngine.Object.Destroy(shown.gameObject);
            shown = null;
            DestroyBox();
        }

        private static void Show(Card card, Rect from, bool glow)
        {
            // One card on the right at a time: the showcase takes the place of any preview
            CardHover.ClosePreview();
            GameObject go = new GameObject("CardShowcase", typeof(RectTransform));
            go.transform.SetParent(CardMotion.Layer, false);
            shown = (RectTransform)go.transform;
            Image art = UIKit.NewImage(shown, "Art", SpriteCache.Load("Cards/List/591x380/" + card._id), Color.white);
            art.preserveAspect = true;
            art.rectTransform.Stretch();
            if (glow)
            {
                // The selected card: a thin gold edge (no halo), the light rising across it, and as it pops out of the
                // hand, a brief orange flash on its edges (The Witcher 3)
                Image edge = UIKit.NewImage(shown, "Edge", UITheme.Current.panelOutline, UITheme.Current.goldBright);
                edge.pixelsPerUnitMultiplier = 0.5f; // twice the outline's width: a hair line gets lost between pixels
                edge.rectTransform.Stretch(-3, -3, -3, -3);
                UIMotion.Breathe(edge.gameObject, 0.55f, 1.2f); // the gold edge pulses gently
                CardShine.Attach(shown, "Shine", GlideSeconds + 1.5f, 5f); // late, and now and then
                Image flash = UIKit.NewImage(shown, "PopFlash", UITheme.Current.softShadow, new Color(1f, 0.55f, 0.12f, 0.9f));
                flash.rectTransform.Stretch(-14, -14, -14, -14);
                flash.transform.SetAsFirstSibling();
                UIMotion.FlashOut(flash, 0.25f);
            }
            CardMotion.Place(shown, from);

            RectTransform target = Layout(true);
            RectTransform showcase = shown;
            glide = CardMotion.GlideOver(showcase, () => LayoutToScreen(target), GlideSeconds, () =>
            {
                // Its description, under it (a plain unit has none)
                string description = CardHover.EffectBoxPath(card);
                if (shown != showcase || description == null)
                    return;
                RectTransform layout = Layout(false);
                Image text = UIKit.NewImage(CardMotion.Layer, "CardShowcaseText", SpriteCache.Load(description), Color.white);
                text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                text.rectTransform.anchoredPosition = layout.anchoredPosition;
                text.rectTransform.sizeDelta = layout.sizeDelta;
                UIMotion.Appear(text.gameObject);
                box = text.gameObject;
            });
        }

        private static void DestroyBox()
        {
            if (box != null)
                UnityEngine.Object.Destroy(box);
            box = null;
        }

        // Where a card's description shows on the right (screen rectangle)
        public static Rect DescriptionScreenRect()
        {
            return LayoutToScreen(Layout(false));
        }

        // Where hovering a card previews it (the card prefab's big image and description), on the overlay with the same
        // reference size as the battle's canvas
        private static RectTransform Layout(bool art)
        {
            CardHover hover = BattleController.Instance.CardPrefab.GetComponent<CardHover>();
            return (RectTransform)(art ? hover.BigImage : hover.BigEffect).transform;
        }

        private static Rect LayoutToScreen(RectTransform layout)
        {
            Transform root = UIOverlay.FixedRoot;
            float scale = root.GetComponent<Canvas>().scaleFactor;
            Vector2 center = root.TransformPoint(layout.anchoredPosition);
            Vector2 size = layout.sizeDelta * scale;
            return new Rect(center - size / 2, size);
        }
    }
}
