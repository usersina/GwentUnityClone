using UnityEngine;
using UnityEngine.EventSystems;

// A card shown in the picker panel (medic, leader abilities). Double-click (tap on mobile) chooses it.
public class CardBtn : MonoBehaviour, IPointerClickHandler
{
    public GameObject CardPrefab;
    public int cardId;
    public string type;
    public string field;
    public bool isPlayerCard;

    private readonly DoubleClick doubleClick = new DoubleClick();

    [HideInInspector]
    public SceneController controller;

    private void Start()
    {
        controller = SceneController.Instance;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (Application.isMobilePlatform || doubleClick.Register())
            Choose();
    }

    public void Choose()
    {
        if (controller == null)
            controller = SceneController.Instance;
        GameObject PanelParent = GetComponentInParent<PanelManager>().gameObject;

        switch (type)
        {
            // Showed by a medic ability or a weather leader: pick one, then place it on the board
            case "play":
                GameAudio.PlaySfx("ability_medic");
                GameLog.Info("Choosing card to play: " + cardId);

                controller.PlayBtnCleanUp(field, cardId, isPlayerCard);
                GameObject instantiatedCard = controller.CreateCardObject(controller.GetCardStats(cardId), isPlayerCard);
                instantiatedCard.transform.SetParent(controller.TempObject != null ? controller.TempObject.transform : null, false);
                controller.selectedCard = instantiatedCard;
                controller.HighlightField(instantiatedCard.GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>(), isPlayerCard);
                controller.ShowcasePicked(cardId, field, isPlayerCard);
                controller.SetHider(true);
                PanelParent.SetActive(false);
                UIOverlay.Toast("Now choose where to play " + controller.GetCardStats(cardId).name);
                break;

            // Showed by a leader ability: pick one to add to hand
            case "draw":
                GameAudio.PlaySfx("card_draw");
                GameLog.Info("Choosing on: " + cardId);
                PanelParent.SetActive(false);
                controller.ActuallyPickCard(field, cardId, isPlayerCard);
                break;

            // Emhyr random look
            case "view":
                GameAudio.PlaySfx("card_select");
                PanelParent.SetActive(false);
                controller.TurnCallBack(isPlayerCard);
                break;
        }
    }
}
