using UnityEngine;

// Click on a card: select a hand card, or (on the board) use the selected weather/scorch/decoy, or the leader
public class CardSelect : MonoBehaviour
{
    [HideInInspector]
    public SceneController controller;

    // Needs to be false if placed on the field
    public bool isSelectable;

    void Start()
    {
        controller = SceneController.Instance;
    }

    public void OnCardSelect()
    {
        if (controller == null)
            controller = SceneController.Instance;

        if (isSelectable)
        {
            // If the turn is the player's and the card is his, hence the Tag
            if (controller.IsPlayerActionAllowed && gameObject.CompareTag("Player") && !controller.PlayerInfo.CanDiscard)
            {
                // Lightoff any previous fields
                controller.LightoffField(true);
                controller.LightoffField(false);
                GameAudio.PlaySfx("card_select");
                if (!CheckSameCard())
                {
                    CardHover hover = gameObject.GetComponent<CardHover>();
                    hover.isHoverable = false;
                    hover.TranslateUp();
                    hover.ShowPreview();
                    controller.selectedCard = gameObject;
                    // Hightlights the appropriate field based on: Card Faction AND Card Row
                    controller.HighlightField(gameObject.GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>(), true);
                }
            }
            return;
        }

        if (controller.TryPlaySelectedDecoyOnCard(gameObject))
            return;

        if (controller.TryPlaySelectedCardOnBoard())
            return;

        TryActivateLeaderCard();
    }

    private bool TryActivateLeaderCard()
    {
        if (controller.battleState != BattleState.PLAYERTURN || !gameObject.CompareTag("Player"))
            return false;

        CardStats cardStats = gameObject.GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>();
        if (cardStats.ability != "leader")
            return false;

        LeaderManager leaderManager = GetComponentInParent<LeaderManager>();
        if (leaderManager == null)
            return false;

        leaderManager.ActivateLeader();
        return true;
    }

    // Deselects the previously selected card; true if it was this very card (click again = deselect)
    private bool CheckSameCard()
    {
        if (controller.selectedCard == null)
            return false;

        bool same_card = controller.selectedCard == gameObject;
        CardHover hover = controller.selectedCard.GetComponent<CardHover>();
        hover.isHoverable = true;
        hover.TranslateDown();
        hover.DestroyEffect();
        controller.selectedCard = null;
        return same_card;
    }
}
