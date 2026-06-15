using Timberborn.Characters;

namespace TwitchBorn.Models
{
    public class BeaverCommandResult
    {
        public BeaverCommandResultType Type { get; private set; }
        public ViewerIdentity Viewer { get; private set; }
        public Character Beaver { get; private set; }
        public string BeaverName { get; private set; }
        public string BeaverStatus { get; private set; }

        public BeaverCommandResult(
            BeaverCommandResultType type,
            ViewerIdentity viewer,
            Character beaver,
            string beaverName,
            string beaverStatus)
        {
            Type = type;
            Viewer = viewer;
            Beaver = beaver;
            BeaverName = beaverName ?? "";
            BeaverStatus = beaverStatus ?? "";
        }

        public bool HasBeaver
        {
            get
            {
                return Beaver != null;
            }
        }
    }
}