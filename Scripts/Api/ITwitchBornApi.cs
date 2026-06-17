using TwitchBorn.Models;

namespace TwitchBorn.Api
{
    public interface ITwitchBornApi
    {
        ViewerIdentity CreateViewerIdentity(
            string source,
            string sourceUserId,
            string loginName,
            string displayName);
        ViewerIdentity CreateViewerIdentity(ViewerIdentityRequest request);

        BeaverCommandResult ClaimCharacter(ViewerIdentity viewer);
        BeaverCommandResult GetCharacterStatus(ViewerIdentity viewer);
        BeaverCommandResult RenameCharacter(ViewerIdentity viewer, string requestedName);
        BeaverCommandResult SetViewerNameColour(ViewerIdentity viewer, string requestedColour);
        BeaverCommandResult SetViewerNameShadow(ViewerIdentity viewer, string requestedColour);
        BeaverCommandResult SendSpeech(ViewerIdentity viewer, string message);
    }
}