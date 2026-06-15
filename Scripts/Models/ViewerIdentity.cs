namespace TwitchBorn.Models
{
    public class ViewerIdentity
    {
        public const string DebugSource = "Debug";
        public const string TwitchSource = "Twitch";

        public string Source { get; private set; }
        public string SourceUserId { get; private set; }
        public string LoginName { get; private set; }
        public string DisplayName { get; private set; }

        public ViewerIdentity(
            string source,
            string sourceUserId,
            string loginName,
            string displayName)
        {
            Source = Sanitize(source, 64);
            SourceUserId = Sanitize(sourceUserId, 128);
            LoginName = Sanitize(loginName, 64);
            DisplayName = Sanitize(displayName, 64);
        }

        public static ViewerIdentity FromDebug(string displayName)
        {
            var safeDisplayName = Sanitize(displayName, 64);
            var loginName = Normalize(safeDisplayName);

            return new ViewerIdentity(
                DebugSource,
                loginName,
                loginName,
                safeDisplayName);
        }

        public static ViewerIdentity FromTwitch(
            string twitchUserId,
            string loginName,
            string displayName)
        {
            return new ViewerIdentity(
                TwitchSource,
                twitchUserId,
                Normalize(loginName),
                Sanitize(displayName, 64));
        }

        public string SafeDisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(DisplayName))
                {
                    return DisplayName;
                }

                if (!string.IsNullOrEmpty(LoginName))
                {
                    return LoginName;
                }

                return SourceUserId;
            }
        }

        public bool IsValid
        {
            get
            {
                return !string.IsNullOrEmpty(Source) && !string.IsNullOrEmpty(SourceUserId);
            }
        }

        public static string CreateViewerKey(ViewerIdentity viewer)
        {
            if (viewer == null)
            {
                return "";
            }

            return CreateViewerKey(viewer.Source, viewer.SourceUserId);
        }

        public static string CreateViewerKey(string source, string sourceUserId)
        {
            return Normalize(source) + ":" + Normalize(sourceUserId);
        }

        private static string Normalize(string value)
        {
            return Sanitize(value, 128).ToLowerInvariant();
        }

        private static string Sanitize(string value, int maxLength)
        {
            if (value == null)
            {
                return "";
            }

            var sanitized = value.Trim();

            sanitized = sanitized.Replace("\r", "");
            sanitized = sanitized.Replace("\n", "");
            sanitized = sanitized.Replace("\t", " ");

            if (sanitized.Length > maxLength)
            {
                sanitized = sanitized.Substring(0, maxLength);
            }

            return sanitized;
        }
    }
}