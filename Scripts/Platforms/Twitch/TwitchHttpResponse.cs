namespace TwitchBorn.Platforms.Twitch
{
    public class TwitchHttpResponse
    {
        public int StatusCode { get; private set; }
        public string Body { get; private set; }

        public TwitchHttpResponse(int statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body ?? "";
        }

        public bool IsSuccess
        {
            get
            {
                return StatusCode >= 200 && StatusCode < 300;
            }
        }
    }
}