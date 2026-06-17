using System;

namespace TwitchBorn.Models
{
    public class ViewerRecord
    {
        // Source platform for this viewer.
        // Examples: "Twitch", "Debug", future "YouTube".
        public string Source { get; set; }

        // Stable platform-specific user ID.
        // For Twitch this should be the Twitch user ID, not display name.
        public string SourceUserId { get; set; }

        // The user's current/latest display name.
        // Example: "Eurymachus".
        public string DisplayName { get; set; }

        // Link to the beaver record assigned to this viewer.
        // Guid.Empty means no beaver is currently assigned.
        public Guid BeaverRecordId { get; set; }

        // Whether this viewer currently has a living assigned claim.
        // This is viewer claim state and remains useful even after BeaverRecordId is cleared by death.
        public bool HasLivingClaim { get; set; }

        // Last assigned clean beaver name used by this viewer.
        // Used when Keep Assigned Name Across Claims is enabled.
        public string LastAssignedBeaverName { get; set; }

        // Last dead beaver name for viewer-facing context after death.
        // This lets status commands explain what happened even when death notifications were disabled or missed.
        public string LastDeadBeaverName { get; set; }

        // Whether the viewer was placed back into the claim queue because their beaver died.
        public bool QueuedAfterDeath { get; set; }

        // Optional per-viewer overlay/name colour in #RRGGBB format.
        // This is viewer style data, not part of the beaver's assigned name.
        public string NameColorHex { get; set; }

        // Optional per-viewer nameplate shadow colour in #RRGGBB or #RRGGBBAA format.
        // Empty means use the default overlay shadow colour.
        public string NameShadowColorHex { get; set; }

        // UTC timestamp string for when this viewer was first seen by TwitchBorn.
        public string FirstSeenAtUtc { get; set; }

        // UTC timestamp string for when this viewer last interacted with TwitchBorn.
        public string LastSeenAtUtc { get; set; }

        // Number of chat messages processed for this viewer.
        public int ChatMessageCount { get; set; }
    }
}