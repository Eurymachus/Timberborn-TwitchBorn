using System;

namespace TwitchBorn.Platforms.Twitch
{
    [Serializable]
    public class TwitchValidatedToken
    {
        public string client_id;
        public string login;
        public string user_id;
        public string[] scopes;
        public int expires_in;
    }
}