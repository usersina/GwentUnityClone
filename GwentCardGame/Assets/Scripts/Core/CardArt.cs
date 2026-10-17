using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Gwent.Rules;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

namespace Gwent.Core
{
    // The cards' full art (380x592, the whole card: the previews, the showcase, the pickers, the deck builder), from
    // Addressables at "CardArt/<id>", a bundle per card (see GwentContent). A card's art is loaded the first time it is
    // shown, and released once no open scene has shown it. Until it is there the image shows the back of the card's
    // faction: nothing waits for it, and on the web a card turns face up as its art arrives. A battle asks for both
    // decks' art as it starts (Preload), so its cards are face up by the time they are seen.
    public static class CardArt
    {
        private class Entry
        {
            public AsyncOperationHandle<Sprite> handle;
            public readonly HashSet<Scene> scenes = new HashSet<Scene>(); // the open scenes that asked for it
        }

        private static readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        private static readonly Dictionary<object, int> waiting = new Dictionary<object, int>(); // the card each image or element waits for

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            entries.Clear();
            waiting.Clear();
            SceneManager.sceneUnloaded -= Unloaded;
            SceneManager.sceneUnloaded += Unloaded;
        }

        public static string Address(int card_id)
        {
            return "CardArt/" + card_id;
        }

        // Shows the card's art on the image: at once when it is loaded, otherwise the card's back until it is
        public static void Show(Image image, int card_id)
        {
            Show(image, image.gameObject.scene, card_id, sprite => image.sprite = sprite, () => image != null);
        }

        // The same as the background of a UI Toolkit element
        public static void Show(VisualElement element, int card_id)
        {
            // Gone with its dialog (the lobby closed as the art came): not shown
            Show(element, SceneManager.GetActiveScene(), card_id, sprite => element.style.backgroundImage = new StyleBackground(sprite), () => element.panel != null);
        }

        private static void Show(object target, Scene scene, int card_id, Action<Sprite> show, Func<bool> alive)
        {
            Entry entry = Request(card_id, scene);
            if (entry.handle.IsDone)
            {
                waiting.Remove(target);
                show(entry.handle.Status == AsyncOperationStatus.Succeeded ? entry.handle.Result : Back(card_id));
                return;
            }
            show(Back(card_id));
            waiting[target] = card_id;
            entry.handle.Completed += loaded =>
            {
                // The image may be gone, or show another card by now
                int wanted;
                if (!alive() || !waiting.TryGetValue(target, out wanted) || wanted != card_id)
                    return;
                waiting.Remove(target);
                if (loaded.Status == AsyncOperationStatus.Succeeded)
                    show(loaded.Result);
            };
        }

        // Whether an image or element on show still waits for its art (the loading screen waits for the next screen's)
        public static bool Waiting
        {
            get
            {
                foreach (object gone in waiting.Keys.Where(Gone).ToList())
                    waiting.Remove(gone);
                return waiting.Count > 0;
            }
        }

        // Loads the cards' art ahead of being shown (a battle's decks), for the scene open now. Completes once all of it
        // is there; a card whose art failed to load (Addressables logs why) keeps its back.
        public static async UniTask Preload(IEnumerable<int> card_ids, string what, CancellationToken ct)
        {
            Stopwatch watch = Stopwatch.StartNew();
            Scene scene = SceneManager.GetActiveScene();
            List<UniTask> loads = new List<UniTask>();
            int cards = 0;
            foreach (int card_id in card_ids.Distinct())
            {
                cards++;
                Entry entry = Request(card_id, scene);
                if (!entry.handle.IsDone)
                    loads.Add(Loaded(entry.handle, ct));
            }
            int loading = loads.Count;
            await UniTask.WhenAll(loads);
            GameLog.Event(LogCategory.Perf, "card_art_loaded", "for", what, "cards", cards, "loaded", loading, "ms", watch.ElapsedMilliseconds);
        }

        private static async UniTask Loaded(AsyncOperationHandle<Sprite> handle, CancellationToken ct)
        {
            try
            {
                await handle.ToUniTask(cancellationToken: ct);
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                // Addressables logged it already
            }
        }

        private static Entry Request(int card_id, Scene scene)
        {
            Entry entry;
            if (!entries.TryGetValue(card_id, out entry))
            {
                entry = new Entry { handle = Addressables.LoadAssetAsync<Sprite>(Address(card_id)) };
                entries[card_id] = entry;
            }
            entry.scenes.Add(scene);
            return entry;
        }

        // A scene closed: the art no other open scene asked for is released (its bundle unloads)
        private static void Unloaded(Scene scene)
        {
            foreach (int card_id in entries.Keys.ToList())
            {
                Entry entry = entries[card_id];
                entry.scenes.Remove(scene);
                if (entry.scenes.Count > 0)
                    continue;
                Addressables.Release(entry.handle);
                entries.Remove(card_id);
            }
            foreach (object gone in waiting.Keys.Where(Gone).ToList())
                waiting.Remove(gone);
        }

        // A destroyed image, or an element no longer on a panel
        private static bool Gone(object target)
        {
            VisualElement element = target as VisualElement;
            if (element != null)
                return element.panel == null;
            return target as UnityEngine.Object == null; // Unity's null: destroyed
        }

        // The back of the card's faction (special cards have the plain one)
        private static Sprite Back(int card_id)
        {
            CardDatabase db = CardLibrary.Get();
            Sprite back = db.Contains(card_id) ? SpriteCache.Load("Cards/Back/" + db.Get(card_id).faction.Code()) : null;
            return back != null ? back : SpriteCache.Load("Cards/Back/BACK");
        }
    }
}
