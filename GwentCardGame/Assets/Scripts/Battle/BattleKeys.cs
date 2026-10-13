using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// The battle's keyboard shortcuts (desktop): X picks the leader and X again uses it; Enter plays the selected card
// when only one place fits it. Space (hold to pass) is the pass button's.
public class BattleKeys : MonoBehaviour
{
    private const float LeaderConfirmSeconds = 3f;
    private float leaderArmedUntil = -1;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        SceneController battle = SceneController.Instance;
        if (keyboard == null || battle == null || UIOverlay.HasModal)
            return;

        // The "press X again" window closed: the leader button gives up the focus (Enter would use the leader)
        if (leaderArmedUntil > 0 && Time.unscaledTime > leaderArmedUntil)
        {
            leaderArmedUntil = -1;
            if (EventSystem.current != null && battle.PlayerLeader.Button != null && EventSystem.current.currentSelectedGameObject == battle.PlayerLeader.Button.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }

        // Right-click puts a selected card back in the hand (The Witcher 3)
        if (GameInput.SecondaryPressed() && battle.selectedCard != null && battle.IsPlayerActionAllowed)
            battle.PutBackSelectedCard();

        if (keyboard.xKey.wasPressedThisFrame)
            LeaderKey(battle);
        // Enter on a focused button is that button's (dialogs, the leader after X)
        GameObject focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool button_focused = focused != null && focused.activeInHierarchy;
        if ((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) && !button_focused)
            PlayWhereItFits(battle);
    }

    private void LeaderKey(SceneController battle)
    {
        if (!battle.CanUseLeader(true))
        {
            GameAudio.PlaySfx("invalid");
            leaderArmedUntil = -1;
            return;
        }
        if (Time.unscaledTime <= leaderArmedUntil)
        {
            leaderArmedUntil = -1;
            battle.ActivateLeader(battle.PlayerInfo.Leader, true);
            return;
        }
        leaderArmedUntil = Time.unscaledTime + LeaderConfirmSeconds;
        GameAudio.PlaySfx("card_select");
        UIKit.Focus(battle.PlayerLeader.Button);
        UIOverlay.Toast("Press X again to use your leader ability", LeaderConfirmSeconds);
    }

    // The lit rows (both sides: spies), the lit weather board and the units framed for a decoy
    private static void PlayWhereItFits(SceneController battle)
    {
        if (!battle.IsSelectedCardPlayable())
            return;
        List<GameObject> rows = battle.PlayerField.GetComponent<RowPicker>().highLightedRows
            .Concat(battle.EnemyField.GetComponent<RowPicker>().highLightedRows).ToList();
        WeatherManager weather = battle.WeatherField.GetComponent<WeatherManager>();
        List<GameObject> decoy_targets = battle.PlayerField.GetComponentsInChildren<Transform>()
            .Where(t => t.name == BoardView.DecoyFrame).Select(t => t.parent.gameObject).ToList();

        int places = rows.Count + (weather.isWeatherCard ? 1 : 0) + decoy_targets.Count;
        if (places != 1)
        {
            GameAudio.PlaySfx("invalid");
            UIOverlay.Toast(places == 0 ? "This card has nowhere to go" : "This card fits more than one place: click where it goes");
            return;
        }
        if (rows.Count == 1)
            rows[0].GetComponent<RowClick>().OnRowClick();
        else if (weather.isWeatherCard)
            weather.OnWeatherClick();
        else
            battle.TryPlaySelectedDecoyOnCard(decoy_targets[0]);
    }
}
