namespace TwitchBorn.Models
{
    public class TwitchRequestMatch
    {
        public PlatformRequestType RequestType { get; private set; }
        public string Arguments { get; private set; }

        public TwitchRequestMatch(
            PlatformRequestType requestType,
            string arguments)
        {
            RequestType = requestType;
            Arguments = arguments ?? "";
        }
    }
}