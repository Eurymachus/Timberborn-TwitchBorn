using ModSettings.Common;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace TwitchBorn.Settings
{
    public class OverlaySettingsOwner : ModSettingsOwner
    {
        public RangeIntModSetting MaxActiveOverlaysSetting { get; private set; }

        public RangeIntModSetting MaxMessageLengthSetting { get; private set; }

        public RangeIntModSetting WrapLineLengthSetting { get; private set; }

        public RangeIntModSetting OverlayFontSizeSetting { get; private set; }

        public RangeIntModSetting NameplateVisibilityDistanceSetting { get; private set; }

        public RangeIntModSetting MessageVisibilityDistanceSetting { get; private set; }

        public ModSetting<float> MessageDurationSeconds { get; } =
            new(
                4.0f,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.MessageDurationSeconds")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.MessageDurationSecondsTooltip"));

        public ModSetting<float> OverlayHeightOffset { get; } =
            new(
                1.9f,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.OverlayHeightOffset")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.OverlayHeightOffsetTooltip"));

        public OverlaySettingsOwner(
            ISettings settings,
            ModSettingsOwnerRegistry modSettingsOwnerRegistry,
            ModRepository modRepository) : base(settings, modSettingsOwnerRegistry, modRepository)
        {
        }

        public override int Order => 10;

        public override string HeaderLocKey => "Eurymachus.TwitchBorn.Settings.OverlayHeader";

        public override ModSettingsContext ChangeableOn => ModSettingsContext.All;

        protected override string ModId => "Eurymachus.TwitchBorn";

        protected override void OnBeforeLoad()
        {
            MaxActiveOverlaysSetting = new RangeIntModSetting(
                5,
                1,
                20,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.MaxActiveOverlays")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.MaxActiveOverlaysTooltip"));

            MaxMessageLengthSetting = new RangeIntModSetting(
                80,
                20,
                240,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.MaxMessageLength")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.MaxMessageLengthTooltip"));

            WrapLineLengthSetting = new RangeIntModSetting(
                28,
                10,
                60,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.WrapLineLength")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.WrapLineLengthTooltip"));

            OverlayFontSizeSetting = new RangeIntModSetting(
                12,
                8,
                24,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.BubbleFontSize")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.BubbleFontSizeTooltip"));

            NameplateVisibilityDistanceSetting = new RangeIntModSetting(
                35,
                5,
                200,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.NameplateVisibilityDistance")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.NameplateVisibilityDistanceTooltip"));

            MessageVisibilityDistanceSetting = new RangeIntModSetting(
                120,
                5,
                300,
                ModSettingDescriptor
                    .CreateLocalized("Eurymachus.TwitchBorn.Settings.MessageVisibilityDistance")
                    .SetLocalizedTooltip("Eurymachus.TwitchBorn.Settings.MessageVisibilityDistanceTooltip"));
        }
    }
}