using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace TwitchBorn.Settings
{
    public class DebugSettingsOwner : ModSettingsOwner
    {
        public ModSetting<bool> EnableDebugKeys { get; } =
            new(false, ModSettingDescriptor.CreateLocalized("Eurymachus.TwitchBorn.Settings.EnableDebugKeys"));

        public DebugSettingsOwner(
            ISettings settings,
            ModSettingsOwnerRegistry modSettingsOwnerRegistry,
            ModRepository modRepository) : base(settings, modSettingsOwnerRegistry, modRepository)
        {
        }

        public override int Order => 20;

        public override string HeaderLocKey => "Eurymachus.TwitchBorn.Settings.DebugHeader";

        public override ModSettingsContext ChangeableOn => ModSettingsContext.All;

        protected override string ModId => "Eurymachus.TwitchBorn";
    }
}