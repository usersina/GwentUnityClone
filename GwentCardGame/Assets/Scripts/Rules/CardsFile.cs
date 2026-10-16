using System;
using System.Collections.Generic;

namespace Gwent.Rules
{
    // Every card: the root object of Resources/Cards/cards_db.json
    [Serializable]
    public class CardsFile
    {
        public List<CardEntry> Cards = new List<CardEntry>();
    }
}
