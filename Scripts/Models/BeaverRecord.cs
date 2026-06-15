using System;

namespace TwitchBorn.Models
{
    public class BeaverRecord
    {
        // TwitchBorn's own stable record ID for this beaver assignment.
        // This is separate from Timberborn's EntityId so the record can survive rerolls/history later.
        public Guid BeaverRecordId { get; set; }

        // The unique Timberborn entity ID for the assigned beaver.
        // This is how we find the actual live in-game beaver.
        public Guid EntityId { get; set; }

        // The beaver's name before TwitchBorn claimed/renamed it.
        // Used later for reroll/unclaim/restore behaviour.
        public string OriginalName { get; set; }

        // The name TwitchBorn currently expects this beaver to have.
        // For normal !beaver claims, this starts as the viewer display name.
        // Later, !rename can make this a custom beaver name.
        public string AssignedName { get; set; }

        // UTC timestamp string for when this beaver was first assigned.
        public string AssignedAtUtc { get; set; }

        // UTC timestamp string for when this beaver record was last resolved or validated.
        public string LastSeenAtUtc { get; set; }

        // Number of times TwitchBorn has renamed this beaver.
        public int RenameCount { get; set; }

        // Number of times this beaver record has been rerolled/reassigned.
        public int RerollCount { get; set; }

        // Whether this record currently points to an active/alive beaver.
        public bool IsActive { get; set; }
    }
}