using System;
using Gwent.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gwent.UI
{
    // UITooltip for UI Toolkit: a small themed hint above an element while the pointer rests on it (with touch controls:
    // while a finger is held on it). It is drawn on the element's panel, above its documents (Gwent.uss, .tooltip).
    public static class ElementTooltip
    {
        private const long DelayMs = 300;
        private const long LongPressMs = (long)(LongPress.Seconds * 1000);
        private const float Gap = 10;

        // text is asked each time the tooltip opens, so it can describe the element's current state (rich text)
        public static void Attach(VisualElement element, Func<string> text)
        {
            IVisualElementScheduledItem pending = null;
            Label shown = null;
            Action hide = () =>
            {
                if (pending != null)
                    pending.Pause();
                if (shown != null)
                    shown.RemoveFromHierarchy();
                shown = null;
            };
            Action<long> show_in = delay =>
            {
                hide();
                pending = element.schedule.Execute(() => shown = Show(element, text())).StartingIn(delay);
            };
            // A finger also "enters" what it touches: its tooltip waits for the long press, which is no tap
            element.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (!GameInput.IsTouch)
                    show_in(DelayMs);
            });
            element.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (GameInput.IsTouch)
                    show_in(LongPressMs);
            });
            element.RegisterCallback<PointerUpEvent>(_ =>
            {
                if (GameInput.IsTouch)
                    hide();
            });
            element.RegisterCallback<PointerLeaveEvent>(_ => hide());
            element.RegisterCallback<DetachFromPanelEvent>(_ => hide());
        }

        private static Label Show(VisualElement target, string message)
        {
            if (string.IsNullOrEmpty(message) || target.panel == null)
                return null;
            VisualElement root = target.panel.visualTree;
            Label tooltip = new Label(message) { name = "Tooltip", pickingMode = PickingMode.Ignore };
            tooltip.AddToClassList("tooltip");
            tooltip.style.visibility = Visibility.Hidden; // until its size is known
            root.Add(tooltip);
            // Above the element's top edge, kept on screen
            tooltip.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                Rect around = target.worldBound, size = tooltip.layout, screen = root.layout;
                float x = Mathf.Clamp(around.center.x - size.width / 2, 8, screen.width - size.width - 8);
                tooltip.style.left = x;
                tooltip.style.top = around.yMin - size.height - Gap;
                tooltip.style.visibility = Visibility.Visible;
            });
            return tooltip;
        }
    }
}
