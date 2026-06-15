namespace TwitchBorn.Core
{
    public static class TwitchBornLog
    {
        private const string Prefix = "[TwitchBorn] ";

        public static void Info(string message)
        {
            UnityEngine.Debug.Log(Prefix + message);
        }

        public static void Warning(string message)
        {
            UnityEngine.Debug.LogWarning(Prefix + message);
        }

        public static void Error(string message)
        {
            UnityEngine.Debug.LogError(Prefix + message);
        }
    }
}