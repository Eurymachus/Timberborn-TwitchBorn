using System;

namespace TwitchBorn.Platforms.Twitch
{
    [Serializable]
    public class TwitchAuthState
    {
        public string accessToken;
        public string refreshToken;
        public string expiresAtUtc;
        public string botLogin;
        public string botUserId;
        public string scopesCsv;

        public bool HasAccessToken
        {
            get
            {
                return !string.IsNullOrEmpty(accessToken);
            }
        }

        public bool HasRefreshToken
        {
            get
            {
                return !string.IsNullOrEmpty(refreshToken);
            }
        }
    }
}