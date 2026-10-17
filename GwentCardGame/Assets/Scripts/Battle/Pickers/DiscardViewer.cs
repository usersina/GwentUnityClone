using System.Collections.Generic;
using Gwent.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Gwent.Battle
{
    // A discard pile in the carousel, as The Witcher 3 shows it (click a pile on the board), its top card in front. The
    // battle waits while it is open; Close, Enter, Escape or a right click puts it away.
    public class DiscardViewer : MonoBehaviour
    {
        private static DiscardViewer current;
        private float previousTimeScale;

        public static bool IsOpen { get { return current != null; } }

        // Closes it if it is open (the battle ends under it), restoring the time scale
        public static void Close()
        {
            if (current != null)
                UIOverlay.CloseModal(current.gameObject);
        }

        public static void Open(List<int> cards, bool is_player)
        {
            if (current != null || cards == null || cards.Count == 0)
                return;
            // Over the whole screen, so the board under it takes no click
            RectTransform root = UIKit.Rect("DiscardViewer", UIOverlay.Root).Stretch();
            Image blocker = root.gameObject.AddComponent<Image>();
            blocker.color = Color.clear;
            current = root.gameObject.AddComponent<DiscardViewer>();
            current.previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            string whose = is_player ? "Your discard pile" : "Your opponent's discard pile";
            CardPicker picker = CardPicker.Open(root, whose + "  (" + cards.Count + ")", cards, null, () => UIOverlay.CloseModal(root.gameObject), cards.Count - 1);
            picker.StandIn(root.gameObject);
            UIOverlay.PushModal(root.gameObject, current.Closed);
        }

        private void Closed()
        {
            Time.timeScale = previousTimeScale > 0 ? previousTimeScale : 1f;
            if (current == this)
                current = null;
        }

        private void OnDestroy()
        {
            if (current == this)
                current = null; // gone with the scene (which sets its own time scale)
        }
    }
}
