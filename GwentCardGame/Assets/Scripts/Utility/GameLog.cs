using System.Diagnostics;

// Verbose gameplay logging: compiled out of release builds (calls and their arguments disappear).
// Errors and warnings still go through Debug.LogError / Debug.LogWarning directly.
public static class GameLog
{
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD"), Conditional("GWENT_VERBOSE_LOGS")]
    public static void Info(object message)
    {
        UnityEngine.Debug.Log(message);
    }
}
