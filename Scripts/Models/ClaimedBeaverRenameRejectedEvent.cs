namespace TwitchBorn.Models
{
    public class ClaimedBeaverRenameRejectedEvent
    {
        public string BeaverName { get; private set; }
        public string ViewerName { get; private set; }

        public ClaimedBeaverRenameRejectedEvent(
            string beaverName,
            string viewerName)
        {
            BeaverName = beaverName ?? "";
            ViewerName = viewerName ?? "";
        }
    }
}