using System.Collections;
using System.Collections.Generic;
using Gwent.Core;
using Gwent.Rules;
using Gwent.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Gwent.Battle
{
    public class LeaderSlot : MonoBehaviour
    {
        [HideInInspector]
        public BattleController controller;

        public int leaderId;
        public Row type;

        private void Start()
        {
            controller = BattleController.Instance;
        }

        private Button activateButton;
        private bool used;
        private bool cancelled;
        private bool ready = true; // the ability can still be used this round (not used, cancelled or passed)
        private Sprite plainFace;  // the gold crown; the lit one is UITheme.leaderReady

        private static readonly Color UsedGrey = new Color(0.45f, 0.45f, 0.45f, 0.9f);
        private static readonly Color PassiveGrey = new Color(0.78f, 0.78f, 0.78f, 1f);
        private static readonly Color CancelledRed = new Color32(255, 134, 134, 200);

        private Button ActivateButton
        {
            get
            {
                if (activateButton == null)
                    activateButton = transform.Find("Activate").GetComponent<Button>();
                return activateButton;
            }
        }

        // The opponent's crown only shows the state of their ability: it is not a button for the player
        public Button Button { get { return ActivateButton; } }

        private bool IsPlayers
        {
            get
            {
                if (controller == null)
                    controller = BattleController.Instance;
                return controller != null && transform.IsChildOf(controller.PlayerField.transform);
            }
        }

        private void Awake()
        {
            UITooltip tooltip = ActivateButton.gameObject.AddComponent<UITooltip>();
            tooltip.text = TooltipText;
        }

        public void UISetPassive()
        {
            if (!IsPlayers)
                ActivateButton.transition = Selectable.Transition.None; // no hover / press look
            Refresh();
        }

        // The ability was used: the crown greys out
        public void MarkUsed()
        {
            used = true;
            Refresh();
        }

        // Cancelled by the opponent's leader (Emhyr, The White Flame): the crown turns red
        public void MarkCancelled()
        {
            cancelled = true;
            Refresh();
        }

        // Whether the ability can still be used this round (a side that passed cannot), every frame
        public void UISetReady(bool can_use)
        {
            if (ready == can_use)
                return;
            ready = can_use;
            Refresh();
        }

        // The Witcher 3's crown: lit (blue) while the ability can still be used this round, grey once used, after
        // passing, or when passive; red when cancelled
        private void Refresh()
        {
            Button button = ActivateButton;
            Image face = button.targetGraphic as Image;
            if (face == null)
                return;
            if (plainFace == null)
                plainFace = face.sprite;
            bool passive = type == Row.LeaderPassive;
            bool lit = ready && !used && !cancelled && !passive && UITheme.Current.leaderReady != null;
            face.sprite = lit ? UITheme.Current.leaderReady : plainFace;
            face.color = cancelled ? CancelledRed : used ? UsedGrey : passive ? PassiveGrey : lit ? Color.white : UsedGrey;
            if (button.transition == Selectable.Transition.None)
                face.canvasRenderer.SetColor(Color.white); // no tint left over from the button's earlier look
            button.interactable = IsPlayers && !used && !cancelled && !passive;
            ColorBlock colors = button.colors;
            colors.disabledColor = Color.white; // the face carries the state
            button.colors = colors;
        }

        private string TooltipText()
        {
            string whose = IsPlayers ? "Leader ability" : "Opponent's leader ability";
            if (cancelled)
                return whose + ": cancelled by the other leader";
            if (type == Row.LeaderPassive)
                return whose + ": passive, always in effect";
            if (used)
                return whose + ": used";
            string press = GameInput.IsTouch ? "tap" : "click";
            return IsPlayers ? "Leader ability: " + press + " to choose it, " + press + " again to use it" : whose + ": not used yet";
        }

        // Reference: Activate button (and a click on the leader card). The first click chooses the leader, the second uses
        // it; the controller marks the button used once the ability really happened.
        public void ActivateLeader()
        {
            if (!IsPlayers)
                return;
            if (controller.battleState == BattleState.PlayerTurn)
                controller.LeaderClicked();
        }
    }
}
