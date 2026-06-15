using ModSettings.Core;

namespace TwitchBorn.Settings
{
    public class TwitchAuthorizationSetting : NonPersistentSetting
    {
        public TwitchAuthorizationSetting(ModSettingDescriptor descriptor) : base(descriptor)
        {
        }
    }
}