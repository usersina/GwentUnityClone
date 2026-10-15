using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Gwent.Core;
using Gwent.UI;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace Gwent.Battle
{
    // Moving cards over the board (game time, so the pause menu and the autoplay's speed apply). A flight is a "ghost"
    // on the overlay (a copy of the card's object in its new place, or the object of a card that left its place) gliding
    // to the card's object in its new place, following it while its line makes room; the object shows once the ghost
    // lands. A card thus looks in flight as it will where it lands, from the start (The Witcher 3).
    public static class CardMotion
    {
        private const float SettleSeconds = 0.25f;
        private static RectTransform layer;
        private static readonly Dictionary<GameObject, MotionHandle> settling = new Dictionary<GameObject, MotionHandle>();

        // The overlay's layer for the battle's moving cards and effects, cleared at once when a battle is cut short
        public static Transform Layer
        {
            get
            {
                if (layer == null)
                {
                    layer = new GameObject("BoardEffects", typeof(RectTransform)).GetComponent<RectTransform>();
                    layer.SetParent(UIOverlay.FixedRoot, false);
                    layer.anchorMin = Vector2.zero;
                    layer.anchorMax = Vector2.one;
                    layer.offsetMin = layer.offsetMax = Vector2.zero; // the overlay's own frame: centred, same size
                }
                return layer;
            }
        }

        public static void ClearLayer()
        {
            if (layer == null)
                return;
            foreach (Transform child in layer)
                Object.Destroy(child.gameObject);
        }

        // Screen rectangle of a UI element, whatever canvas it is on
        public static Rect ScreenRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // The overlay's layout (centre and size) of a screen rectangle
        public static void ToLayout(Rect screen_rect, out Vector2 center, out Vector2 size)
        {
            RectTransform root = (RectTransform)UIOverlay.FixedRoot;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen_rect.center, null, out center);
            size = screen_rect.size / root.GetComponent<Canvas>().scaleFactor;
        }

        // Puts an element of the overlay over a screen rectangle
        public static void Place(RectTransform rect, Rect screen_rect)
        {
            Vector2 center, size;
            ToLayout(screen_rect, out center, out size);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center; // the overlay's root is centred
            rect.sizeDelta = size;
        }

        // A copy of a card object on the overlay, over a screen rectangle: the card as it looks there (its art, strength,
        // ability icons, hero frame), to fly to it. Only its look: made under an inactive holder, so that none of the
        // card's behaviours wakes up, and stripped of them; it never catches clicks.
        public static RectTransform Copy(GameObject card, Rect screen_rect)
        {
            CardFace face = card.GetComponent<CardFace>();
            if (face != null)
                face.EnsureShown();
            GameObject holder = new GameObject("CardCopy", typeof(RectTransform));
            holder.SetActive(false);
            holder.transform.SetParent(Layer, false);
            GameObject copy = Object.Instantiate(card, holder.transform, false);
            copy.name = card.name;
            foreach (MonoBehaviour behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour is UnityEngine.EventSystems.UIBehaviour)) // the images and texts stay
                    Object.DestroyImmediate(behaviour);
            RectTransform rect = (RectTransform)copy.transform;
            rect.SetParent(Layer, false);
            Object.Destroy(holder);
            rect.localScale = Vector3.one;
            CanvasGroup group = Group(copy);
            group.alpha = 1; // the object it copies is hidden until the copy lands
            group.blocksRaycasts = false;
            Place(rect, screen_rect);
            return rect;
        }

        // A rectangle of that size centred on a screen point
        public static Rect Around(Vector2 center, Vector2 size)
        {
            return new Rect(center - size / 2, size);
        }

        // A card object taken out of its place onto the overlay, where it stood (it no longer catches clicks)
        public static RectTransform Lift(GameObject card)
        {
            RectTransform rect = (RectTransform)card.transform;
            Rect screen_rect = ScreenRect(rect);
            rect.SetParent(Layer, false);
            Place(rect, screen_rect);
            Group(card).blocksRaycasts = false;
            return rect;
        }

        // A hidden card does not catch the pointer either (unless it stays clickable: the selected card's empty slot)
        public static void Hide(GameObject card, bool hidden, bool clickable = false)
        {
            SetAlpha(card, hidden ? 0 : 1);
            if (card != null)
                Group(card).blocksRaycasts = !hidden || clickable;
        }

        public static void SetAlpha(GameObject card, float alpha)
        {
            if (card != null)
                Group(card).alpha = alpha;
        }

        private static CanvasGroup Group(GameObject card)
        {
            CanvasGroup group = card.GetComponent<CanvasGroup>();
            return group != null ? group : card.AddComponent<CanvasGroup>();
        }

        // Glides a ghost to the card object (followed as it moves) along a slight arc, shows the object and removes the
        // ghost. Eases out, as The Witcher 3's cards do.
        public static async UniTask Fly(RectTransform ghost, GameObject target, float seconds, float arc = 0, CancellationToken ct = default)
        {
            if (target != null)
            {
                Rect last = ScreenRect((RectTransform)target.transform);
                await Glide(ghost, () => target != null ? last = ScreenRect((RectTransform)target.transform) : last, seconds, arc, ct);
            }
            Hide(target, false);
            if (ghost != null)
                Object.Destroy(ghost.gameObject);
        }

        // Glides a ghost to a screen rectangle (asked again every frame, as it may move), easing out along a slight arc
        public static UniTask Glide(RectTransform ghost, System.Func<Rect> to, float seconds, float arc = 0, CancellationToken ct = default)
        {
            if (ghost == null)
                return UniTask.CompletedTask;
            Vector2 from_pos = ghost.anchoredPosition, from_size = ghost.sizeDelta;
            return LMotion.Create(0f, 1f, seconds).WithEase(Ease.OutCubic).Bind(eased =>
            {
                Vector2 to_pos, to_size;
                ToLayout(to(), out to_pos, out to_size);
                Vector2 path = to_pos - from_pos;
                Vector2 bend = path.sqrMagnitude > 1 ? new Vector2(path.y, -path.x).normalized * arc * Mathf.Sin(Mathf.PI * eased) : Vector2.zero;
                ghost.anchoredPosition = Vector2.LerpUnclamped(from_pos, to_pos, eased) + bend;
                ghost.sizeDelta = Vector2.LerpUnclamped(from_size, to_size, eased);
            }).AddTo(ghost.gameObject).ToUniTask(ct);
        }

        // Glides an element of the overlay to a screen rectangle (asked again every frame, as it may move), easing out;
        // done once it has arrived (not if it is cancelled or the element goes first)
        public static MotionHandle GlideOver(RectTransform rect, System.Func<Rect> to, float seconds, System.Action done)
        {
            Rect from = ScreenRect(rect);
            return LMotion.Create(0f, 1f, seconds).WithEase(Ease.OutCubic).WithOnComplete(done).Bind(eased =>
            {
                Rect target = to();
                Place(rect, new Rect(Vector2.LerpUnclamped(from.position, target.position, eased), Vector2.LerpUnclamped(from.size, target.size, eased)));
            }).AddTo(rect.gameObject);
        }

        // Fades a ghost out where it stands (a card leaving the board for good)
        public static async UniTask FadeOut(RectTransform ghost, float seconds, CancellationToken ct = default)
        {
            if (ghost == null)
                return;
            await LMotion.Create(1f, 0f, seconds).BindToAlpha(Group(ghost.gameObject)).AddTo(ghost.gameObject).ToUniTask(ct);
            if (ghost != null)
                Object.Destroy(ghost.gameObject);
        }

        // A card that appears without coming from anywhere: fades in from slightly larger (again from the start if it was
        // already settling)
        public static void SettleIn(GameObject card)
        {
            MotionHandle running;
            if (settling.TryGetValue(card, out running))
                running.TryCancel();
            CanvasGroup group = Group(card);
            Transform t = card.transform;
            settling[card] = LMotion.Create(0f, 1f, SettleSeconds).WithEase(Ease.OutQuad)
                .WithOnComplete(() => settling.Remove(card)).WithOnCancel(() => settling.Remove(card))
                .Bind(eased => { group.alpha = eased; t.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, eased); })
                .AddTo(card);
        }
    }
}
