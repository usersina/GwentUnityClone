using System;
using System.Collections.Generic;
using Gwent.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Gwent.UI
{
    // Runtime-built UI on top of the current scene: menus, dialogs, toasts and extra buttons, in the UI theme.
    // Two canvases with the 1920x1080 reference of the scene canvases: Root (menus and dialogs, scaled by the
    // "menu size" setting) and FixedRoot (labels that must keep their place in the scene's layout).
    // Also routes Escape / right click: the top dialog closes first, otherwise the scene's escape action runs.
    public static class UIOverlay
    {
        private static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        private static OverlayRunner runner;
        private static Canvas fixedCanvas;
        private static GameObject currentToast;

        public static Color Gold { get { return UITheme.Current.gold; } }

        public static Transform Root
        {
            get
            {
                if (runner == null)
                    runner = CreateCanvas("UIOverlay", 500, true).gameObject.AddComponent<OverlayRunner>();
                return runner.transform;
            }
        }

        public static Transform FixedRoot
        {
            get
            {
                if (fixedCanvas == null)
                    fixedCanvas = CreateCanvas("UIOverlayFixed", 490, false);
                return fixedCanvas.transform;
            }
        }

        private static Canvas CreateCanvas(string name, int sorting_order, bool scaled_by_settings)
        {
            GameObject canvas_go = new GameObject(name, typeof(RectTransform));
            Canvas canvas = canvas_go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sorting_order;
            CanvasScaler scaler = canvas_go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = scaled_by_settings ? ReferenceResolution / GameSettings.MenuScale : ReferenceResolution;
            scaler.matchWidthOrHeight = 1;
            canvas_go.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null && UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            return canvas;
        }

        //----------------------------------------Building blocks (kept for the scene scripts)----------------------------------------//
        public static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            return UIKit.Rect(name, parent).Place(anchor, position, size);
        }

        public static TextMeshProUGUI CreateText(Transform parent, string text, float font_size, Vector2 anchor, Vector2 position, Vector2 size)
        {
            TextMeshProUGUI label = UIKit.Text(parent, text, UIKit.TextStyle.Body, TextAlignmentOptions.Center);
            label.fontSize = font_size;
            label.rectTransform.Place(anchor, position, size);
            return label;
        }

        public static Button CreateButton(Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size, Action on_click)
        {
            Button button = UIKit.Button(parent, text, UIKit.ButtonStyle.Secondary, on_click, size);
            ((RectTransform)button.transform).Place(anchor, position, size);
            return button;
        }

        //----------------------------------------Toast----------------------------------------//
        // Short message at the top of the screen, replaced by the next one; never blocks clicks
        public static void Toast(string message, float seconds = 2.5f)
        {
            GameLog.Info("Toast: " + message);
            if (currentToast != null)
                UnityEngine.Object.Destroy(currentToast);

            RectTransform panel = UIKit.Panel(Root, "Toast", true).Place(new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(1100, 84));
            foreach (Graphic graphic in panel.GetComponentsInChildren<Graphic>())
                graphic.raycastTarget = false;
            TextMeshProUGUI label = UIKit.Text(panel, message, UIKit.TextStyle.Body, TextAlignmentOptions.Center);
            label.rectTransform.Stretch(30, 30, 0, 2);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = true;
            label.fontSizeMin = 20;
            label.fontSizeMax = 32;
            UIMotion.ShowFor(panel.gameObject, seconds);
            currentToast = panel.gameObject;
        }

        //----------------------------------------Confirmation dialog----------------------------------------//
        public static void Confirm(string message, string yes_text, string no_text, Action on_yes)
        {
            RectTransform dimmer = UIKit.Dimmer(Root, "ConfirmDialog");
            RectTransform panel = UIKit.Panel(dimmer, "Panel").Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920, 330));
            UIKit.Ornament(panel, new Vector2(0.5f, 1f), new Vector2(0, -40), 360);
            TextMeshProUGUI label = UIKit.Text(panel, message, UIKit.TextStyle.Body, TextAlignmentOptions.Center);
            label.rectTransform.Stretch(60, 60, 56, 124);
            label.textWrappingMode = TextWrappingModes.Normal;

            GameObject dialog = dimmer.gameObject;
            Button yes = UIKit.Button(panel, yes_text, UIKit.ButtonStyle.Primary, () =>
            {
                CloseModal(dialog);
                on_yes();
            });
            ((RectTransform)yes.transform).Place(new Vector2(0.5f, 0f), new Vector2(-160, 70), new Vector2(280, 68));
            Button no = UIKit.Button(panel, no_text, UIKit.ButtonStyle.Secondary, () => CloseModal(dialog));
            ((RectTransform)no.transform).Place(new Vector2(0.5f, 0f), new Vector2(160, 70), new Vector2(280, 68));
            PushModal(dialog);
            UIKit.Focus(no); // Enter on a keyboard picks the safe choice
        }

        //----------------------------------------Modal dialogs and Escape----------------------------------------//
        private class Modal
        {
            public GameObject root;
            public Action onClose;
            public bool closeOnRightClick;
            public List<KeyValuePair<Selectable, Navigation>> blockedNavigation;
            public GameObject previousSelection;
        }

        private static readonly List<Modal> modals = new List<Modal>();
        private static int lastCancelFrame = -1;

        // A dialog is open (or one was just closed this frame): scene scripts must not also react to Escape / right click
        public static bool BlocksCancel
        {
            get
            {
                Prune();
                return modals.Count > 0 || lastCancelFrame == Time.frameCount;
            }
        }

        public static bool HasModal
        {
            get
            {
                Prune();
                return modals.Count > 0;
            }
        }

        // What Escape (and the Android back button) does in this scene when no dialog is open
        public static void SetEscapeAction(Action action)
        {
            Root.GetComponent<OverlayRunner>().escapeAction = action;
        }

        // Registers a dialog: Escape (and right click, unless disabled) closes it, keyboard navigation stays inside it
        public static void PushModal(GameObject root, Action on_close = null, bool close_on_right_click = true)
        {
            Modal modal = new Modal
            {
                root = root,
                onClose = on_close,
                closeOnRightClick = close_on_right_click,
                blockedNavigation = new List<KeyValuePair<Selectable, Navigation>>(),
                previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null,
            };
            foreach (Selectable selectable in Selectable.allSelectablesArray)
            {
                if (selectable.transform.IsChildOf(root.transform))
                    continue;
                modal.blockedNavigation.Add(new KeyValuePair<Selectable, Navigation>(selectable, selectable.navigation));
                selectable.navigation = new Navigation { mode = Navigation.Mode.None };
            }
            modals.Add(modal);
        }

        // This dialog is the one on top (keys and clicks are its own)
        public static bool IsTopModal(GameObject root)
        {
            Prune();
            return modals.Count > 0 && modals[modals.Count - 1].root == root;
        }

        public static void CloseModal(GameObject root)
        {
            int index = modals.FindIndex(m => m.root == root);
            if (index < 0)
            {
                if (root != null)
                    UnityEngine.Object.Destroy(root);
                return;
            }
            Modal modal = modals[index];
            modals.RemoveAt(index);
            foreach (KeyValuePair<Selectable, Navigation> entry in modal.blockedNavigation)
                if (entry.Key != null)
                    entry.Key.navigation = entry.Value;
            if (modal.root != null)
                UnityEngine.Object.Destroy(modal.root);
            if (EventSystem.current != null && modal.previousSelection != null)
                EventSystem.current.SetSelectedGameObject(modal.previousSelection);
            if (modal.onClose != null)
                modal.onClose();
        }

        private static void Prune()
        {
            modals.RemoveAll(m => m.root == null);
        }

        private class OverlayRunner : MonoBehaviour
        {
            public Action escapeAction;
            private CanvasScaler scaler;

            private void Awake()
            {
                scaler = GetComponent<CanvasScaler>();
            }

            private void Update()
            {
                Vector2 reference = ReferenceResolution / GameSettings.MenuScale;
                if (scaler != null && scaler.referenceResolution != reference)
                    scaler.referenceResolution = reference;

                bool escape = GameInput.EscapePressed();
                bool right_click = GameInput.SecondaryPressed();
                if (!escape && !right_click)
                    return;

                Prune();
                if (modals.Count > 0)
                {
                    Modal top = modals[modals.Count - 1];
                    if (escape || top.closeOnRightClick)
                    {
                        lastCancelFrame = Time.frameCount;
                        CloseModal(top.root);
                    }
                }
                else if (escape && escapeAction != null && lastCancelFrame != Time.frameCount)
                {
                    lastCancelFrame = Time.frameCount;
                    escapeAction();
                }
            }

            private void OnDestroy()
            {
                // Scene change: the dialogs went with the canvas
                modals.Clear();
            }
        }
    }
}
