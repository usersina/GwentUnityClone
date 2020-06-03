using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class WeatherManager : MonoBehaviour, IPointerClickHandler
{
    [HideInInspector]
    public GameObject controllerObject;
    [HideInInspector]
    public SceneController controller;

    public Sprite weather;
    public Sprite weatherSelected;

    public bool isWeatherCard;

    private void Start()
    {
        controllerObject = GameObject.Find("SceneController");
        controller = controllerObject.GetComponent<SceneController>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) // Detect a left click
        {
            OnWeatherClick();
        }
    }

    public void OnWeatherClick()
    {
        if (controller.battleState == BattleState.PLAYERTURN)
        {
            if (controller.selectedCard != null && isWeatherCard)
            {
                Debug.Log("Player placing weather card !");
                controller.DirectlyPlaceCard(controller.selectedCard, gameObject, true);
            }
        }
    }

    public void ClearWeatherObject()
    {
        for (int i=0; i< gameObject.transform.childCount; i++)
        {
            Destroy(gameObject.transform.GetChild(i).gameObject);
        }
        gameObject.transform.DetachChildren();
    }
}
