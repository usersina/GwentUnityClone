using System;
using System.Collections.Generic;
using Gwent.Core;
using Gwent.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Gwent.Battle
{
    // A choice among cards as The Witcher 3 shows it (a medic's revival, a leader's pick, the opponent's hand, a discard
    // pile): a band across the top with the title, the carousel, the front card's description at the bottom right. A
    // click on the front card (or Enter) picks it; the band's × gives the choice up (asked first) or closes a look. Down
    // goes to the ×, Up comes back to the cards.
    public class CardPicker : CardCarousel
    {
        private Action<int> pick;     // null: the cards are only shown
        private Button button;
        private GameObject modalRoot; // the dialog it stands in, when it is one (a discard pile)

        public Button BandButton { get { return button; } }

        // Whether a card is to be picked (its × gives the choice up), not only looked at (its × closes)
        public bool Picks { get { return pick != null; } }

        // Shows the cards under the title, the one at front_index in front; pick gets the chosen card (null: nothing to
        // pick, Enter presses the ×), the × does on_button (gives up or closes). The picker's parent is its root: destroy
        // it to close.
        public static CardPicker Open(Transform parent, string title, IList<int> card_ids, Action<int> pick, Action on_button, int front_index = 0)
        {
            RectTransform root = UIKit.Rect("CardPicker", parent).Stretch();
            TextMeshProUGUI title_text;
            Button button = Band(root, title, null, null, on_button, out title_text);
            button.navigation = new Navigation { mode = Navigation.Mode.None }; // the arrows are the cards'

            CardPicker picker = Create<CardPicker>(root, new Vector2(0, 50));
            picker.name = "CardPickerCards";
            picker.pick = pick;
            picker.button = button;
            picker.focus = front_index;
            picker.Rebuild(card_ids);
            return picker;
        }

        // The picker stands in this dialog: it takes the keys while the dialog is on top
        public void StandIn(GameObject modal_root)
        {
            modalRoot = modal_root;
        }

        protected override bool TakesInput()
        {
            return modalRoot != null ? UIOverlay.IsTopModal(modalRoot) : base.TakesInput();
        }

        // A click on the front card picks it; when there is nothing to pick it does nothing (Enter presses the button)
        public override void OnCardClicked(int index)
        {
            if (pick != null || index != focus)
                base.OnCardClicked(index);
        }

        protected override void Choose()
        {
            if (pick == null)
            {
                button.onClick.Invoke();
                return;
            }
            if (focus >= shown.Count)
                return;
            Action<int> chosen = pick;
            pick = null; // once
            chosen(shown[focus]);
        }

        protected override void HandleKeys(Keyboard keyboard)
        {
            if (keyboard == null)
                return;
            bool on_button = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == button.gameObject;
            bool sideways = keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame
                || keyboard.aKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame;
            if (on_button && sideways)
                EventSystem.current.SetSelectedGameObject(null); // back to the cards
            else if ((keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) && !on_button)
                UIKit.Focus(button);
            else if ((keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) && on_button)
                EventSystem.current.SetSelectedGameObject(null);
            else if (!on_button)
                base.HandleKeys(keyboard); // Enter on the button is the button's
        }
    }
}
