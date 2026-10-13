using System.Collections.Generic;
using System.Linq;
using Gwent.Rules;
using UnityEngine;
using UnityEngine.UI;

// Draws the battle from the model: the card objects in the hands, rows, horn slots, discard piles and weather slot,
// row highlights, weather overlays, scores and the panels' markers. SceneController runs the battle and asks it
// to redraw; this class never changes the model.
public class BoardView
{
    private const string WeatherOverlayName = "WeatherOverlay";

    //----------------------------------------Cached scene references----------------------------------------//
    // Looked up once instead of transform.Find chains on every refresh / every frame
    public class RowView
    {
        public RowManager Manager;
        public GameObject Unit;
        public GameObject Special;
        public Image UnitImage;
        public Image SpecialImage;
    }

    public class FieldView
    {
        public GameObject Root;
        public RowPicker Picker;
        public HandManager Hand;
        public DeckManager Deck;
        public PileManager Discard;
        public GameObject DiscardPile;
        public StatsManager Stats;
        public LeaderManager Leader;
        public GameObject LeaderHolder;
        public GameObject LeadMarker;   // the laurel around the total of the side that is ahead (as in The Witcher 3)
        public GameObject PassedLabel;  // "Passed" under the panel of a side that has passed
        public readonly Dictionary<string, RowView> Rows = new Dictionary<string, RowView>();

        public FieldView(GameObject root)
        {
            Root = root;
            Transform t = root.transform;
            Picker = root.GetComponent<RowPicker>();
            Hand = t.Find("Hand").GetComponent<HandManager>();
            Deck = t.Find("Deck").GetComponent<DeckManager>();
            Discard = t.Find("Discard").GetComponent<PileManager>();
            DiscardPile = t.Find("Discard").Find("Pile").gameObject;
            Stats = t.Find("Stats").GetComponent<StatsManager>();
            Leader = t.Find("Leader").GetComponent<LeaderManager>();
            LeaderHolder = t.Find("Leader").Find("Holder").gameObject;
            LeadMarker = t.Find("Stats").Find("Total").Find("Laurel").gameObject;
            PassedLabel = t.Find("Stats").Find("Passed").gameObject;
            foreach (string row in Gwent.Rules.Rows.Units)
            {
                Transform row_t = t.Find(char.ToUpper(row[0]) + row.Substring(1));
                Rows[row] = new RowView
                {
                    Manager = row_t.GetComponent<RowManager>(),
                    Unit = row_t.Find("Unit").gameObject,
                    Special = row_t.Find("Special").gameObject,
                    UnitImage = row_t.Find("Unit").GetComponent<Image>(),
                    SpecialImage = row_t.Find("Special").GetComponent<Image>(),
                };
            }
        }
    }

    private readonly SceneController controller;
    private readonly FieldView playerView;
    private readonly FieldView enemyView;

    public WeatherManager Weather { get; private set; }
    private readonly Image weatherImage;

    public BoardView(SceneController battle)
    {
        controller = battle;
        playerView = new FieldView(battle.PlayerField);
        enemyView = new FieldView(battle.EnemyField);
        // A click on either discard pile shows it card by card
        foreach (bool is_player in new[] { true, false })
            View(is_player).DiscardPile.transform.parent.gameObject.AddComponent<DiscardPileBrowser>().isPlayer = is_player;
        Weather = battle.WeatherField.GetComponent<WeatherManager>();
        weatherImage = battle.WeatherField.GetComponent<Image>();
    }

    public FieldView View(bool is_player) { return is_player ? playerView : enemyView; }
    private SceneController.MainInfo Info(bool is_player) { return is_player ? controller.PlayerInfo : controller.EnemyInfo; }

    //----------------------------------------Whole board----------------------------------------//
    // Shows both sides and the weather from the lists, then lights off the rows and updates the scores
    public void Refresh()
    {
        RefreshField(true);
        RefreshField(false);
        SyncCards(controller.WeatherField, controller.weatherList, true);
        Lightoff(true);
        Lightoff(false);
        UpdateScores(true);
        UpdateScores(false);
    }

    // The laurel on the side that is ahead, "Passed" on a side that has passed (every frame)
    public void UpdateMarkers()
    {
        int player_total = controller.PlayerInfo.TotalScore(), enemy_total = controller.EnemyInfo.TotalScore();
        SetActiveIfChanged(playerView.LeadMarker, player_total > enemy_total);
        SetActiveIfChanged(enemyView.LeadMarker, enemy_total > player_total);
        SetActiveIfChanged(playerView.PassedLabel, controller.PlayerInfo.hasPassed);
        SetActiveIfChanged(enemyView.PassedLabel, controller.EnemyInfo.hasPassed);
    }

    private static void SetActiveIfChanged(GameObject go, bool active)
    {
        if (go.activeSelf != active)
            go.SetActive(active);
    }

    private void RefreshField(bool is_player_field)
    {
        SceneController.MainInfo info = Info(is_player_field);
        FieldView view = View(is_player_field);

        if (ShowsCards(view.Hand.transform, info.HandList))
            view.Stats.UIUpdateHandCount(info.HandList.Count);
        else
            SetHand(info.HandList, is_player_field);

        foreach (string row in Rows.Units)
        {
            RowView row_view = view.Rows[row];
            List<int> units = info.UnitRow(row);
            if (SyncCards(row_view.Unit, units, is_player_field))
                row_view.Manager.ResizeUnitObject(units.Count);
            SyncCards(row_view.Special, info.SpecialRow(row), is_player_field);
        }

        SetDiscard(info.DiscardList, is_player_field);
        RefreshWeatherOverlays(view);
    }

    //----------------------------------------Piles and hand----------------------------------------//
    // Updates the deck count and the stack of card backs
    public void SetDeck(SceneController.MainInfo info, bool is_player_deck)
    {
        View(is_player_deck).Deck.UIUpdateDeckBase(info.DeckBSprite, info.DeckList.Count);
    }

    // Rebuilds the hand's card objects and updates the card count
    public void SetHand(List<int> hand_list, bool is_player_hand)
    {
        FieldView view = View(is_player_hand);
        view.Hand.ClearHandObject();
        CreateCardObjects(view.Hand.gameObject, hand_list, true, is_player_hand);
        view.Hand.ResizeHand();
        view.Stats.UIUpdateHandCount(hand_list.Count);
    }

    public void SetDiscard(List<int> discard_list, bool is_player_hand)
    {
        FieldView view = View(is_player_hand);
        if (!SyncCards(view.DiscardPile, discard_list, is_player_hand))
            return;

        // A visible stack, as in The Witcher 3: older cards peek out to the bottom right of the top one
        Transform pile = view.DiscardPile.transform;
        int top = pile.childCount - 1;
        for (int i = 0; i < pile.childCount; i++)
        {
            float depth = Mathf.Min(top - i, 12) * 0.7f;
            pile.GetChild(i).localPosition = new Vector3(depth, -depth, 0);
        }
    }

    //----------------------------------------Card objects----------------------------------------//
    // True when a container already shows exactly these cards, in this order (card objects are named after their
    // id; other children such as the weather overlay are ignored). Refreshes skip such containers: rebuilding the
    // whole board on every play cost ~30 ms in the editor on a busy board, several times that on a phone.
    private static bool ShowsCards(Transform container, List<int> card_ids)
    {
        int shown = 0;
        foreach (Transform child in container)
        {
            if (child.GetComponent<CardDisplay>() == null)
                continue;
            if (shown >= card_ids.Count || child.name != card_ids[shown].ToString())
                return false;
            shown++;
        }
        return shown == card_ids.Count;
    }

    // Makes a container show exactly these cards, in this order, reusing the card objects already there: a play
    // only creates the cards that arrived and removes the ones that left. For containers without per-card state
    // (rows, horn slots, the discard pile, the weather slot); other children (the weather overlay) stay after them.
    // Returns false when nothing changed.
    private bool SyncCards(GameObject container, List<int> card_ids, bool is_player_card)
    {
        Transform parent = container.transform;
        if (ShowsCards(parent, card_ids))
            return false;

        Dictionary<int, Queue<GameObject>> existing = new Dictionary<int, Queue<GameObject>>();
        List<GameObject> unused = new List<GameObject>();
        foreach (Transform child in parent)
        {
            if (child.GetComponent<CardDisplay>() == null)
                continue;
            int id;
            if (!int.TryParse(child.name, out id))
            {
                unused.Add(child.gameObject);
                continue;
            }
            Queue<GameObject> queue;
            if (!existing.TryGetValue(id, out queue))
                existing[id] = queue = new Queue<GameObject>();
            queue.Enqueue(child.gameObject);
        }

        List<GameObject> ordered = new List<GameObject>(card_ids.Count);
        foreach (int id in card_ids)
        {
            Queue<GameObject> queue;
            GameObject card;
            if (existing.TryGetValue(id, out queue) && queue.Count > 0)
                card = queue.Dequeue();
            else
            {
                card = CreateCardObject(controller.Db.Get(id), is_player_card);
                card.GetComponent<CardSelect>().isSelectable = false;
                card.GetComponent<CardHover>().isHoverable = false;
                card.GetComponent<CardExchange>().isExchangeable = false;
                card.transform.SetParent(parent, false);
            }
            ordered.Add(card);
        }
        foreach (Queue<GameObject> queue in existing.Values)
            unused.AddRange(queue);
        foreach (GameObject card in unused)
        {
            card.transform.SetParent(null, false); // out of the container now: counts and layouts must not see it
            Object.Destroy(card);
        }
        for (int i = 0; i < ordered.Count; i++)
            ordered[i].transform.SetSiblingIndex(i);
        return true;
    }

    // Generate Cards GameObject from cards prefab and set their parent to the passed gameobject
    public void CreateCardObjects(GameObject my_object, List<int> card_list, bool isSelectable, bool is_player_card)
    {
        foreach (int card_id in card_list)
        {
            GameObject instantiatedCard = CreateCardObject(controller.Db.Get(card_id), is_player_card);
            instantiatedCard.GetComponent<CardSelect>().isSelectable = isSelectable;
            instantiatedCard.GetComponent<CardHover>().isHoverable = isSelectable;
            instantiatedCard.GetComponent<CardExchange>().isExchangeable = controller.Db.Get(card_id).ability != Abilities.Leader;
            instantiatedCard.transform.SetParent(my_object.transform, false);
        }
    }

    // A card object with its stats filled in (not parented)
    public GameObject CreateCardObject(Card card, bool is_player_card)
    {
        GameObject instantiatedCard = Object.Instantiate(controller.CardPrefab);
        instantiatedCard.name = card._id.ToString();
        CardStats stats = instantiatedCard.transform.Find("Stats").GetComponent<CardStats>();
        stats.name = card.name;
        stats._id = card._id;
        stats._idstr = card._idstr;
        stats.faction = card.faction;
        stats.unique = card.unique;
        stats.strength = card.strength;
        stats.row = card.row;
        stats.ability = card.ability;
        instantiatedCard.tag = is_player_card ? "Player" : "Enemy";
        return instantiatedCard;
    }

    //----------------------------------------Weather overlays and scores----------------------------------------//
    private void RefreshWeatherOverlays(FieldView view)
    {
        List<int> weather = controller.weatherList;
        SetWeatherOverlay(view.Rows[Rows.Close].Unit.transform, weather.Contains(CardIds.BitingFrost), "Cards/EffectBox/overlay_frost", new Color32(108, 198, 255, 78));
        SetWeatherOverlay(view.Rows[Rows.Range].Unit.transform, weather.Contains(CardIds.ImpenetrableFog), "Cards/EffectBox/overlay_fog", new Color32(185, 190, 188, 86));
        SetWeatherOverlay(view.Rows[Rows.Siege].Unit.transform, weather.Contains(CardIds.TorrentialRain), "Cards/EffectBox/overlay_rain", new Color32(61, 111, 184, 86));
    }

    private static void SetWeatherOverlay(Transform unitRow, bool isActive, string spriteResourcePath, Color fallbackColor)
    {
        Transform existingOverlay = unitRow.Find(WeatherOverlayName);
        if (!isActive)
        {
            if (existingOverlay != null)
                Object.Destroy(existingOverlay.gameObject);
            return;
        }

        GameObject overlayObject;
        if (existingOverlay == null)
        {
            overlayObject = new GameObject(WeatherOverlayName, typeof(RectTransform));
            overlayObject.transform.SetParent(unitRow, false);

            LayoutElement layoutElement = overlayObject.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            Image image = overlayObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = false;

            RectTransform rectTransform = overlayObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
        else
            overlayObject = existingOverlay.gameObject;

        overlayObject.transform.SetAsLastSibling();

        Image overlayImage = overlayObject.GetComponent<Image>();
        Sprite overlaySprite = SpriteCache.Load(spriteResourcePath);
        overlayImage.sprite = overlaySprite;
        overlayImage.color = overlaySprite != null ? new Color(1f, 1f, 1f, 0.82f) : fallbackColor;
    }

    private void UpdateScores(bool is_player_field)
    {
        SceneController.MainInfo info = Info(is_player_field);
        FieldView view = View(is_player_field);

        // New card objects get their strength labels next frame (coroutine)
        foreach (string row in Rows.Units)
        {
            RowManager manager = view.Rows[row].Manager;
            controller.StartCoroutine(manager.UIUpdateUnitsStrength(info.BaseStrengths(row), info.ModifiedStrengths(row)));
            manager.UpdateRowScore(info.ModifiedStrengths(row));
        }

        view.Stats.UIUpdateTotalScore(info.MCloseStrength, info.MRangeStrength, info.MSiegeStrength);
        SetDeck(info, is_player_field);
        view.Stats.UIUpdateHandCount(info.HandList.Count);
    }

    //----------------------------------------Highlighting----------------------------------------//
    // Highlights the rows where the selected card can go, and stores them in the RowPicker
    public void Highlight(CardStats card_stats, bool is_player_turn)
    {
        FieldView mine = View(is_player_turn);
        FieldView opp = View(!is_player_turn);

        mine.Picker.ResetStored();
        Weather.isWeatherCard = false;

        if (card_stats.faction == Factions.Special)
        {
            if (card_stats.row == Rows.Special)
            {
                // Commander's Horn: any special slot that has no horn yet (one per row)
                PlayerState side = is_player_turn ? (PlayerState)controller.PlayerInfo : controller.EnemyInfo;
                foreach (KeyValuePair<string, RowView> row in mine.Rows)
                {
                    if (side.HasHorn(row.Key))
                        continue;
                    row.Value.SpecialImage.sprite = mine.Picker.specialSelected;
                    mine.Picker.StoreField(row.Value.Special);
                }
            }
            else if (card_stats.ability == Abilities.Decoy)
            {
                // Decoy goes on a card, not a row: frame the units it can take back
                MarkDecoyTargets(mine, true);
            }
            else
            {
                // Weather and scorch go on the weather board
                weatherImage.sprite = Weather.weatherSelected;
                Weather.isWeatherCard = true;
            }
            return;
        }

        // Spies are played on the opponent's side
        FieldView target = card_stats.ability == Abilities.Spy ? opp : mine;
        target.Picker.ResetStored();
        string[] rows = card_stats.row == Rows.Agile ? new[] { Rows.Close, Rows.Range } : new[] { card_stats.row };
        foreach (string row in rows.Where(Rows.IsUnitRow))
        {
            target.Rows[row].UnitImage.sprite = SelectedSprite(target.Picker, row);
            target.Picker.StoreField(target.Rows[row].Unit);
        }
    }

    private static Sprite SelectedSprite(RowPicker picker, string row)
    {
        switch (row)
        {
            case Rows.Close: return picker.closeSelected;
            case Rows.Range: return picker.rangeSelected;
            default: return picker.siegeSelected;
        }
    }

    private static Sprite IdleSprite(RowPicker picker, string row)
    {
        switch (row)
        {
            case Rows.Close: return picker.close;
            case Rows.Range: return picker.range;
            default: return picker.siege;
        }
    }

    // Lights off any highlighted field
    public void Lightoff(bool is_player_turn)
    {
        FieldView view = View(is_player_turn);
        view.Picker.ResetStored();
        foreach (KeyValuePair<string, RowView> row in view.Rows)
        {
            row.Value.SpecialImage.sprite = view.Picker.special;
            row.Value.UnitImage.sprite = IdleSprite(view.Picker, row.Key);
        }

        // Light off the weather board and deselect weather card
        weatherImage.sprite = Weather.weather;
        Weather.isWeatherCard = false;
        MarkDecoyTargets(view, false);
    }

    // A gold frame around each unit of this side a decoy can take back (non-hero units on the rows)
    public const string DecoyFrame = "DecoyTarget";

    private void MarkDecoyTargets(FieldView view, bool on)
    {
        foreach (RowView row in view.Rows.Values)
            foreach (Transform card in row.Unit.transform)
            {
                CardDisplay display = card.GetComponent<CardDisplay>();
                if (display == null)
                    continue;
                Transform frame = card.Find(DecoyFrame);
                CardStats stats = display.cardStats.GetComponent<CardStats>();
                bool target = on && !stats.unique && stats.faction != Factions.Special;
                if (target && frame == null)
                {
                    Image image = UIKit.NewImage(card, DecoyFrame, UITheme.Current.panelOutline, UITheme.Current.goldBright);
                    image.raycastTarget = false;
                    image.rectTransform.Stretch(-4, -4, -4, -4);
                    image.gameObject.AddComponent<UIPulse>();
                }
                else if (!target && frame != null)
                {
                    frame.SetParent(null, false);
                    Object.Destroy(frame.gameObject);
                }
            }
    }

    // Hides the player's hand interactability
    public void SetHider(bool hidden)
    {
        controller.hiderObject.transform.localPosition = hidden ? new Vector2(0, -461) : new Vector2(0, -618);
    }
}
