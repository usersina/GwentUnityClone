using System.Collections;
using System.Collections.Generic;
using Gwent.Core;
using Gwent.UI;
using UnityEngine;

namespace Gwent.DeckBuilding
{
    public class CloseOnCancel : MonoBehaviour
    {
        // Update is called once per frame
        void Update()
        {
            if (GameInput.CancelPressed() && !UIOverlay.BlocksCancel)
                gameObject.SetActive(false);
        }
    }
}
