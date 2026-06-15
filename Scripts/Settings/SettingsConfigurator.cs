using Bindito.Core;
using ModSettings.CoreUI;
using TwitchBorn.Platforms.Twitch;

namespace TwitchBorn.Settings
{
    [Context("MainMenu")]
    [Context("Game")]
    internal class SettingsConfigurator : Configurator
    {
        protected override void Configure()
        {
            Bind<PlatformIntegrationSettingsOwner>().AsSingleton();
            Bind<TwitchTriggerSettingsOwner>().AsSingleton();
            Bind<OverlaySettingsOwner>().AsSingleton();
            Bind<ClaimSettingsOwner>().AsSingleton();
            Bind<DebugSettingsOwner>().AsSingleton();

            Bind<TwitchAuthService>().AsSingleton();

            MultiBind<IModSettingElementFactory>()
                .To<TwitchAuthorizationSettingElementFactory>()
                .AsSingleton();
        }
    }
}