namespace Gwent.Rules
{
    // A card's or a deck's faction. Special cards (weather, horns, decoys, scorch) are a faction of their own here, as
    // in the card database. Skellige came last (Blood and Wine): the values are saved as numbers in the scenes.
    public enum Faction { NorthernRealms, Nilfgaard, Scoiatael, Monsters, Neutral, Special, Skellige }
}
