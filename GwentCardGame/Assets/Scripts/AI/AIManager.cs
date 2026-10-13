using System;
using System.Collections.Generic;
using System.Linq;
using Gwent.Rules;
using UnityEngine;

// Plays the opponent's side. Decisions come from Gwent.Rules.AiBrain; this class only executes them,
// and always ends the AI's turn (falls back to passing if an action cannot be carried out).
public class AIManager : MonoBehaviour
{
    public GameObject tempObject;
    public GameObject cardPrefab;

    private SceneController controller;

    public void AIInitialize()
    {
        controller = GetComponent<SceneController>();
        if (controller == null)
            Debug.LogError("Could not initialize AI, scene controller not found!");
    }

    //----------------------------------------------AI Phases------------------------------------------------------------//
    // Initial redraw of up to 2 cards (a redrawn card goes to the bottom of the deck):
    // - the extra members of a muster family: the one kept calls them from the deck anyway
    // - then plain units weaker than an average unit of the deck
    public void AIExchangeCards()
    {
        PlayerState ai = controller.EnemyInfo;
        CardDatabase db = controller.Db;
        ai.CardsExchanged = 0;

        List<int> to_exchange = new List<int>();
        List<int> kept = new List<int>();
        IEnumerable<int> musters = ai.HandList.Where(id => db.Get(id).ability == Abilities.Muster)
            .OrderByDescending(id => ai.HandList.Count(other => AbilityRules.IsMusterRelative(db.Get(id), db.Get(other))));
        foreach (int card_id in musters)
        {
            if (kept.Any(caller => AbilityRules.IsMusterRelative(db.Get(caller), db.Get(card_id))))
                to_exchange.Add(card_id);
            else
                kept.Add(card_id);
        }

        List<Card> deck_units = ai.DeckList.Select(db.Get).Where(card => card.IsUnit).ToList();
        double average = deck_units.Count > 0 ? deck_units.Average(card => card.strength) : 0;
        to_exchange.AddRange(ai.HandList.Where(id =>
        {
            Card card = db.Get(id);
            return card.IsUnit && !card.unique && string.IsNullOrEmpty(card.ability) && card.strength < average - 1;
        }).OrderBy(id => db.Get(id).strength));

        foreach (int card_id in to_exchange.ToList())
        {
            if (ai.CardsExchanged >= 2)
                break;
            if (!ai.HandList.Contains(card_id))
                continue;
            controller.ExchangeCard(card_id, false);
            ai.CardsExchanged++;
        }

        ai.CanExchange = false;
        GameLog.Info("AI: Finished exchanging " + ai.CardsExchanged + " cards.");
    }

    // The AI's turn: exactly one action, then the turn passes to the player
    public void AIStartTurn()
    {
        if (controller == null)
            AIInitialize();

        AiAction action;
        try
        {
            action = AiBrain.Decide(BuildContext());
        }
        catch (Exception e)
        {
            Debug.LogError("AI decision failed, passing: " + e);
            action = AiAction.Pass();
        }
        GameLog.Info("AI: " + action);

        bool done;
        try
        {
            done = Execute(action);
        }
        catch (Exception e)
        {
            Debug.LogError("AI action failed: " + e);
            done = controller.battleState != BattleState.ENEMYTURN;
        }

        // Never leave the game waiting on the AI
        if (!done && controller.battleState == BattleState.ENEMYTURN)
            controller.SkipTurn(false);
    }

    private AiContext BuildContext()
    {
        return new AiContext
        {
            Me = controller.EnemyInfo,
            Opponent = controller.PlayerInfo,
            Weather = controller.weatherList,
            DoubleSpies = controller.doubleSpies,
            RandomMedic = controller.randomMedic,
            Db = controller.Db,
        };
    }

    // Returns false when nothing was done (the caller then passes)
    private bool Execute(AiAction action)
    {
        PlayerState ai = controller.EnemyInfo;
        switch (action.Type)
        {
            case AiActionType.PlayCard:
                if (!ai.HandList.Contains(action.CardId))
                    return false;
                if (action.CardId == CardIds.Decoy || controller.Db.Get(action.CardId).ability == Abilities.Decoy)
                {
                    if (action.TargetId < 0 || !Rows.IsUnitRow(action.Row))
                        return false;
                    controller.PlayDecoy(action.TargetId, action.Row, false, action.CardId);
                    return true;
                }
                controller.PlayCard(action.CardId, action.Row, false);
                return true;

            case AiActionType.UseLeader:
                if (LeaderIds.WeatherPickRow(ai.Leader) != null)
                    return controller.AiUseWeatherLeader(action.CardId);
                if (!controller.CanUseLeader(false))
                    return false;
                controller.ActivateLeader(ai.Leader, false);
                return controller.battleState != BattleState.ENEMYTURN || !ai.canLeader;

            default:
                controller.SkipTurn(false);
                return true;
        }
    }
}
