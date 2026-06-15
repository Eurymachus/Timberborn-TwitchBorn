using System;

namespace TwitchBorn.Platforms.Twitch
{
    [Serializable]
    public class TwitchUserResponse
    {
        public string id;
        public string login;
        public string display_name;
    }
}