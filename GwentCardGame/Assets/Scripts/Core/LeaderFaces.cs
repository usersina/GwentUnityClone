using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gwent.Core
{
    // Where each leader's face is on its card, for the round portraits (the opponent picker, the LAN lobby, How to play,
    // the battle's side panels and end screen): the point put at the circle's middle (fractions of the card from its top
    // left) and how far in (the card's width, in circle diameters). The faces are not in the same place on every card
    // (Eredin's helmet near the top, Francesca small on her throne), so one framing for all cut most of them off. The
    // circle stays inside the card, above its name banner. Francesca's covered versions (CensoredArt) place her
    // differently: those have their own framing.
    public static class LeaderFaces
    {
        private const float CardAspect = 1048f / 672f; // the card art's height over its width
        private const float Banner = 0.8f;             // the name banner starts this far down the card
        private static readonly Vector3 Plain = new Vector3(0.5f, 0.28f, 1.75f); // a card not listed: the old framing

        // x, y: the face; z: the card's width in circle diameters
        private static readonly Dictionary<int, Vector3> Faces = new Dictionary<int, Vector3>
        {
            // Northern Realms: Foltest
            { 51, new Vector3(0.38f, 0.205f, 1.9f) }, { 52, new Vector3(0.52f, 0.195f, 1.9f) }, { 53, new Vector3(0.51f, 0.25f, 1.9f) },
            { 54, new Vector3(0.52f, 0.16f, 1.9f) }, { 55, new Vector3(0.53f, 0.16f, 1.9f) },
            // Nilfgaard: Emhyr
            { 33, new Vector3(0.73f, 0.18f, 2f) }, { 34, new Vector3(0.43f, 0.23f, 1.9f) }, { 35, new Vector3(0.42f, 0.24f, 1.9f) },
            { 36, new Vector3(0.63f, 0.30f, 2f) }, { 37, new Vector3(0.33f, 0.24f, 1.9f) },
            // Scoia'tael: Francesca, small on Daisy of the Valley and The Beautiful
            { 57, new Vector3(0.68f, 0.11f, 4f) }, { 58, new Vector3(0.46f, 0.25f, 2.2f) }, { 59, new Vector3(0.45f, 0.20f, 2.4f) },
            { 60, new Vector3(0.50f, 0.22f, 1.9f) }, { 61, new Vector3(0.68f, 0.085f, 3.6f) },
            // Monsters: Eredin
            { 40, new Vector3(0.56f, 0.18f, 1.9f) }, { 41, new Vector3(0.69f, 0.16f, 2f) }, { 42, new Vector3(0.68f, 0.13f, 2.2f) },
            { 43, new Vector3(0.42f, 0.15f, 2f) }, { 44, new Vector3(0.72f, 0.14f, 2f) },
            // Skellige: Crach an Craite, King Bran
            { 157, new Vector3(0.37f, 0.22f, 2f) }, { 165, new Vector3(0.38f, 0.13f, 2f) },
        };

        // The covered versions' faces, where they differ (Francesca's hood moves her)
        private static readonly Dictionary<int, Vector3> CoveredFaces = new Dictionary<int, Vector3>
        {
            { 57, new Vector3(0.73f, 0.066f, 4f) }, { 58, new Vector3(0.46f, 0.27f, 2.2f) }, { 59, new Vector3(0.32f, 0.14f, 2.4f) },
            { 61, new Vector3(0.71f, 0.066f, 3.6f) },
        };

        // The point of the card at the circle's middle, kept so the circle stays on the card; zoom: the card's width in
        // circle diameters. covered: the card shows its covered version
        public static Vector2 Point(int leader_id, bool covered, out float zoom)
        {
            Vector3 face;
            if (!(covered && CoveredFaces.TryGetValue(leader_id, out face)) && !Faces.TryGetValue(leader_id, out face))
                face = Plain;
            zoom = face.z;
            float half_x = 0.5f / zoom, half_y = 0.5f / (zoom * CardAspect);
            return new Vector2(Mathf.Clamp(face.x, half_x, 1 - half_x), Mathf.Clamp(face.y, half_y, Banner - half_y));
        }

        // A UI Toolkit portrait: the art element in its round box (sized in the box's percentages, so any ring fits)
        public static void Frame(VisualElement art, int leader_id, bool covered)
        {
            float zoom;
            Vector2 point = Point(leader_id, covered, out zoom);
            art.style.position = Position.Absolute;
            art.style.width = Length.Percent(zoom * 100);
            art.style.height = Length.Percent(zoom * CardAspect * 100);
            art.style.left = Length.Percent(50 - point.x * zoom * 100);
            art.style.top = Length.Percent(50 - point.y * zoom * CardAspect * 100);
        }

        // A uGUI portrait: the art image in its round mask, at the mask's middle
        public static void Frame(RectTransform art, int leader_id, bool covered)
        {
            float diameter = ((RectTransform)art.parent).rect.width;
            float zoom;
            Vector2 point = Point(leader_id, covered, out zoom);
            art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.5f);
            art.pivot = new Vector2(point.x, 1 - point.y);
            art.sizeDelta = new Vector2(diameter * zoom, diameter * zoom * CardAspect);
            art.anchoredPosition = Vector2.zero;
        }
    }
}
