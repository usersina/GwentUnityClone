using System.Diagnostics;

namespace Gwent.Core
{
    // Verbose gameplay logging: compiled out of release builds (calls and their arguments disappear).
    // DEBUG is the debug managed-code variant of Unity 6.6, which replaces DEVELOPMENT_BUILD.
    // Errors and warnings still go through Debug.LogError / Debug.LogWarning directly.
    public static class GameLog
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEBUG"), Conditional("GWENT_VERBOSE_LOGS")]
        public static void Info(object message)
        {
            UnityEngine.Debug.Log(message);
        }
    }
}
