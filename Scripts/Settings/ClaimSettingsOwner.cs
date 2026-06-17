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

        public ModSetting<bool> AutoClaimOnDeath { get; } =
            new(
                false,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.AutoClaimOnDeath")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.AutoClaimOnDeathTooltip"));

        public ModSetting<bool> NotifyViewerOnGrowUp { get; } =
            new(
                true,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.NotifyViewerOnGrowUp")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.NotifyViewerOnGrowUpTooltip"));

        public ModSetting<bool> NotifyViewerOnDeath { get; } =
            new(
                true,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.NotifyViewerOnDeath")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.NotifyViewerOnDeathTooltip"));

        public ModSetting<bool> KeepAssignedNameAcrossClaims { get; } =
            new(
                true,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.KeepAssignedNameAcrossClaims")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.KeepAssignedNameAcrossClaimsTooltip"));

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