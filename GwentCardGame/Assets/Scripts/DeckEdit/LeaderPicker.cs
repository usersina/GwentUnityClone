using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LeaderPicker : MonoBehaviour
{
    public GameObject contentGO;
    public GameObject leaderWPrefab;
    public DeckController deckController;
    public CollectionManager deckCollection;

    public List<int> NRLeaders = new List<int> { 51, 52, 53, 54, 55};
    public List<int> NFLeaders = new List<int> { 33, 34, 35, 36, 37};
    public List<int> SCLeaders = new List<int> { 57 ,58, 59 ,60 ,61};
    public List<int> MLeaders = new List<int> { 40, 41, 42 ,43 ,44};

    private void Update()
    {
        // Close window on right click
        if (Input.GetMouseButtonDown(1))
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
        switch (faction)
        {
            case "NR":
                CreateLeaders(NRLeaders);
                break;
            case "NF":
                CreateLeaders(NFLeaders);
                break;
            case "SC":
                CreateLeaders(SCLeaders);
                break;
            case "M":
                CreateLeaders(MLeaders);
                break;
            default:
                Debug.LogError("SetLeaderContent: Unexpected value: " + faction);
                break;
        }
    }

    private void CreateLeaders(List<int> leaders_list)
    {
        for (int i = 0; i < leaders_list.Count; i++)
        {
            GameObject instantiatedLeader = Instantiate(leaderWPrefab);
            instantiatedLeader.GetComponent<LeaderWindow>().leaderId = leaders_list[i];
            instantiatedLeader.transform.Find("LImage").GetComponent<Image>().sprite = 
                Resources.Load<Sprite>("Cards/List/591x380/" + leaders_list[i]);
            instantiatedLeader.transform.Find("LEffect").GetComponent<Image>().sprite =
                Resources.Load<Sprite>("Cards/EffectBox/Leader/" + leaders_list[i]);
            instantiatedLeader.transform.SetParent(contentGO.transform, false);
        }
    }
}
