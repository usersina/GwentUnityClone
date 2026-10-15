using UnityEngine;

namespace Gwent.UI
{
    // Fades and settles a screen in (unscaled time, so it also works while the game is paused)
    public class UIAppear : MonoBehaviour
    {
        private const float Duration = 0.45f;
        private CanvasGroup group;
        private float age;

        private void Awake()
        {
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            transform.localScale = Vector3.one * 0.96f;
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(age / Duration);
            float eased = 1 - (1 - t) * (1 - t);
            group.alpha = eased;
            transform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, eased);
            if (t >= 1)
                enabled = false;
        }
    }
}
