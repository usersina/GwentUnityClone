using System.Collections.Generic;
using UnityEngine;

namespace Gwent.Core
{
    public class GameAudio : MonoBehaviour
    {
        private const string MusicPath = "Audio/Music";
        private const string SfxPath = "Audio/Sfx";
        private const float MusicBaseVolume = 0.45f;
        private const float SfxBaseVolume = 0.9f;

        private static GameAudio instance;
        private static readonly Dictionary<string, string[]> SfxAliases = new Dictionary<string, string[]>
        {
            { "ui_click", new[] { "menu_buy" } },
            { "invalid", new[] { "warning" } },
            { "card_select", new[] { "card" } },
            { "card_draw", new[] { "card" } },
            { "card_discard", new[] { "discard" } },
            { "card_exchange", new[] { "redraw" } },
            { "card_play_unit", new[] { "common1", "common2", "common3" } },
            { "card_play_hero", new[] { "hero" } },
            { "card_play_special", new[] { "card" } },
            { "pass", new[] { "pass" } },
            { "round_end", new[] { "round1_start" } },
            { "round_win", new[] { "round_win" } },
            { "round_lose", new[] { "round_lose" } },
            { "victory", new[] { "game_win" } },
            { "defeat", new[] { "game_lose" } },
            { "game_start", new[] { "game_start" } },
            { "turn_player", new[] { "turn_me" } },
            { "turn_enemy", new[] { "turn_op" } },
            { "ability_spy", new[] { "spy" } },
            { "ability_medic", new[] { "med" } },
            { "ability_muster", new[] { "ally" } },
            { "ability_scorch", new[] { "scorch" } },
            { "ability_weather_frost", new[] { "cold" } },
            { "ability_weather_fog", new[] { "fog" } },
            { "ability_weather_rain", new[] { "rain" } },
            { "ability_weather_clear", new[] { "clear" } },
            { "ability_weather", new[] { "cold", "fog", "rain" } },
            { "ability_horn", new[] { "horn" } },
            { "ability_morale", new[] { "moral" } },
            { "ability_tight_bond", new[] { "ally" } },
            { "ability_decoy", new[] { "knockback" } },
            { "leader", new[] { "shield" } },
            { "deck_add", new[] { "menu_buy" } },
            { "deck_remove", new[] { "discard" } },
            { "deck_select", new[] { "card" } },
            { "deck_save", new[] { "game_buy" } },
        };

        private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
        private AudioClip[] musicClips;
        private AudioSource musicSource;
        private AudioSource sfxSource;
        private bool hasFocus = true;
        private bool musicWanted;
        private bool appPaused;
        private float lastTrackStart = float.NegativeInfinity;

        public static GameAudio Instance
        {
            get
            {
                if (instance == null)
                    CreateInstance();
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            CreateInstance();
        }

        private static void CreateInstance()
        {
            if (instance != null)
                return;

            GameObject audioObject = new GameObject("GameAudio");
            instance = audioObject.AddComponent<GameAudio>();
            DontDestroyOnLoad(audioObject);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            ConfigureSources();
            GameSettings.Changed += ApplyVolumes;
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= ApplyVolumes;
        }

        private void OnApplicationFocus(bool focus)
        {
            hasFocus = focus;
            ApplyVolumes();
        }

        // Phones suspend the app (and its audio): a stopped track then is not a finished one
        private void OnApplicationPause(bool paused)
        {
            appPaused = paused;
        }

        // A playlist: when a track ends, another one starts
        private void Update()
        {
            // (at most once a second, in case a track cannot be read)
            if (musicWanted && !appPaused && musicSource != null && (musicSource.clip == null || !musicSource.isPlaying)
                && Time.unscaledTime - lastTrackStart > 1f)
                PlayNextTrack();
        }

        private void ConfigureSources()
        {
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.loop = false; // the next track starts when this one ends
                musicSource.playOnAwake = false;
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.loop = false;
                sfxSource.playOnAwake = false;
            }
            ApplyVolumes();
        }

        //----------------------------------------Volumes (GameSettings)----------------------------------------//
        private void ApplyVolumes()
        {
            if (musicSource != null)
                musicSource.volume = MusicBaseVolume * GameSettings.MusicVolume;
            if (sfxSource != null)
                sfxSource.volume = SfxBaseVolume * GameSettings.SfxVolume;
            bool muted = !hasFocus && GameSettings.MuteInBackground && !Application.isMobilePlatform;
            AudioListener.volume = muted ? 0f : GameSettings.MasterVolume;
        }

        public static void PlayMenuMusic()
        {
            Instance.PlayAnyMusic();
        }

        public static void PlayGameMusic()
        {
            Instance.PlayAnyMusic();
        }

        public static void PlayDeckMusic()
        {
            Instance.PlayAnyMusic();
        }

        public static void PlaySfx(string clipName)
        {
            Instance.PlaySoundEffect(clipName, 1f);
        }

        public static void PlaySfx(string clipName, float volumeScale)
        {
            Instance.PlaySoundEffect(clipName, volumeScale);
        }

        private void PlayAnyMusic()
        {
            ConfigureSources();
            musicWanted = true;
            // isPlaying can read true on the very first frame although no track was ever set: check the clip too
            if (musicSource.clip == null || !musicSource.isPlaying)
                PlayNextTrack();
        }

        // A random track other than the one that just played. The tracks stream from disk (import settings), so
        // loading them all only creates their small handles
        private void PlayNextTrack()
        {
            if (musicClips == null)
                musicClips = Resources.LoadAll<AudioClip>(MusicPath);
            if (musicClips == null || musicClips.Length == 0)
                return;

            AudioClip previous = musicSource.clip;
            AudioClip clip = musicClips[Random.Range(0, musicClips.Length)];
            if (clip == previous && musicClips.Length > 1)
                clip = musicClips[(System.Array.IndexOf(musicClips, clip) + 1 + Random.Range(0, musicClips.Length - 1)) % musicClips.Length];
            musicSource.clip = clip;
            musicSource.Play();
            lastTrackStart = Time.unscaledTime;
        }

        private void PlaySoundEffect(string clipName, float volumeScale)
        {
            ConfigureSources();

            if (GameSettings.SfxVolume <= 0f || GameSettings.MasterVolume <= 0f)
                return;

            AudioClip clip = LoadSfxClip(clipName);
            if (clip == null)
                return;

            sfxSource.PlayOneShot(clip, volumeScale);
        }

        private AudioClip LoadSfxClip(string clipName)
        {
            string[] choices;
            if (!SfxAliases.TryGetValue(clipName, out choices))
                return LoadClip(SfxPath, clipName);

            if (choices.Length == 0)
                return null;

            int startIndex = Random.Range(0, choices.Length);
            for (int i = 0; i < choices.Length; i++)
            {
                string choice = choices[(startIndex + i) % choices.Length];
                AudioClip clip = LoadClip(SfxPath, choice);
                if (clip != null)
                    return clip;
            }

            return null;
        }

        private AudioClip LoadClip(string basePath, string clipName)
        {
            if (string.IsNullOrEmpty(clipName))
                return null;

            string resourcePath = basePath + "/" + clipName;
            if (clipCache.ContainsKey(resourcePath))
                return clipCache[resourcePath];

            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            clipCache[resourcePath] = clip;
            return clip;
        }
    }
}
