using Timberborn.Characters;

namespace TwitchBorn.Models
{
    public class ClaimedBeaverOverlayTarget
    {
        public Character Character { get; private set; }
        public string BeaverName { get; private set; }
        public string ViewerName { get; private set; }

        public ClaimedBeaverOverlayTarget(
            Character character,
            string beaverName,
            string viewerName)
        {
            Character = character;
            BeaverName = beaverName ?? "";
            ViewerName = viewerName ?? "";
        }
    }
}