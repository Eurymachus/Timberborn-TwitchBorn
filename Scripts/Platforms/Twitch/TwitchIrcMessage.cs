using System.Collections.Generic;
using TwitchBorn.Models;

namespace TwitchBorn.Platforms.Twitch
{
    public class TwitchIrcMessage
    {
        public ViewerIdentity Viewer { get; private set; }
        public string Message { get; private set; }
        public Dictionary<string, string> Tags { get; private set; }

        public TwitchIrcMessage(
            ViewerIdentity viewer,
            string message,
            Dictionary<string, string> tags)
        {
            Viewer = viewer;
            Message = message ?? "";
            Tags = tags ?? new Dictionary<string, string>();
        }
    }
}