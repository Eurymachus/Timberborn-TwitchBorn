using System;

namespace TwitchBorn.Models
{
    public class BeaverEntityHistoryEntry
    {
        public Guid EntityId { get; set; }
        public string Reason { get; set; }
        public string NameAtTime { get; set; }
        public string RecordedAtUtc { get; set; }
        public int AgeAtTime { get; set; }
    }
}
