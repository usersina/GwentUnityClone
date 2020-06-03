using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using static SceneController;

public class CardBtn : MonoBehaviour, IPointerClickHandler
{
    public GameObject CardPrefab;
    public int cardId;
    public string type;
    public string field;
    public bool isPlayerCard;

    float lastClick = 0f;
    float interval = 0.4f;

    [HideInInspector]
    public GameObject controllerObject;
    [HideInInspector]
    public SceneController controller;
    [HideInInspector]
    public GameObject tempObject;
    [HideInInspector]
    public GameObject hiderObject;

    private void Start()
    {
        controllerObject = GameObject.Find("SceneController");
        controller = controllerObject.GetComponent<SceneController>();

        tempObject = GameObject.Find("Temp");
        hiderObject = GameObject.Find("Hider");
    }

    // On Double Click
    public void OnPointerClick(PointerEventData eventData)
    {
        switch (type)
        {
            // Showed by a medic ability, pick one to summon
            case "play":
                if ((lastClick + interval) > Time.time)
                {
                    Debug.Log("Reviving card: " + cardId);

                    // Panel GameObject
                    GameObject PanelParent = transform.parent.parent.parent.gameObject;

                    Card card = controller.GetCardStats(cardId);
                    GameObject instantiatedCard = Instantiate(CardPrefab);
                    instantiatedCard.name = card._id.ToString();
                    instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().name = card.name;
                    instantiatedCard.transform.Find("Stats").GetComponent<CardStats>()._id = card._id;
                    instantiatedCard.transform.Find("Stats").GetComponent<CardStats>()._idstr = card._idstr;
                    instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().faction = card.faction;
                    instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().unique = card.unique;
                    instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().strength = card.strength;
                    instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().row = card.row;
                    instantiatedCard.transform.Find("Stats").GetComponent<CardStats>().ability = card.ability;
                    if (isPlayerCard)
                        instantiatedCard.tag = "Player";
                    else
                        instantiatedCard.tag = "Enemy";

                    controller.PlayBtnCleanUp(field, cardId, isPlayerCard);

                    instantiatedCard.transform.SetParent(tempObject.transform, false);
                    controller.selectedCard = instantiatedCard;
                    controller.HighlightField(instantiatedCard.GetComponent<CardDisplay>().cardStats.GetComponent<CardStats>(), isPlayerCard);
                    controller.SetHider(true);
                    PanelParent.SetActive(false);
                }
                else
                {
                    //Debug.Log("Single Click! ");
                    lastClick = Time.time;
                }
                break;

            // Showed by a leader ability, pick one to add to hand
            case "draw":
                if ((lastClick + interval) > Time.time)
                {
                    Debug.Log("Choosing on: " + cardId);

                    // Panel GameObject
                    GameObject PanelParent = transform.parent.parent.parent.gameObject;
                    controller.ActuallyPickCard(field, cardId, isPlayerCard);
                    PanelParent.SetActive(false);
                }
                else
                {
                    //Debug.Log("Single Click! ");
                    lastClick = Time.time;
                }
                break;

            // Emhyr random look
            case "view":
                if ((lastClick + interval) > Time.time)
                {
                    GameObject PanelParent = transform.parent.parent.parent.gameObject;
                    controller.TurnCallBack(isPlayerCard);
                    PanelParent.SetActive(false);
                }
                else
                {
                    //Debug.Log("Single Click! ");
                    lastClick = Time.time;
                }
                break;
        }
    }
}