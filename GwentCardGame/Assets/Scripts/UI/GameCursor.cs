using Gwent.Battle;
using Gwent.Core;
using Gwent.Rules;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Gwent.UI
{
    // The Witcher-style mouse cursor (desktop): a silver arrow, gold over anything that reacts to a click, a red "no" badge
    // where the selected card cannot go, and a gold hand over the player's own deck in their turn (laying a hand on it
    // gives up the battle). Reads the UI's own raycast under the mouse every frame and only changes the hardware cursor
    // when the state changes.
    public class GameCursor : MonoBehaviour
    {
        public enum State { Default, Interact, Invalid, GiveUp }

        public static readonly Vector2 Hotspot = new Vector2(5, 4);       // the arrows' tip (UISpriteGenerator draws them)
        public static readonly Vector2 HandHotspot = new Vector2(30, 40); // the hand's palm
        private static GameCursor instance;
        private State current = (State)(-1);

        public static State Current { get { return instance != null ? instance.current : State.Default; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null || GameInput.IsTouch)
                return;
            GameObject go = new GameObject("GameCursor");
            instance = go.AddComponent<GameCursor>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            Apply(Evaluate());
        }

        private void OnDestroy()
        {
            if (instance == this)
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        private void Apply(State state)
        {
            if (state == current)
                return;
            current = state;
            UITheme theme = UITheme.Current;
            if (state == State.GiveUp && theme.cursorGiveUp != null)
            {
                Cursor.SetCursor(theme.cursorGiveUp, HandHotspot, CursorMode.Auto);
                return;
            }
            Texture2D texture = state == State.Interact || state == State.GiveUp ? theme.cursorInteract : state == State.Invalid ? theme.cursorInvalid : theme.cursorDefault;
            Cursor.SetCursor(texture, Hotspot, CursorMode.Auto);
        }

        //----------------------------------------What is under the mouse----------------------------------------//
        private static State Evaluate()
        {
            Mouse mouse = Mouse.current;
            EventSystem events = EventSystem.current;
            InputSystemUIInputModule module = events != null ? events.currentInputModule as InputSystemUIInputModule : null;
            if (mouse == null || module == null)
                return State.Default;
            GameObject hovered = module.GetLastRaycastResult(mouse.deviceId).gameObject;
            if (hovered == null)
                return State.Default;

            // A UI Toolkit screen or dialog: its panel's event handler stands for all of it, so ask the panel what is there
            UnityEngine.UIElements.PanelEventHandler panel = hovered.GetComponentInParent<UnityEngine.UIElements.PanelEventHandler>();
            if (panel != null)
                return EvaluatePanel(panel.panel, mouse.position.ReadValue());

            // Dialogs and menus first: anything with a button (a disabled one is just a picture)
            Selectable selectable = hovered.GetComponentInParent<Selectable>();
            if (selectable != null)
                return selectable.IsInteractable() && selectable.enabled ? State.Interact : State.Default;

            // The player's own deck in their turn: a hand laid on it gives up the battle
            DeckGiveUp deck = hovered.GetComponentInParent<DeckGiveUp>();
            if (deck != null)
                return deck.Available ? State.GiveUp : State.Default;

            // A discard pile with cards opens for browsing
            DiscardPileBrowser pile = hovered.GetComponentInParent<DiscardPileBrowser>();
            if (pile != null)
                return pile.HasCards ? State.Interact : State.Default;

            // The battle board decides for its own cards, rows and weather board (rows only take a selected card)
            BattleController battle = BattleController.Instance;
            if (battle != null && !UIOverlay.HasModal)
            {
                State? board = EvaluateBoard(battle, hovered);
                if (board.HasValue)
                    return board.Value;
            }

            // Elsewhere (deck builder cards, the redraw carousel, the pass button...): whatever takes a click
            if (hovered.GetComponentInParent<IPointerClickHandler>() != null || hovered.GetComponentInParent<IPointerDownHandler>() != null)
            {
                // Cards lying on the board only show their preview
                CardSelect card = hovered.GetComponentInParent<CardSelect>();
                if (card != null && !card.isSelectable)
                    return State.Default;
                return State.Interact;
            }
            return State.Default;
        }

        // Over a UI Toolkit panel: a button (a menu entry, a dialog's, a stepper's arrow), a switch or a slider takes a
        // click while enabled; the rest (text, the dimmer, the gaps between entries) does not
        private static State EvaluatePanel(UnityEngine.UIElements.IPanel panel, Vector2 screen)
        {
            if (panel == null)
                return State.Default;
            Vector2 at = UnityEngine.UIElements.RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y)); // from the top left
            for (UnityEngine.UIElements.VisualElement element = panel.Pick(at); element != null; element = element.parent)
            {
                if (element is UnityEngine.UIElements.Button || element is UnityEngine.UIElements.Toggle
                    || element is UnityEngine.UIElements.Slider || element is UnityEngine.UIElements.SliderInt)
                    return element.enabledInHierarchy ? State.Interact : State.Default;
            }
            return State.Default;
        }

        // The battle board: what a click would do with (or without) a selected card; null when not over the board's
        // cards, rows or weather board
        private static State? EvaluateBoard(BattleController battle, GameObject hovered)
        {
            CardSelect card = hovered.GetComponentInParent<CardSelect>();
            RowClick row = hovered.GetComponentInParent<RowClick>();
            WeatherSlot weather = hovered.GetComponentInParent<WeatherSlot>();
            if (card == null && row == null && weather == null)
                return null;
            bool my_turn = battle.IsPlayerActionAllowed;

            // A hand card: playable when the player can act (or redrawable during the redraw)
            if (card != null && card.isSelectable && card.CompareTag("Player"))
                return my_turn || RedrawPicker.IsOpen ? State.Interact : State.Invalid;

            // The player's leader card: lifted by a click, put back by another on its empty slot
            if (card != null && !card.isSelectable && card.CompareTag("Player") && battle.selectedCard == null)
            {
                Card stats = card.GetComponent<CardFace>().Card;
                if (stats.ability == Gwent.Rules.Ability.Leader)
                    return battle.CanUseLeader(true) ? State.Interact : State.Default;
            }

            // The leader lifted: the board only shows where it acts, its crown uses it
            if (battle.LeaderSelected && my_turn)
                return State.Default;

            if (battle.selectedCard == null || !my_turn)
                return State.Default;

            // A card is selected: is this a place it can go?
            Card selected = battle.selectedCard.GetComponent<CardFace>().Card;
            if (selected.ability == Gwent.Rules.Ability.Decoy)
            {
                if (card == null || card.isSelectable)
                    return row != null || weather != null ? State.Invalid : State.Default;
                Card target = card.GetComponent<CardFace>().Card;
                bool swappable = card.CompareTag("Player") && row != null && !target.unique
                    && target.faction != Gwent.Rules.Faction.Special && target.ability != Gwent.Rules.Ability.Leader;
                return swappable ? State.Interact : State.Invalid;
            }
            if (weather != null)
                return weather.isWeatherCard ? State.Interact : State.Invalid;
            if (row != null)
            {
                RowPicker picker = row.transform.parent.parent.GetComponent<RowPicker>();
                bool lit = picker != null && picker.highLightedRows.Contains(row.gameObject);
                return lit ? State.Interact : State.Invalid;
            }
            return State.Default;
        }
    }
}
