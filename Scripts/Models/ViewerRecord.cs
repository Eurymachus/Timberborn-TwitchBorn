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