using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gwent.Rules;
using UnityEngine;

// Plays the opponent's side. Decisions come from Gwent.Rules.AiBrain at the difficulty of the settings; this class
// only executes them, and always ends the AI's turn (falls back to passing if an action cannot be carried out).
public class AIManager : MonoBehaviour
{
    // A move being decided. Easy and Normal decide at once; Hard plays battles out on another thread, on its own copy
    // of the board, while the turn's notice shows.
    public class Decision
    {
        private Task<AiAction> thinking;
        private readonly Func<AiAction> fallback;
        private AiAction action;

        public Decision(AiAction decided)
        {
            action = decided;
        }

        public Decision(Task<AiAction> task, Func<AiAction> if_it_fails)
        {
            thinking = task;
            fallback = if_it_fails;
        }

        public bool Done { get { return thinking == null || thinking.IsCompleted; } }

        // Once Done
        public AiAction Action
        {
            get
            {
                if (thinking != null)
                {
                    if (thinking.Status == TaskStatus.RanToCompletion)
                        action = thinking.Result;
                    else
                    {
                        Debug.LogError("AI decision failed, playing as on Normal: " + thinking.Exception);
                        action = fallback();
                    }
                    thinking = null;
                }
                return action;
            }
        }
    }

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
    // (Easy keeps the hand it was dealt, as a beginner would)
    public void AIExchangeCards()
    {
        PlayerState ai = controller.EnemyInfo;
        CardDatabase db = controller.Db;
        ai.CardsExchanged = 0;
        if (GameSettings.AiDifficulty == AiDifficulty.Easy)
        {
            ai.CanExchange = false;
            GameLog.Info("AI: Keeps its hand (Easy).");
            return;
        }

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

    // The AI's turn: exactly one action, decided (the battle shows it), then carried out; the turn then passes
    public Decision AIDecide()
    {
        if (controller == null)
            AIInitialize();
        AiContext ctx = BuildContext();
        if (ctx.Difficulty == AiDifficulty.Hard && Application.platform != RuntimePlatform.WebGLPlayer)
        {
            AiContext copy = Snapshot(ctx);
            return new Decision(Task.Run(() => AiBrain.Decide(copy)), () =>
            {
                AiContext normal = BuildContext();
                normal.Difficulty = AiDifficulty.Normal;
                return Decide(normal);
            });
        }
        return new Decision(Decide(ctx));
    }

    private static AiAction Decide(AiContext ctx)
    {
        try
        {
            return AiBrain.Decide(ctx);
        }
        catch (Exception e)
        {
            Debug.LogError("AI decision failed, passing: " + e);
            return AiAction.Pass();
        }
    }

    public void AIExecute(AiAction action)
    {
        if (controller == null)
            AIInitialize();
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
            Difficulty = GameSettings.AiDifficulty,
            Seed = Rng.Shared.Next(),
        };
    }

    // The board as it is now, for a decision made on another thread while the battle goes on
    private static AiContext Snapshot(AiContext ctx)
    {
        return new AiContext
        {
            Me = ctx.Me.Clone(),
            Opponent = ctx.Opponent.Clone(),
            Weather = new List<int>(ctx.Weather),
            DoubleSpies = ctx.DoubleSpies,
            RandomMedic = ctx.RandomMedic,
            Db = ctx.Db,
            Difficulty = ctx.Difficulty,
            Seed = ctx.Seed,
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
