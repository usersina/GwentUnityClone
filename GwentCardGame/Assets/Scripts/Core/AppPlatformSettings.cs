using UnityEngine;

namespace Gwent.Core
{
    // The phone app stays in landscape, either way up
    public static class AppPlatformSettings
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            // Only the Android app: in a browser it becomes screen.orientation.lock(), which browsers refuse (an error in
            // the console) unless the page is fullscreen on a phone
            if (Application.platform != RuntimePlatform.Android)
                return;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation; // either landscape side, as the phone is turned
        }
    }
}
