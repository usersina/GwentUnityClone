using System;
using System.Collections.Generic;
using Gwent.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gwent.UI
{
    // The menus' screens in UI Toolkit: a UXML document (Assets/UI/Toolkit, styled by Gwent.uss) on a Panel Renderer of
    // its own. The panels share UITheme's Panel Settings: the 1920x1080 reference of the canvases, matched on the height
    // and scaled by the "menu size" setting, sorted between the scene's canvases (0) and the dialogs' (UIOverlay, 500), so
    // a dialog still covers a screen.
    public static class UIScreens
    {
        private static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        private static PanelSettings settings;
        private static readonly List<VisualElement> roots = new List<VisualElement>(); // the screens shown, for Find

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            GameSettings.Changed -= ApplyMenuScale;
            settings = null;
            roots.Clear();
        }

        // Shows the document; bind finds its elements and hooks them up, each time the document is rebuilt
        public static PanelRenderer Show(string name, VisualTreeAsset document, Action<VisualElement> bind)
        {
            GameObject screen = new GameObject(name);
            PanelRenderer renderer = screen.AddComponent<PanelRenderer>();
            int bound = -1;
            renderer.RegisterUIReloadCallback((_, root, version) =>
            {
                if (version == bound)
                    return; // the same document as already bound
                bound = version;
                // A Panel Renderer's root does not fill the panel by itself
                root.style.position = Position.Absolute;
                root.style.left = 0;
                root.style.top = 0;
                root.style.right = 0;
                root.style.bottom = 0;
                roots.Add(root);
                bind(root);
            });
            renderer.panelSettings = Settings;
            renderer.visualTreeAsset = document;
            return renderer;
        }

        // An element of a screen on show, by name (tools and tests reach the screens through it), or null
        public static VisualElement Find(string name)
        {
            roots.RemoveAll(root => root.panel == null); // gone with their scene
            foreach (VisualElement root in roots)
            {
                VisualElement element = root.Q(name);
                if (element != null)
                    return element;
            }
            return null;
        }

        // A copy of the theme's Panel Settings: the menu size changes its reference resolution
        private static PanelSettings Settings
        {
            get
            {
                if (settings == null)
                {
                    settings = UnityEngine.Object.Instantiate(UITheme.Current.panelSettings);
                    settings.name = UITheme.Current.panelSettings.name;
                    ApplyMenuScale();
                    GameSettings.Changed += ApplyMenuScale;
                }
                return settings;
            }
        }

        private static void ApplyMenuScale()
        {
            if (settings != null)
                settings.referenceResolution = Vector2Int.RoundToInt(ReferenceResolution / GameSettings.MenuScale);
        }
    }
}
