using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace TwitchBorn.Settings
{
    public class PlatformIntegrationSettingsOwner : ModSettingsOwner
    {
        public ModSetting<bool> EnableTwitchIntegration { get; } =
            new(false, ModSettingDescriptor.CreateLocalized("Eurymachus.TwitchBorn.Settings.EnableTwitchIntegration"));

        public ModSetting<string> TwitchChannel { get; } =
            new("", ModSettingDescriptor.CreateLocalized("Eurymachus.TwitchBorn.Settings.TwitchChannel"));

        public TwitchAuthorizationSetting TwitchAuthorization { get; } =
            new(ModSettingDescriptor.CreateLocalized("Eurymachus.TwitchBorn.Settings.TwitchAuthorization"));

        public PlatformIntegrationSettingsOwner(
            ISettings settings,
            ModSettingsOwnerRegistry modSettingsOwnerRegistry,
            ModRepository modRepository) : base(settings, modSettingsOwnerRegistry, modRepository)
        {
        }

        public override int Order => 0;

        public override string HeaderLocKey => "Eurymachus.TwitchBorn.Settings.PlatformIntegrationHeader";

        public override ModSettingsContext ChangeableOn => ModSettingsContext.All;

        protected override string ModId => "Eurymachus.TwitchBorn";
    }
}