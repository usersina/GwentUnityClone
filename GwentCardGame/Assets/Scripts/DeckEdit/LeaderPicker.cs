using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LeaderPicker : MonoBehaviour
{
    public GameObject contentGO;
    public GameObject leaderWPrefab;
    public DeckController deckController;
    public CollectionManager deckCollection;


    private void Update()
    {
        // Close window on right click
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            gameObject.SetActive(false);
    }

    public void ClearLeaderContent()
    {
        for (int i = 0; i < contentGO.transform.childCount; i++)
        {
            Destroy(contentGO.transform.GetChild(i).gameObject);
        }
        // Do not remove this, otherwise count will still get the previous frame's count (NOT 0);
        contentGO.transform.DetachChildren();
    }

    public void SetLeaderContent(string faction)
    {
        int[] leaders;
        if (Gwent.Rules.LeaderIds.ByFaction.TryGetValue(faction ?? "", out leaders))
            CreateLeaders(leaders);
        else
            Debug.LogError("SetLeaderContent: Unexpected value: " + faction);
    }

    private void CreateLeaders(int[] leaders_list)
    {
        for (int i = 0; i < leaders_list.Length; i++)
        {
            GameObject instantiatedLeader = Instantiate(leaderWPrefab);
            instantiatedLeader.GetComponent<LeaderWindow>().leaderId = leaders_list[i];
            instantiatedLeader.transform.Find("LImage").GetComponent<Image>().sprite =
                SpriteCache.Load("Cards/List/591x380/" + leaders_list[i]);
            instantiatedLeader.transform.Find("LEffect").GetComponent<Image>().sprite =
                SpriteCache.Load("Cards/EffectBox/Leader/" + leaders_list[i]);
            instantiatedLeader.transform.SetParent(contentGO.transform, false);
        }
    }
}
