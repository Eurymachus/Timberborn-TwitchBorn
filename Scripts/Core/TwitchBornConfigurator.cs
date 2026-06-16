using Bindito.Core;
using TwitchBorn.Api;
using TwitchBorn.Debug;
using TwitchBorn.Platforms.Twitch;
using TwitchBorn.Registry;
using TwitchBorn.Services;

namespace TwitchBorn.Core
{
    [Context("Game")]
    public class TwitchBornConfigurator : Configurator
    {
        protected override void Configure()
        {
            Bind<TwitchBornInitializer>().AsSingleton();
            Bind<BeaverRegistry>().AsSingleton();
            Bind<DebugInputProvider>().AsSingleton();

            Bind<BeaverOverlayService>().AsSingleton();
            Bind<BeaverStatusTextProvider>().AsSingleton();
            Bind<ClaimedBeaverRenameFeedbackService>().AsSingleton();
            Bind<PlatformRequestService>().AsSingleton();

            Bind<ITwitchBornApi>().To<TwitchBornApi>().AsSingleton();

            Bind<TwitchTriggerMatcher>().AsSingleton();
            Bind<TwitchIrcService>().AsSingleton();
        }
    }
}