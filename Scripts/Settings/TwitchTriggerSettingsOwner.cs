using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace TwitchBorn.Settings
{
    public class TwitchTriggerSettingsOwner : ModSettingsOwner
    {
        public ModSetting<string> ClaimBeaverTriggerText { get; } =
            new(
                "!claim",
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.ClaimBeaverTriggerText")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.ClaimBeaverTriggerTextTooltip"));

        public ModSetting<bool> ClaimBeaverIsChannelPointReward { get; } =
            new(
                false,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.ClaimBeaverIsChannelPointReward")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.ClaimBeaverIsChannelPointRewardTooltip"));

        public ModSetting<string> BeaverStatusTriggerText { get; } =
            new(
                "!beaver",
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.BeaverStatusTriggerText")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.BeaverStatusTriggerTextTooltip"));

        public ModSetting<bool> BeaverStatusIsChannelPointReward { get; } =
            new(
                false,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.BeaverStatusIsChannelPointReward")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.BeaverStatusIsChannelPointRewardTooltip"));

        public ModSetting<string> BeaverRenameTriggerText { get; } =
            new(
                "!rename",
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.BeaverRenameTriggerText")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.BeaverRenameTriggerTextTooltip"));

        public ModSetting<bool> BeaverRenameIsChannelPointReward { get; } =
            new(
                false,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.BeaverRenameIsChannelPointReward")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.BeaverRenameIsChannelPointRewardTooltip"));

        public ModSetting<string> ViewerNameColourTriggerText { get; } =
            new(
        "!colour",
        ModSettingDescriptor
            .CreateLocalized("Eurymachus.TwitchBorn.Settings.ViewerNameColourTriggerText")
            .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.ViewerNameColourTriggerTextTooltip"));

        public ModSetting<bool> ViewerNameColourIsChannelPointReward { get; } =
            new(
                false,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.ViewerNameColourIsChannelPointReward")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.ViewerNameColourIsChannelPointRewardTooltip"));

        public ModSetting<string> ViewerNameShadowTriggerText { get; } =
            new(
                "!shadow",
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.ViewerNameShadowTriggerText")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.ViewerNameShadowTriggerTextTooltip"));

        public ModSetting<bool> ViewerNameShadowIsChannelPointReward { get; } =
            new(
                false,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.ViewerNameShadowIsChannelPointReward")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.ViewerNameShadowIsChannelPointRewardTooltip"));

        public ModSetting<bool> IgnoreUnhandledCommands { get; } =
            new(
                true,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.IgnoreUnhandledCommands")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.IgnoreUnhandledCommandsTooltip"));

        public TwitchTriggerSettingsOwner(
            ISettings settings,
            ModSettingsOwnerRegistry modSettingsOwnerRegistry,
            ModRepository modRepository) : base(settings, modSettingsOwnerRegistry, modRepository)
        {
        }

        public override int Order => 5;

        public override string HeaderLocKey => "Eurymachus.TwitchBorn.Settings.TwitchTriggersHeader";

        public override ModSettingsContext ChangeableOn => ModSettingsContext.All;

        protected override string ModId => "Eurymachus.TwitchBorn";
    }
}