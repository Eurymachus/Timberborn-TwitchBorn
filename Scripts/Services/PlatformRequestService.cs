using Timberborn.Characters;
using Timberborn.Localization;
using TwitchBorn.Core;
using TwitchBorn.Models;
using TwitchBorn.Registry;

namespace TwitchBorn.Services
{
    public class PlatformRequestService
    {
        private const string ClaimedByLocKey = "Eurymachus.TwitchBorn.Overlay.ClaimedBy";
        private const string RenamedToLocKey = "Eurymachus.TwitchBorn.Overlay.RenamedTo";

        private readonly BeaverRegistry _beaverRegistry;
        private readonly BeaverOverlayService _beaverOverlayService;
        private readonly BeaverStatusTextProvider _beaverStatusTextProvider;
        private readonly ILoc _loc;

        public PlatformRequestService(
            BeaverRegistry beaverRegistry,
            BeaverOverlayService beaverOverlayService,
            BeaverStatusTextProvider beaverStatusTextProvider,
            ILoc loc)
        {
            _beaverRegistry = beaverRegistry;
            _beaverOverlayService = beaverOverlayService;
            _beaverStatusTextProvider = beaverStatusTextProvider;
            _loc = loc;

            _beaverRegistry.PendingClaimAssigned += OnPendingClaimAssigned;
        }

        public bool HandleBeaverSpeech(ViewerIdentity viewer, string message)
        {
            if (viewer == null)
            {
                TwitchBornLog.Info("Beaver speech ignored because viewer was null.");
                return false;
            }

            var beaver = _beaverRegistry.TryGetBeaver(viewer);

            if (beaver == null)
            {
                TwitchBornLog.Info("Beaver speech ignored because no beaver is registered for " + viewer.SafeDisplayName);
                return false;
            }

            _beaverRegistry.RecordChatMessage(viewer);

            var result = CreateResult(
                BeaverCommandResultType.Status,
                viewer,
                beaver);

            _beaverOverlayService.ShowMessage(
                beaver,
                result.BeaverName,
                GetPlainViewerName(viewer),
                message ?? "");

            return true;
        }

        public BeaverCommandResult HandleBeaverClaim(ViewerIdentity viewer)
        {
            if (viewer == null || !viewer.IsValid)
            {
                return new BeaverCommandResult(
                    BeaverCommandResultType.InvalidViewer,
                    viewer,
                    null,
                    "",
                    "");
            }

            var existingBeaver = _beaverRegistry.TryGetBeaver(viewer);

            if (existingBeaver != null)
            {
                return CreateResult(
                    BeaverCommandResultType.AlreadyClaimed,
                    viewer,
                    existingBeaver);
            }

            if (_beaverRegistry.IsPendingClaim(viewer))
            {
                return new BeaverCommandResult(
                    BeaverCommandResultType.AlreadyQueued,
                    viewer,
                    null,
                    "",
                    "");
            }

            var claimedBeaver = _beaverRegistry.ClaimBeaver(viewer);

            if (claimedBeaver != null)
            {
                var result = CreateResult(
                    BeaverCommandResultType.Claimed,
                    viewer,
                    claimedBeaver);

                _beaverOverlayService.ShowMessage(
                    claimedBeaver,
                    result.BeaverName,
                    GetPlainViewerName(viewer),
                    _loc.T(ClaimedByLocKey, GetPlainViewerName(viewer)));

                return result;
            }

            if (_beaverRegistry.TryEnqueuePendingClaim(viewer))
            {
                return new BeaverCommandResult(
                    BeaverCommandResultType.Queued,
                    viewer,
                    null,
                    "",
                    "");
            }

            return new BeaverCommandResult(
                BeaverCommandResultType.NoAvailableBeaver,
                viewer,
                null,
                "",
                "");
        }

        public BeaverCommandResult HandleBeaverStatus(ViewerIdentity viewer)
        {
            if (viewer == null || !viewer.IsValid)
            {
                return new BeaverCommandResult(
                    BeaverCommandResultType.InvalidViewer,
                    viewer,
                    null,
                    "",
                    "");
            }

            var beaver = _beaverRegistry.TryGetBeaver(viewer);

            if (beaver == null)
            {
                return new BeaverCommandResult(
                    BeaverCommandResultType.NoClaimedBeaver,
                    viewer,
                    null,
                    "",
                    "");
            }

            return CreateResult(
                BeaverCommandResultType.Status,
                viewer,
                beaver);
        }

        public BeaverCommandResult HandleBeaverRename(
            ViewerIdentity viewer,
            string requestedName)
        {
            if (viewer == null || !viewer.IsValid)
            {
                return new BeaverCommandResult(
                    BeaverCommandResultType.InvalidViewer,
                    viewer,
                    null,
                    "",
                    "");
            }

            string safeName;
            var beaver = _beaverRegistry.RenameBeaver(
                viewer,
                requestedName,
                out safeName);

            if (string.IsNullOrEmpty(safeName))
            {
                return new BeaverCommandResult(
                    BeaverCommandResultType.InvalidRequestedName,
                    viewer,
                    null,
                    "",
                    "");
            }

            if (beaver == null)
            {
                return new BeaverCommandResult(
                    BeaverCommandResultType.NoClaimedBeaver,
                    viewer,
                    null,
                    TwitchBornTextSanitizer.SanitizePlainText(safeName, 32),
                    "");
            }

            var result = CreateResult(
                BeaverCommandResultType.Renamed,
                viewer,
                beaver);

            _beaverOverlayService.ShowMessage(
                beaver,
                result.BeaverName,
                GetPlainViewerName(viewer),
                _loc.T(RenamedToLocKey, result.BeaverName));

            return result;
        }

        public Character DebugClaimViewerBeaver(ViewerIdentity viewer)
        {
            return _beaverRegistry.ClaimBeaver(viewer);
        }

        private void OnPendingClaimAssigned(BeaverCommandResult result)
        {
            if (result == null || result.Beaver == null || result.Viewer == null)
            {
                return;
            }

            _beaverOverlayService.ShowMessage(
                result.Beaver,
                result.BeaverName,
                GetPlainViewerName(result.Viewer),
                _loc.T(ClaimedByLocKey, GetPlainViewerName(result.Viewer)));
        }

        private static string GetPlainViewerName(ViewerIdentity viewer)
        {
            if (viewer == null)
            {
                return "viewer";
            }

            var plainName = TwitchBornTextSanitizer.SanitizePlainText(viewer.SafeDisplayName, 64);
            return string.IsNullOrEmpty(plainName) ? "viewer" : plainName;
        }

        private BeaverCommandResult CreateResult(
            BeaverCommandResultType type,
            ViewerIdentity viewer,
            Character beaver)
        {
            return new BeaverCommandResult(
                type,
                viewer,
                beaver,
                _beaverStatusTextProvider.GetBeaverName(beaver),
                _beaverStatusTextProvider.GetBeaverStatus(beaver));
        }
    }
}