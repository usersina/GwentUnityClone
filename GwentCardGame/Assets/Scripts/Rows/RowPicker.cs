using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RowPicker : MonoBehaviour
{
    public Sprite close;
    public Sprite range;
    public Sprite siege;

    public Sprite closeSelected;
    public Sprite rangeSelected;
    public Sprite siegeSelected;

    // The rows' emblems (sword, bow, catapult), laid over an empty row: the row art has none, so cards never show them
    public Sprite closeEmblem;
    public Sprite rangeEmblem;
    public Sprite siegeEmblem;
    public Sprite closeEmblemSelected;
    public Sprite rangeEmblemSelected;
    public Sprite siegeEmblemSelected;

    public Sprite special;
    public Sprite specialSelected;

    public Sprite orangeCircle;
    public Sprite blueCircle;

    public List<GameObject> highLightedRows;

    //--------------------------------Highlights related-----------------------------------//

    public void ResetStored()
    {
        highLightedRows.Clear();
    }

    public void StoreField(GameObject unit_field)
    {
        highLightedRows.Add(unit_field);
    }
}
