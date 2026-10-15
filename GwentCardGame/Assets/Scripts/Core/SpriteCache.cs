using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gwent.Core
{
    // Resources.Load<Sprite> with a per-scene cache: card objects are re-created on every board refresh
    // and each one used to load the same handful of sprites again.
    public static class SpriteCache
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            cache.Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Let the previous scene's textures be unloaded
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
                cache.Clear();
        }

        // null for a path with no sprite, remembered as such (not looked for again each time)
        public static Sprite Load(string path)
        {
            Sprite sprite;
            if (cache.TryGetValue(path, out sprite) && (ReferenceEquals(sprite, null) || sprite != null))
                return sprite; // known, or known to be missing (a sprite destroyed since is loaded again)
            sprite = Resources.Load<Sprite>(path);
            cache[path] = sprite;
            return sprite;
        }
    }
}
