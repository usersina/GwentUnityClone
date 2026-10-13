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
            if (controller.selectedCard == null)
                return;
            if (isWeatherCard && controller.TryPlaySelectedCardOnBoard())
                GameLog.Info("Player placing weather card !");
            else
                GameAudio.PlaySfx("invalid"); // only weather and scorch go on the weather board
        }
    }

}
