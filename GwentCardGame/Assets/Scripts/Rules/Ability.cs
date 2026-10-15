namespace Gwent.Rules
{
    // A card's ability. UnitHorn is Dandelion's horn on his own row (the database's "commander_horn"); CommanderHorn
    // is the special card ("double_strength"). The weather cards reduce a row (Skellige Storm two); a row scorch destroys
    // the strongest units of the enemy's row when they total 10 or more. Scorch is the special card's, and the Clan
    // Dimun Pirate's as he lands. A Berserker turns into a bear on a row with a Mardroeme (the special card, or Ermion).
    public enum Ability
    {
        None, Leader, Spy, Medic, Muster, MoraleBoost, TightBond, UnitHorn, CommanderHorn, ClearWeather,
        ReduceClose, ReduceRange, ReduceSiege, Decoy, Scorch, ScorchClose, ScorchRange, ScorchSiege,
        ReduceRangeSiege, Berserker, Mardroeme,
    }
}
