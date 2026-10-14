using UnityEngine;
using UnityEngine.EventSystems;

// On a side's discard pile: a click opens the pile in the discard viewer (the cards lying on the pile forward their
// clicks here too). With the leader chosen, a click on the pile it takes a unit from uses it.
public class DiscardPileBrowser : MonoBehaviour, IPointerClickHandler
{
    public bool isPlayer;

    public bool HasCards
    {
        get
        {
            SceneController battle = SceneController.Instance;
            return battle != null && (isPlayer ? battle.PlayerInfo : battle.EnemyInfo).DiscardList.Count > 0;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            Clicked();
    }

    public void Clicked()
    {
        SceneController battle = SceneController.Instance;
        if (battle != null && battle.TryUseLeaderOn(gameObject))
            return;
        Browse();
    }

    public void Browse()
    {
        SceneController battle = SceneController.Instance;
        if (battle == null || !HasCards)
            return;
        GameAudio.PlaySfx("card_select");
        DiscardViewer.Open((isPlayer ? battle.PlayerInfo : battle.EnemyInfo).DiscardList, isPlayer);
    }
}
