namespace Gwent.Rules
{
    public enum PlayOutcome { Done, WaitingForMedicChoice }

    // Resolving a card once it has left the hand
    public static class PlayRules
    {
        // manual_medic: the side picks its medic target itself (the human player), otherwise the AI picks.
        // transform: Berserkers meeting a Mardroeme turn now; the battle turns them after showing the play
        // (AbilityRules.Transform), which changes nothing else: what a play scorches is decided before they meet.
        public static PlayOutcome Resolve(Board board, Card card, Row field_row, bool is_player, bool manual_medic, bool transform = true)
        {
            PlayerState me = board.Side(is_player);
            PlayerState opp = board.Opponent(is_player);

            if (card.IsUnit)
                return ResolveUnit(board, card, field_row, is_player, manual_medic, transform);

            switch (card.row)
            {
                case Row.Special: // Commander's Horn or Mardroeme in the chosen row's slot (one card per slot)
                    Row row = Rows.IsUnitRow(field_row) ? field_row : Row.Close;
                    if (me.SlotTaken(row))
                        me.DiscardList.Add(card._id);
                    else
                    {
                        me.SpecialRow(row).Add(card._id);
                        if (transform)
                            AbilityRules.Transform(me, board.Db); // a Mardroeme: the row's Berserkers turn
                    }
                    break;
                case Row.Weather:
                    AbilityRules.PlayWeather(me, opp, board.Weather, card);
                    break;
                default: // one_time: scorch (decoy goes through PlayDecoy)
                    me.DiscardList.Add(card._id);
                    if (card.ability == Ability.Scorch)
                    {
                        board.Recalculate(); // Scorch AFTER modifiers
                        AbilityRules.Scorch(board.Player, board.Enemy, board.Db);
                    }
                    break;
            }
            board.Recalculate();
            return PlayOutcome.Done;
        }

        private static PlayOutcome ResolveUnit(Board board, Card card, Row field_row, bool is_player, bool manual_medic, bool transform)
        {
            PlayerState me = board.Side(is_player);
            PlayerState opp = board.Opponent(is_player);
            Row row = card.row == Row.Agile ? (Rows.IsUnitRow(field_row) ? field_row : Row.Close) : card.row;
            if (!Rows.IsUnitRow(row))
                row = Row.Close;

            PlayerState owner = me;
            int revived = -1;
            PlayOutcome outcome = PlayOutcome.Done;

            if (card.ability == Ability.Spy)
            {
                // The spy user draws two cards, the spy goes to the opponent's side
                RoundRules.Draw(me, 2);
                owner = opp;
            }
            else if (card.ability == Ability.Muster)
                AbilityRules.Muster(me, card, row, board.Db);
            else if (card.ability == Ability.Medic)
            {
                if (board.RandomMedic && !card.unique) // Emhyr, Invader of the North (hero medics still choose)
                    revived = AbilityRules.TakeRandomMedicTarget(me, board.Rnd, board.Db);
                else if (manual_medic)
                {
                    if (AbilityRules.MedicCandidates(me.DiscardList, board.Db).Count > 0)
                        outcome = PlayOutcome.WaitingForMedicChoice;
                }
                else
                {
                    revived = AiBrain.ChooseMedicTarget(me, board.Db);
                    if (revived != -1)
                        me.DiscardList.Remove(revived);
                }
            }

            // The Clan Dimun Pirate scorches the battlefield as he lands (the strongest units but himself)
            if (card.ability == Ability.Scorch)
            {
                board.Recalculate();
                AbilityRules.Scorch(board.Player, board.Enemy, board.Db);
            }

            owner.UnitRow(row).Add(card._id);
            if (transform)
                AbilityRules.Transform(owner, board.Db); // a Berserker on a Mardroeme's row, or Ermion on theirs
            board.Recalculate();

            Row? scorch_row = Abilities.RowScorchTarget(card.ability);
            if (scorch_row != null)
            {
                AbilityRules.ScorchRow(opp, scorch_row.Value, board.Db);
                board.Recalculate();
            }

            // A restored unit is played like any other (spies draw, musters muster, medics chain)
            if (revived != -1)
            {
                Card revived_card = board.Db.Get(revived);
                Row revived_row = revived_card.row == Row.Agile
                    ? AbilityRules.BestAgileRow(me, revived, board.Weather, board.DoubleSpies, board.Db)
                    : revived_card.row;
                return ResolveUnit(board, revived_card, revived_row, is_player, manual_medic, transform);
            }
            return outcome;
        }

        // Decoy: the target goes back to the hand and the decoy takes its place (decoy already out of the hand)
        public static void Decoy(Board board, int target_id, Row row, bool is_player, int decoy_id)
        {
            PlayerState info = board.Side(is_player);
            if (Rows.IsUnitRow(row) && info.UnitRow(row).Remove(target_id))
            {
                info.HandList.Add(target_id);
                info.UnitRow(row).Add(decoy_id);
            }
            else
                info.DiscardList.Add(decoy_id);
            board.Recalculate();
        }
    }
}
