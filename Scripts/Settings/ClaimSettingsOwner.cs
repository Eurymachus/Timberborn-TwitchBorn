using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace TwitchBorn.Settings
{
    public class ClaimSettingsOwner : ModSettingsOwner
    {
        public ModSetting<bool> AllowBotClaims { get; } =
            new(
                false,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.AllowBotClaims")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.AllowBotClaimsTooltip"));

        public ClaimSettingsOwner(
            ISettings settings,
            ModSettingsOwnerRegistry modSettingsOwnerRegistry,
            ModRepository modRepository) : base(settings, modSettingsOwnerRegistry, modRepository)
        {
        }

        public override int Order => 3;

        public override string HeaderLocKey => "Eurymachus.TwitchBorn.Settings.ClaimingHeader";

        public override ModSettingsContext ChangeableOn => ModSettingsContext.All;

        protected override string ModId => "Eurymachus.TwitchBorn";
    }
}