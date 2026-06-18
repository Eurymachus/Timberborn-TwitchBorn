using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace TwitchBorn.Settings
{
    public class MessageFilteringSettingsOwner : ModSettingsOwner
    {
        public ModSetting<string> BlacklistedTerms { get; } =
            new(
                "",
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.BlacklistedTerms")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.BlacklistedTermsTooltip"));

        public MessageFilteringSettingsOwner(
            ISettings settings,
            ModSettingsOwnerRegistry modSettingsOwnerRegistry,
            ModRepository modRepository) : base(settings, modSettingsOwnerRegistry, modRepository)
        {
        }

        public override int Order => 2;

        public override string HeaderLocKey => "Eurymachus.TwitchBorn.Settings.MessageFilteringHeader";

        public override ModSettingsContext ChangeableOn => ModSettingsContext.All;

        protected override string ModId => "Eurymachus.TwitchBorn";
    }
}
