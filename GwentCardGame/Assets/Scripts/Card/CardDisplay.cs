using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// This is instansiated on every card creation ! 
public class CardDisplay : MonoBehaviour
{
    // Gameplay related
    public GameObject cardStats;
    // Display related
    public GameObject artworkImage;
    public GameObject natureImage;
    public GameObject strengthImage;
    public GameObject rowImage;
    public GameObject abilityImage;

    void Start()
    {
        // Populate display related details
        OnCreation();
    }

    //----------------------------------------On card creation-----------------------------------------------//
    private void OnCreation()
    {
        CardStats card = cardStats.GetComponent<CardStats>();

        // Change the artwork based on ID
        artworkImage.GetComponent<Image>().sprite = SpriteCache.Load("Cards/List/135x87/" + card._id);

        if (card.faction == "Special")
        {// Special Card:
            // Change to the corresponding special Icon
            natureImage.GetComponent<Image>().sprite = SpriteCache.Load("Cards/Special/" + card._idstr);
            rowImage.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            abilityImage.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        }
        else
        {// Unit Card or Leader:
            if (card.ability == "leader")
            {
                natureImage.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                rowImage.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                abilityImage.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            }
            else
            {
                // Change the strength, strength text color and uniqueness image
                strengthImage.GetComponent<TextMeshProUGUI>().text = card.strength.ToString();
                if (card.unique)
                {
                    natureImage.GetComponent<Image>().sprite = SpriteCache.Load("Cards/Nature/unique");
                    strengthImage.GetComponent<TextMeshProUGUI>().color = Color.white;
                }
                else
                {
                    natureImage.GetComponent<Image>().sprite = SpriteCache.Load("Cards/Nature/not_unique");
                    strengthImage.GetComponent<TextMeshProUGUI>().color = new Color(0, 0, 0);
                }
                // Change row Image
                rowImage.GetComponent<Image>().sprite = SpriteCache.Load("Cards/Row/" + card.row);
                // Change the ability image
                if (string.IsNullOrEmpty(card.ability))
                {
                    abilityImage.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                }
                else
                {
                    if (card.ability.Contains("scorch"))
                        abilityImage.GetComponent<Image>().sprite = SpriteCache.Load("Cards/Ability/scorch");
                    else
                        abilityImage.GetComponent<Image>().sprite = SpriteCache.Load("Cards/Ability/" + card.ability);
                }
            }
        }
    }
}
