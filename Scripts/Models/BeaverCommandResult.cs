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
        public int BeaverAge { get; private set; }
        public string PreviousBeaverName { get; private set; }
        public string DeathReason { get; private set; }
        public bool RequiresExplicitClaim { get; private set; }
        public bool WasAutoClaimed { get; private set; }
        public bool WasQueued { get; private set; }

        public BeaverCommandResult(
            BeaverCommandResultType type,
            ViewerIdentity viewer,
            Character beaver,
            string beaverName,
            string beaverStatus)
            : this(type, viewer, beaver, beaverName, beaverStatus, "", false, false, false)
        {
        }

        public BeaverCommandResult(
            BeaverCommandResultType type,
            ViewerIdentity viewer,
            Character beaver,
            string beaverName,
            string beaverStatus,
            string previousBeaverName,
            bool requiresExplicitClaim,
            bool wasAutoClaimed,
            bool wasQueued,
            string deathReason = "")
        {
            Type = type;
            Viewer = viewer;
            Beaver = beaver;
            BeaverName = beaverName ?? "";
            BeaverStatus = beaverStatus ?? "";
            BeaverAge = beaver == null ? -1 : beaver.Age;
            PreviousBeaverName = previousBeaverName ?? "";
            DeathReason = deathReason ?? "";
            RequiresExplicitClaim = requiresExplicitClaim;
            WasAutoClaimed = wasAutoClaimed;
            WasQueued = wasQueued;
        }

        public bool HasBeaver
        {
            get
            {
                return Beaver != null;
            }
        }

        public bool HasKnownBeaverAge
        {
            get
            {
                return BeaverAge >= 0;
            }
        }

        public bool HasDeathReason
        {
            get
            {
                return !string.IsNullOrEmpty(DeathReason);
            }
        }
    }
}
