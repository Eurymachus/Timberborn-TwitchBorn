namespace TwitchBorn.Models
{
    public enum BeaverCommandResultType
    {
        InvalidViewer,
        Claimed,
        AlreadyClaimed,
        Queued,
        AlreadyQueued,
        AssignedFromQueue,
        NoAvailableBeaver,
        Status,
        Renamed,
        NoClaimedBeaver,
        InvalidRequestedName,
        ViewerNameColourUpdated,
        ViewerNameColourCleared,
        ViewerNameShadowUpdated,
        ViewerNameShadowCleared,
        InvalidRequestedColour,

        PreviousClaimDied,
        ReclaimedAfterDeath,
        AlreadyQueuedAfterDeath,
        GrownUp,
        Died,
        AutoReclaimedAfterDeath,
        QueuedAfterDeath
    }
}