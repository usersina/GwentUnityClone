using System;
using System.Collections.Generic;

namespace Gwent.Rules
{
    // Both sides plus the shared weather board. The lists are shared with the battle, so changes apply directly.
    public class Board
    {
        public PlayerState Player;
        public PlayerState Enemy;
        public List<int> Weather = new List<int>();
        public bool DoubleSpies;
        public bool RandomMedic;
        public CardDatabase Db;
        public Random Rnd = Rng.Shared;

        public PlayerState Side(bool is_player) { return is_player ? Player : Enemy; }
        public PlayerState Opponent(bool is_player) { return is_player ? Enemy : Player; }

        public void Recalculate()
        {
            StrengthCalculator.Recalculate(Player, Weather, DoubleSpies, Db);
            StrengthCalculator.Recalculate(Enemy, Weather, DoubleSpies, Db);
        }

        public AiContext AiContextFor(bool is_player)
        {
            return new AiContext { Me = Side(is_player), Opponent = Opponent(is_player), Weather = Weather, DoubleSpies = DoubleSpies, RandomMedic = RandomMedic, Db = Db };
        }
    }
}
