using TwitchBorn.Models;
using TwitchBorn.Services;

namespace TwitchBorn.Api
{
    public class TwitchBornApi : ITwitchBornApi
    {
        private readonly PlatformRequestService _platformRequestService;

        public TwitchBornApi(PlatformRequestService platformRequestService)
        {
            _platformRequestService = platformRequestService;
        }

        public ViewerIdentity CreateViewerIdentity(
            string source,
            string sourceUserId,
            string loginName,
            string displayName)
        {
            return new ViewerIdentity(
                source,
                sourceUserId,
                loginName,
                displayName);
        }

        public ViewerIdentity CreateViewerIdentity(ViewerIdentityRequest request)
        {
            if (request == null)
            {
                return new ViewerIdentity("", "", "", "");
            }

            return new ViewerIdentity(
                request.Source,
                request.SourceUserId,
                request.LoginName,
                request.DisplayName);
        }

        public BeaverCommandResult ClaimCharacter(ViewerIdentity viewer)
        {
            return _platformRequestService.HandleBeaverClaim(viewer);
        }

        public BeaverCommandResult GetCharacterStatus(ViewerIdentity viewer)
        {
            return _platformRequestService.HandleBeaverStatus(viewer);
        }

        public BeaverCommandResult RenameCharacter(ViewerIdentity viewer, string requestedName)
        {
            return _platformRequestService.HandleBeaverRename(viewer, requestedName);
        }

        public BeaverCommandResult SetViewerNameColour(ViewerIdentity viewer, string requestedColour)
        {
            return _platformRequestService.HandleViewerNameColour(viewer, requestedColour);
        }

        public BeaverCommandResult SetViewerNameShadow(ViewerIdentity viewer, string requestedColour)
        {
            return _platformRequestService.HandleViewerNameShadow(viewer, requestedColour);
        }

        public BeaverCommandResult SendSpeech(ViewerIdentity viewer, string message)
        {
            var statusResult = _platformRequestService.HandleBeaverStatus(viewer);

            if (statusResult == null || !statusResult.HasBeaver)
            {
                return statusResult;
            }

            if (!_platformRequestService.HandleBeaverSpeech(viewer, message))
            {
                return new BeaverCommandResult(
                    BeaverCommandResultType.NoClaimedBeaver,
                    viewer,
                    null,
                    "",
                    "");
            }

            return statusResult;
        }
    }
}