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
    // The Witcher-style mouse cursor (desktop): a silver arrow, gold over anything that reacts to a click, and a red
    // "no" badge where the selected card cannot go. Reads the UI's own raycast under the mouse every frame and only
    // changes the hardware cursor when the state changes.
    public class GameCursor : MonoBehaviour
    {
        public enum State { Default, Interact, Invalid }

        private static readonly Vector2 Hotspot = new Vector2(5, 4);
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
            Texture2D texture = state == State.Interact ? theme.cursorInteract : state == State.Invalid ? theme.cursorInvalid : theme.cursorDefault;
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

            // Dialogs and menus first: anything with a button (a disabled one is just a picture)
            Selectable selectable = hovered.GetComponentInParent<Selectable>();
            if (selectable != null)
                return selectable.IsInteractable() && selectable.enabled ? State.Interact : State.Default;

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

            // The player's leader card: chosen by a click, used by a second one
            if (card != null && !card.isSelectable && card.CompareTag("Player") && battle.selectedCard == null)
            {
                Card stats = card.GetComponent<CardFace>().Card;
                if (stats.ability == Gwent.Rules.Ability.Leader)
                    return battle.CanUseLeader(true) ? State.Interact : State.Default;
            }

            // The leader chosen: the places it lit use it
            if (battle.LeaderSelected && my_turn)
            {
                if (weather != null)
                    return battle.IsLeaderTarget(weather.gameObject) ? State.Interact : State.Invalid;
                if (row != null)
                    return battle.IsLeaderTarget(row.gameObject) ? State.Interact : State.Invalid;
                return State.Default;
            }

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
