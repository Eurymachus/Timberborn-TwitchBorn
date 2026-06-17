using Timberborn.Characters;

namespace TwitchBorn.Models
{
    public class ClaimedBeaverOverlayTarget
    {
        public Character Character { get; private set; }
        public string BeaverName { get; private set; }
        public string ViewerName { get; private set; }
        public string NameColorHex { get; private set; }
        public string NameShadowColorHex { get; private set; }

        public ClaimedBeaverOverlayTarget(
            Character character,
            string beaverName,
            string viewerName,
            string nameColorHex,
            string nameShadowColorHex)
        {
            Character = character;
            BeaverName = beaverName ?? "";
            ViewerName = viewerName ?? "";
            NameColorHex = nameColorHex ?? "";
            NameShadowColorHex = nameShadowColorHex ?? "";
        }
    }
}