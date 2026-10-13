using UnityEngine;

public static class AppPlatformSettings
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.AutoRotation; // either landscape side, as the phone is turned
    }
}
