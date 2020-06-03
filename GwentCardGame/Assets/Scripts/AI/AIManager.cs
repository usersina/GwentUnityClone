using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SceneController;

public class AIManager : MonoBehaviour
{
    public GameObject tempObject;
    public GameObject cardPrefab;

    private SceneController controller;
    private GameObject field;

    public void AIInitialize()
    {
        Debug.Log("AI: Starting...");
        // Initialize Controller
        controller = GetComponent<SceneController>();
        field = GetComponent<SceneController>().EnemyField;
        if (controller != null || field != null)
        {

            Debug.Log("AI: Started successfully.");
        }
        else
            Debug.LogError("Could not initialize AI, field or scene controller not found!");
    }

    //----------------------------------------------AI Phases------------------------------------------------------------//
    // Initial 2 cards re-draw (Works perfectly <3)
    public void AIExchangeCards()
    {
        Debug.Log("AI: Exchaning Cards...");
        //controller.LogList(controller.EnemyInfo.HandList, "AI_Hand List");

        controller.EnemyInfo.CardsExchanged = 0;

        IEnumerable<int> duplicates_enum = controller.EnemyInfo.HandList.GroupBy(x => x).SelectMany(g => g.Skip(1));
        List<int> duplicates = duplicates_enum.ToList();

        //controller.LogList(duplicates, "Duplicates");

        for (int i = 0; i < 2; i++)
        {
            // Exchange Impenetrable Fog Card
            while (controller.EnemyInfo.HandList.Contains(204))
            {
                if (controller.EnemyInfo.CardsExchanged < 2)
                {
                    controller.ExchangeCard(204, false);
                    //Debug.Log("Exchaning 204");
                    controller.EnemyInfo.CardsExchanged++;
                }
                else
                    break;
            }

            // Exchange from duplicates
            foreach (int card in duplicates)
            {
                if (controller.EnemyInfo.CardsExchanged < 2)
                {
                    controller.ExchangeCard(card, false);
                    controller.EnemyInfo.CardsExchanged++;
                }
            }

            // Exchange same name (muster related)
            for (int j = 0; j < controller.EnemyInfo.HandList.Count; j++)
            {
                int card = controller.EnemyInfo.HandList[j];
                if (controller.EnemyInfo.CardsExchanged < 2)
                {
                    Card my_card = controller.GetCardStats(card);
                    string card_name = controller.TrimToChar(my_card._idstr, '_');

                    List<Card> other_cards = controller.GetCardsList(controller.EnemyInfo.HandList);

                    foreach (Card other_card in other_cards)
                    {
                        if (other_card._idstr.Contains(card_name) && other_card._id != card)
                        {
                            controller.ExchangeCard(card, false);
                            //Debug.Log("Changed CardID: " + card);
                            controller.EnemyInfo.CardsExchanged++;
                            j--;
                            break;
                        }
                    }
                }
            }
        }

        //Debug.Log("Exchanged card: " + controller.EnemyInfo.CardsExchanged);
        //controller.LogList(controller.EnemyInfo.HandList, "AI_Hand List");
        controller.EnemyInfo.CanExchange = false;

        Debug.Log("AI: Finished Exchaning Cards.");
    }

    // The AI's turn
    public void AIStartTurn()
    {
        // NEEDS TO PLACE CARD OR SKIP TURN ELSE PLAYER WON'T PLAY

        Debug.Log("AI: Starting turn...");
        List<Card> hand_cards = controller.GetCardsList(controller.EnemyInfo.HandList); // List of all cards in hand
        List<Card> hand_units = controller.GetCardsList(GetHandUnits());                // List of all unit cards in hand (except dandelion)
        List<Card> hand_specials = controller.GetCardsList(GetHandSpecials());          // List of special cards in hand

        Card card_to_play;
        string card_to_play_row;

        // Get Player Scores
        int pl_all_score = controller.GetTotalScore(true, "all");
        int pl_close_score = controller.GetTotalScore(true, "close");
        int pl_range_score = controller.GetTotalScore(true, "range");
        int pl_siege_score = controller.GetTotalScore(true, "siege");

        // Get AI Scores
        int ai_all_score = controller.GetTotalScore(false, "all");
        int ai_close_score = controller.GetTotalScore(false, "close");
        int ai_range_score = controller.GetTotalScore(false, "range");
        int ai_siege_score = controller.GetTotalScore(false, "siege");

        // Get Weather Modifiers
        bool reduce_close = SearchWeather("close");
        bool reduce_range = SearchWeather("range");
        bool reduce_siege = SearchWeather("siege");

        // Get AI Doublers
        bool ai_close_double = SearchDouble("close");
        bool ai_range_double = SearchDouble("range");
        bool ai_siege_double = SearchDouble("siege");

        // Get AI Morale Boosters
        bool ai_close_morale = SearchMorale("close");
        bool ai_range_morale = SearchMorale("range");
        bool ai_siege_morale = SearchMorale("siege");

        // DEVONLY:
        //PlaceCard(126, "close");

        int needed_score;
        List<Card> eligible_cards = new List<Card>();

        // If AI has cards !!
        if (hand_units.Count > 0)
        {
            if (controller.PlayerInfo.hasPassed || controller.PlayerInfo.HandList.Count == 0)
            {
                // Place until total score surpassed or pass
                switch (controller.EnemyInfo.Lives)
                {
                    case 1:
                        // All-Out
                        needed_score = pl_all_score - ai_all_score;
                        Debug.Log("Needed Score Case 1_1: " + needed_score);
                        Debug.Log("AI: Passed player, gotta surpass him else I'll lose");
                        if (needed_score < 0) // Enemy already surpasses, pass turn
                        {
                            controller.SkipTurn(false);
                            return;
                        }
                        else
                        {
                            if (reduce_close)
                            {
                                if (controller.EnemyInfo.HandList.Contains(201)) // Clear_weather
                                {
                                    PlaceCard(201, "weather");
                                    return;
                                }
                            }

                            if (pl_range_score >= 10)
                            {
                                if (controller.EnemyInfo.canLeader && controller.PlayerInfo.RangeList.Count > 3)
                                {
                                    PlayLeader();
                                    return;
                                }
                            }

                            if (ai_close_score >= 5 && !ai_close_double)
                            {
                                if (controller.EnemyInfo.HandList.Contains(202))     // Commander Horn
                                {
                                    PlaceCard(202, "sp_close");
                                    return;
                                }
                                else if (controller.EnemyInfo.HandList.Contains(22)) // Dandelion
                                {
                                    PlaceCard(22, "close");
                                    return;
                                }
                            }

                            eligible_cards.Clear();
                            foreach (Card card in hand_units)
                            {
                                if (card.strength > needed_score)
                                    eligible_cards.Add(card);
                            }
                            if (eligible_cards.Count < 1) // No card higher than the needed score, summon the highest
                            {
                                // From hand_unit
                                Debug.Log("Play highest strength card");
                                card_to_play = GetMaximumStrength(hand_units);
                                card_to_play_row = GetCardRow(card_to_play);
                                PlaceCard(card_to_play._id, card_to_play_row);
                                return;
                            }
                            else
                            {
                                // Find Minimum from eligible cards
                                card_to_play = GetMinimumStrength(eligible_cards);
                                card_to_play_row = GetCardRow(card_to_play);
                                PlaceCard(card_to_play._id, card_to_play_row);
                                return;
                            }
                        }
                    case 2:
                        // Passive
                        needed_score = pl_all_score - ai_all_score;
                        Debug.Log("Needed Score Case 1_2: " + needed_score);
                        if (needed_score < 0) // Enemy already surpasses, pass turn
                        {
                            controller.SkipTurn(false);
                            return;
                        }
                        else // needed_score < 0
                        {
                            // Play strongest
                            card_to_play = GetMaximumStrength(hand_units);
                            card_to_play_row = GetCardRow(card_to_play);
                            PlaceCard(card_to_play._id, card_to_play_row);
                            return;
                        }
                }
                return; // Unneeded
            }
            // Player still playing
            else
            {
                switch (controller.EnemyInfo.Lives)
                {
                    case 1:
                        // All-Out
                        needed_score = pl_all_score - ai_all_score;
                        Debug.Log("Needed Score Case 2_1: " + needed_score);
                        Debug.Log("AI: NEED TO PLACE CARDS AAAAA");
                        if (needed_score <= -40) // AI already surpasses, pass turn
                        {
                            controller.SkipTurn(false);
                            return;
                        }
                        else
                        {
                            if (reduce_close)
                            {
                                if (controller.EnemyInfo.HandList.Contains(201)) // Clear_weather
                                {
                                    PlaceCard(201, "weather");
                                    return;
                                }
                            }

                            if (pl_range_score >= 10)
                            {
                                if (controller.EnemyInfo.canLeader && controller.PlayerInfo.RangeList.Count > 3)
                                {
                                    PlayLeader();
                                    return;
                                }
                            }

                            if (ai_close_score >= 5 && !ai_close_double)
                            {
                                if (controller.EnemyInfo.HandList.Contains(202))     // Commander Horn
                                {
                                    PlaceCard(202, "sp_close");
                                    return;
                                }
                                else if (controller.EnemyInfo.HandList.Contains(22)) // Dandelion
                                {
                                    PlaceCard(22, "close");
                                    return;
                                }
                            }

                            eligible_cards.Clear();
                            foreach (Card card in hand_units)
                            {
                                if (card.strength > needed_score)
                                    eligible_cards.Add(card);
                            }
                            if (eligible_cards.Count < 1) // No card higher than the needed score, summon the highest
                            {
                                // From hand_unit
                                Debug.Log("Play highest strength card");
                                card_to_play = GetMaximumStrength(hand_units);
                                card_to_play_row = GetCardRow(card_to_play);
                                PlaceCard(card_to_play._id, card_to_play_row);
                                return;
                            }
                            else
                            {
                                // Find Minimum from eligible cards
                                card_to_play = GetMinimumStrength(eligible_cards);
                                card_to_play_row = GetCardRow(card_to_play);
                                PlaceCard(card_to_play._id, card_to_play_row);
                                return;
                            }
                        }
                    case 2:
                        // Passive
                        needed_score = pl_all_score - ai_all_score;
                        Debug.Log("Needed Score Case 2_2: " + needed_score);
                        Debug.Log("AI: Chilling...");
                        if (needed_score < -25 || needed_score > 30) // AI surpasses by 25 or player surpasses by 30, pass turn
                        {
                            controller.SkipTurn(false);
                            return;
                        }
                        else // needed_score in [25, 30]
                        {
                            if (needed_score < 0)
                            {// Player Surpasses
                                if (hand_units.Count >= 5)
                                {
                                    // Play strongest
                                    card_to_play = GetMaximumStrength(hand_units);
                                    card_to_play_row = GetCardRow(card_to_play);
                                    PlaceCard(card_to_play._id, card_to_play_row);
                                }
                                else
                                {
                                    controller.SkipTurn(false);
                                    return;
                                }
                                return;
                            }
                            else // AI Surpasses
                            {
                                if (hand_units.Count >= 5)
                                {
                                    // Play weakest
                                    card_to_play = GetMinimumStrength(hand_units);
                                    card_to_play_row = GetCardRow(card_to_play);
                                    PlaceCard(card_to_play._id, card_to_play_row);
                                    return;
                                }
                                else
                                {
                                    controller.SkipTurn(false);
                                    return;
                                }
                            }
                        }
                }
            }
        }
        else if (hand_cards.Count > 0) // Has cards in hand, but only specials
        {
            card_to_play = GetRandomCard(hand_cards);
            card_to_play_row = GetCardRow(card_to_play);
            PlaceCard(card_to_play._id, card_to_play_row);
            return;
        }
        else if (controller.EnemyInfo.canLeader && controller.EnemyInfo.DeckList.Contains(204)) 
        {
            PlayLeader();
            return;
        }
        else // No cards in hand, and no leader conclude the round
        {
            controller.SkipTurn(false);
            return;
        }
    }

    //-----------------------------------------------------Card Placing-------------------------------------------------//
    // Place the card
    private void PlaceCard(int cardId, string card_row)
    {
        Card card = controller.GetCardStats(cardId);
        GameObject instantiatedCard = Instantiate(cardPrefab);
        instantiatedCard.name = card._id.ToString();
        instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().name = card.name;
        instantiatedCard.transform.Find("Stats").GetComponent<CardStats>()._id = card._id;
        instantiatedCard.transform.Find("Stats").GetComponent<CardStats>()._idstr = card._idstr;
        instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().faction = card.faction;
        instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().unique = card.unique;
        instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().strength = card.strength;
        instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().row = card.row;
        instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().ability = card.ability;
        instantiatedCard.tag = "Enemy";

        GameObject cardRowGO = GetCardRowGO(card_row);
        instantiatedCard.transform.SetParent(tempObject.transform, false);

        controller.DirectlyPlaceCard(instantiatedCard, cardRowGO, false);
    }

    // Gets the card row GO
    private GameObject GetCardRowGO(string row)
    {
        switch (row)
        {
            // Unit Cards
            case "close":
                return field.transform.Find("Close").Find("Unit").gameObject;
            case "range":
                return field.transform.Find("Range").Find("Unit").gameObject;
            case "siege":
                return field.transform.Find("Siege").Find("Unit").gameObject;
            // Special Cards
            case "sp_close":
                return field.transform.Find("Close").Find("Special").gameObject;
            case "sp_range":
                return field.transform.Find("Range").Find("Special").gameObject;
            case "sp_siege":
                return field.transform.Find("Siege").Find("Special").gameObject;

            case "weather":
                return controller.WeatherField;

            // Fix
            default:
                Debug.LogError("GetCardRowGO: Unexpected Error!");
                return null;
        }
    }

    private string GetCardRow(Card card)
    {
        switch (card.row)
        {
            case "close":
                return card.row;
            case "range":
                return card.row;
            case "siege":
                return card.row;
            case "close_range":
                return "close";

            case "weather":
                return "weather";
            case "one_time":
                return "weather";
            case "special":
                return "sp_close";

            default:
                Debug.LogError("GetCardRow: Unexpected Error ! Cannot Find valid Row");
                return null;
        }
    }

    // Static, don't modify deck
    private void PlayLeader()
    {
        if (controller.EnemyInfo.DeckList.Contains(204))  // reduce_range
        {
            controller.EnemyInfo.DeckList.Remove(204);
            field.transform.Find("Leader").GetComponent<LeaderManager>().DisableButtonRC();
            controller.EnemyInfo.canLeader = false;
            PlaceCard(204, "weather");
        }
    }

    //-----------------------------------------------------------------------------------------------------------------//
    // Search Weather Modifiers
    private bool SearchWeather(string row)
    {
        bool reduced = false;
        switch (row)
        {
            case "close": // Biting Frost
                if (controller.weatherList.Contains(200))
                    reduced = true;
                break;
            case "range": // Impenetrable Fog
                if (controller.weatherList.Contains(204))
                    reduced = true;
                break;
            case "siege": // Torrential Rain
                if (controller.weatherList.Contains(206))
                    reduced = true;
                break;
        }
        return reduced;
    }

    // Search Commander_Horn (double units in row)
    private bool SearchDouble(string row)
    {
        bool doubled = false;
        switch (row)
        {
            case "close": // Commander horn or dandelion
                if (controller.EnemyInfo.SpCloseList.Contains(202) || controller.EnemyInfo.CloseList.Contains(22))
                    doubled = true;
                break;
            case "range":
                if (controller.EnemyInfo.SpRangeList.Contains(202))
                    doubled = true;
                break;
            case "siege":
                if (controller.EnemyInfo.SpSiegeList.Contains(202))
                    doubled = true;
                break;
        }
        return doubled;
    }

    // Search Morale_Boost (+1 to all units in row)
    private bool SearchMorale(string row)
    {
        bool moraled = false;
        List<Card> cards_list;
        switch (row)
        {
            case "close":
                cards_list = controller.GetCardsList(controller.EnemyInfo.CloseList);
                foreach (Card card in cards_list)
                    if (card.ability == "morale_boost")
                    {
                        moraled = true;
                        break;
                    }
                break;
            case "range":
                cards_list = controller.GetCardsList(controller.EnemyInfo.RangeList);
                foreach (Card card in cards_list)
                    if (card.ability == "morale_boost")
                    {
                        moraled = true;
                        break;
                    }
                break;
            case "siege":
                cards_list = controller.GetCardsList(controller.EnemyInfo.SiegeList);
                foreach (Card card in cards_list)
                    if (card.ability == "morale_boost")
                    {
                        moraled = true;
                        break;
                    }
                break;
        }
        return moraled;
    }

    // Get a list of unit card ids in hand (except dandelion)
    private List<int> GetHandUnits()
    {
        List<int> returned_list = new List<int>();
        List<Card> cards_list = controller.GetCardsList(controller.EnemyInfo.HandList);
        foreach (Card card in cards_list)
        {
            if (card.faction != "Special" && card.ability != "commander_horn")
            {
                returned_list.Add(card._id);
            }
        }
        return returned_list;
    }

    // Get a list of special card ids in hand (except specials)
    private List<int> GetHandSpecials()
    {
        List<int> returned_list = new List<int>();
        List<Card> cards_list = controller.GetCardsList(controller.EnemyInfo.HandList);
        foreach (Card card in cards_list)
        {
            if (card.faction == "Special")
            {
                returned_list.Add(card._id);
            }
        }
        return returned_list;
    }

    //----------------------------------------------Card List Manipulation
    private Card GetMinimumStrength(List<Card> cards_list)
    {
        Card card = cards_list[0];
        for (int i = 1; i < cards_list.Count; i++)
        {
            if (cards_list[i].strength <= card.strength)
                card = cards_list[i];
        }
        return card;
    }

    private Card GetMaximumStrength(List<Card> cards_list)
    {
        Card card = cards_list[0];
        for (int i = 1; i < cards_list.Count; i++)
        {
            if (cards_list[i].strength >= card.strength)
                card = cards_list[i];
        }
        return card;
    }

    private Card GetRandomCard(List<Card> cards_list)
    {
        System.Random rnd = new System.Random();
        int r = rnd.Next(cards_list.Count);
        return cards_list[r];
    }

}

