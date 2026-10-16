using Gwent.Core;
using Gwent.UI;
using UnityEngine;

namespace Gwent.DeckBuilding
{
    public class CloseOnCancel : MonoBehaviour
    {
        // Closed by a right click or Escape (unless a dialog over it takes them)
        void Update()
        {
            if (GameInput.CancelPressed() && !UIOverlay.BlocksCancel)
                gameObject.SetActive(false);
        }
    }
}
