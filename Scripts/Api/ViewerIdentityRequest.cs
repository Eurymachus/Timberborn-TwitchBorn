namespace TwitchBorn.Api
{
    public class ViewerIdentityRequest
    {
        public string Source { get; private set; }
        public string SourceUserId { get; private set; }
        public string LoginName { get; private set; }
        public string DisplayName { get; private set; }

        public ViewerIdentityRequest(
            string source,
            string sourceUserId,
            string loginName,
            string displayName)
        {
            Source = source ?? "";
            SourceUserId = sourceUserId ?? "";
            LoginName = loginName ?? "";
            DisplayName = displayName ?? "";
        }
    }
}