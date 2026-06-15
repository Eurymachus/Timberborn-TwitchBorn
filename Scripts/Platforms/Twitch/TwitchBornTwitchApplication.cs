namespace TwitchBorn.Platforms.Twitch
{
    public static class TwitchBornTwitchApplication
    {
        public const string ClientId = "ko5bsohlu97c8wd6abn3x2snvh8a31";

        public static readonly string[] Scopes =
        {
            "chat:read",
            "chat:edit"
        };

        public static string ScopeString
        {
            get
            {
                return string.Join(" ", Scopes);
            }
        }
    }
}