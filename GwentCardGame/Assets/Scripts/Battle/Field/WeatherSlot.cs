using System.Collections;
using System.Collections.Generic;
using Gwent.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gwent.Battle
{
    public class WeatherSlot : MonoBehaviour, IPointerClickHandler
    {
        [HideInInspector]
        public BattleController controller;

        public Sprite weather;
        public Sprite weatherSelected;

        public bool isWeatherCard;

        private void Start()
        {
            controller = BattleController.Instance;
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
                controller = BattleController.Instance;
            if (controller.battleState == BattleState.PlayerTurn)
            {
                // The lifted leader: its crown uses it, not the board
                if (controller.LeaderSelected)
                {
                    controller.HintLeaderCrown();
                    return;
                }
                if (controller.selectedCard == null)
                    return;
                if (!(isWeatherCard && controller.TryPlaySelectedCardOnBoard()))
                    GameAudio.PlaySfx("invalid"); // only weather and scorch go on the weather board
            }
        }

    }
}
