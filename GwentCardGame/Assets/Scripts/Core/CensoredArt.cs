using System;
using UnityEngine;

namespace Gwent.Core
{
    // The censored versions of the art, chosen in Settings > Game > Censored artwork (on by default). A censored image is
    // its original's file with "-censored" after its name, beside it and of the same size: a card's full art
    // (Addressables "CardArt/<id>-censored", see CardArt), its board picture (Resources "Cards/List/135x87/<id>-censored")
    // and the menu's key art. An image with no censored version shows as it is. Turned on or off, every image on screen
    // changes at once (Changed).
    public static class CensoredArt
    {
        public const string Suffix = "-censored";
        private const string BoardPictures = "Cards/List/135x87/";
        private const string KeyArtPath = "Background/gwent_cover 1";

        private static bool on;

        // The setting changed: the images on screen show the other version
        public static event Action Changed;

        public static bool On { get { return GameSettings.CensoredArt; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            on = GameSettings.CensoredArt;
            GameSettings.Changed -= Check;
            GameSettings.Changed += Check;
        }

        private static void Check()
        {
            if (GameSettings.CensoredArt == on)
                return;
            on = GameSettings.CensoredArt;
            Changed?.Invoke();
        }

        // A card's board picture (on the rows, in the hand, on the rules page)
        public static Sprite BoardPicture(int card_id)
        {
            return Pick(BoardPictures + card_id);
        }

        // The menu's key art (the main menu, the menu's loading screen)
        public static Sprite KeyArt()
        {
            return Pick(KeyArtPath);
        }

        private static Sprite Pick(string path)
        {
            if (On)
            {
                Sprite censored = SpriteCache.Load(path + Suffix);
                if (censored != null)
                    return censored;
            }
            return SpriteCache.Load(path);
        }
    }
}
