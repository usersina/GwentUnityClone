using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The player's settings, saved in PlayerPrefs and applied as soon as they change: audio (applied by GameAudio)
// and display. Window mode and resolution are only applied once the player has changed them, so Unity's own
// window handling stays in charge until then.
public static class GameSettings
{
    private const string MasterVolumeKey = "masterVolume";
    private const string MusicVolumeKey = "musicVolume"; // same keys as the old audio buttons, so saved volumes carry over
    private const string SfxVolumeKey = "sfxVolume";
    private const string MuteInBackgroundKey = "muteInBackground";
    private const string FullscreenKey = "fullscreen";
    private const string ResolutionWidthKey = "resolutionWidth";
    private const string ResolutionHeightKey = "resolutionHeight";
    private const string FrameRateKey = "frameRate";
    private const string MenuScaleKey = "menuScale";
    private const string GameSpeedKey = "gameSpeed";

    // Frame rate choices: VSync follows the screen, 0 is unlimited (desktop); phones have no VSync choice
    public const int VSync = -1;
    public static readonly int[] DesktopFrameRates = { VSync, 30, 60, 120, 144, 0 };
    public static readonly int[] MobileFrameRates = { 30, 60, 120 };
    public static readonly float[] MenuScales = { 0.9f, 1f, 1.15f, 1.3f };
    public static readonly string[] GameSpeedNames = { "Slow", "Normal", "Fast" };
    private static readonly float[] GameSpeedScales = { 0.75f, 1f, 3f };

    // Raised after any change (GameAudio listens; the overlay canvas reads the menu size every frame)
    public static event Action Changed;

    public static bool IsMobile { get { return Application.isMobilePlatform; } }

    //----------------------------------------Audio (0..1)----------------------------------------//
    public static float MasterVolume
    {
        get { return PlayerPrefs.GetFloat(MasterVolumeKey, 1f); }
        set { SetFloat(MasterVolumeKey, Mathf.Clamp01(value)); }
    }

    public static float MusicVolume
    {
        get { return PlayerPrefs.GetFloat(MusicVolumeKey, 1f); }
        set { SetFloat(MusicVolumeKey, Mathf.Clamp01(value)); }
    }

    public static float SfxVolume
    {
        get { return PlayerPrefs.GetFloat(SfxVolumeKey, 1f); }
        set { SetFloat(SfxVolumeKey, Mathf.Clamp01(value)); }
    }

    public static bool MuteInBackground
    {
        get { return PlayerPrefs.GetInt(MuteInBackgroundKey, 1) == 1; }
        set { SetInt(MuteInBackgroundKey, value ? 1 : 0); }
    }

    //----------------------------------------Display----------------------------------------//
    public static bool Fullscreen
    {
        get { return PlayerPrefs.HasKey(FullscreenKey) ? PlayerPrefs.GetInt(FullscreenKey) == 1 : Screen.fullScreenMode != FullScreenMode.Windowed; }
        set
        {
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            Saved(ApplyWindow);
        }
    }

    public static Vector2Int Resolution
    {
        get
        {
            int width = PlayerPrefs.GetInt(ResolutionWidthKey, 0);
            int height = PlayerPrefs.GetInt(ResolutionHeightKey, 0);
            return width > 0 && height > 0 ? new Vector2Int(width, height) : new Vector2Int(Screen.width, Screen.height);
        }
        set
        {
            PlayerPrefs.SetInt(ResolutionWidthKey, value.x);
            PlayerPrefs.SetInt(ResolutionHeightKey, value.y);
            Saved(ApplyWindow);
        }
    }

    // The screen's sizes, largest first (at least 1024x576), always including the current one
    public static List<Vector2Int> AvailableResolutions()
    {
        List<Vector2Int> sizes = Screen.resolutions
            .Select(r => new Vector2Int(r.width, r.height))
            .Where(r => r.x >= 1024 && r.y >= 576)
            .ToList();
        sizes.Add(Resolution);
        return sizes.Distinct().OrderByDescending(r => r.x * r.y).ThenByDescending(r => r.x).ToList();
    }

    public static int[] FrameRates { get { return IsMobile ? MobileFrameRates : DesktopFrameRates; } }

    public static int FrameRate
    {
        get { return PlayerPrefs.GetInt(FrameRateKey, IsMobile ? 60 : VSync); }
        set
        {
            PlayerPrefs.SetInt(FrameRateKey, value);
            Saved(ApplyFrameRate);
        }
    }

    public static string FrameRateLabel(int frame_rate)
    {
        return frame_rate == VSync ? "VSync" : frame_rate == 0 ? "Unlimited" : frame_rate + " FPS";
    }

    // Size of the menus and dialogs (the battle board and the deck builder always fit the screen)
    public static float MenuScale
    {
        get { return PlayerPrefs.GetFloat(MenuScaleKey, 1f); }
        set { SetFloat(MenuScaleKey, value); }
    }

    //----------------------------------------Game----------------------------------------//
    // How fast a battle runs: the cards' moves and effects, the notices and the opponent's turns (0 slow, 1 normal,
    // 2 fast). Clicks and holds keep their real-time feel.
    public static int GameSpeed
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt(GameSpeedKey, 1), 0, GameSpeedScales.Length - 1); }
        set { SetInt(GameSpeedKey, Mathf.Clamp(value, 0, GameSpeedScales.Length - 1)); }
    }

    // The battle's time scale at that speed
    public static float GameTimeScale { get { return GameSpeedScales[GameSpeed]; } }

    //----------------------------------------Apply / reset----------------------------------------//
    public static void ResetToDefaults()
    {
        bool window_was_set = WindowSet;
        foreach (string key in new[] { MasterVolumeKey, MusicVolumeKey, SfxVolumeKey, MuteInBackgroundKey, FullscreenKey,
                                       ResolutionWidthKey, ResolutionHeightKey, FrameRateKey, MenuScaleKey, GameSpeedKey })
            PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        ApplyFrameRate();
        // Back to the game's default window: fullscreen at the screen's own resolution
        if (window_was_set && CanResizeWindow)
            Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
        RaiseChanged();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyAtStartup()
    {
        ApplyFrameRate();
        if (WindowSet)
            ApplyWindow();
    }

    private static bool WindowSet { get { return PlayerPrefs.HasKey(FullscreenKey) || PlayerPrefs.HasKey(ResolutionWidthKey); } }

    // The editor's Game view and phones have no window to resize
    private static bool CanResizeWindow { get { return !IsMobile && !Application.isEditor; } }

    private static void ApplyFrameRate()
    {
        int frame_rate = FrameRate;
        QualitySettings.vSyncCount = frame_rate == VSync && !IsMobile ? 1 : 0;
        Application.targetFrameRate = frame_rate > 0 ? frame_rate : -1;
    }

    private static void ApplyWindow()
    {
        if (!CanResizeWindow)
            return;
        FullScreenMode mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Vector2Int size = Resolution;
        if (Screen.width != size.x || Screen.height != size.y || Screen.fullScreenMode != mode)
            Screen.SetResolution(size.x, size.y, mode);
    }

    private static void SetFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        Saved(null);
    }

    private static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        Saved(null);
    }

    // Written to disk when the settings close (a slider changes the value every frame while dragged)
    private static void Saved(Action apply)
    {
        if (apply != null)
            apply();
        RaiseChanged();
    }

    private static void RaiseChanged()
    {
        if (Changed != null)
            Changed();
    }
}
