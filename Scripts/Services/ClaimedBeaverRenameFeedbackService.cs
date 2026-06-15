using Timberborn.Localization;
using Timberborn.QuickNotificationSystem;
using Timberborn.SingletonSystem;
using TwitchBorn.Models;

namespace TwitchBorn.Services
{
    public class ClaimedBeaverRenameFeedbackService : ILoadableSingleton
    {
        private const string RenameRejectedLocKey = "Eurymachus.TwitchBorn.Notification.RenameRejected";

        private readonly EventBus _eventBus;
        private readonly ILoc _loc;
        private readonly QuickNotificationService _quickNotificationService;

        public ClaimedBeaverRenameFeedbackService(
            EventBus eventBus,
            ILoc loc,
            QuickNotificationService quickNotificationService)
        {
            _eventBus = eventBus;
            _loc = loc;
            _quickNotificationService = quickNotificationService;
        }

        public void Load()
        {
            _eventBus.Register(this);
        }

        [OnEvent]
        public void OnClaimedBeaverRenameRejected(ClaimedBeaverRenameRejectedEvent rejectedEvent)
        {
            if (rejectedEvent == null)
            {
                return;
            }

            var viewerName = string.IsNullOrEmpty(rejectedEvent.ViewerName)
                ? rejectedEvent.BeaverName
                : rejectedEvent.ViewerName;

            var message = _loc.T(RenameRejectedLocKey, viewerName);

            _quickNotificationService.SendWarningNotification(message);
        }
    }
}