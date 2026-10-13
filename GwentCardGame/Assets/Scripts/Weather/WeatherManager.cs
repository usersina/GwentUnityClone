using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class WeatherManager : MonoBehaviour, IPointerClickHandler
{
    [HideInInspector]
    public SceneController controller;

    public Sprite weather;
    public Sprite weatherSelected;

    public bool isWeatherCard;

    private void Start()
    {
        controller = SceneController.Instance;
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
        if (controller == null)
            controller = SceneController.Instance;
        if (controller.battleState == BattleState.PLAYERTURN)
        {
            if (controller.selectedCard != null && isWeatherCard && controller.TryPlaySelectedCardOnBoard())
            {
                GameLog.Info("Player placing weather card !");
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
