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
