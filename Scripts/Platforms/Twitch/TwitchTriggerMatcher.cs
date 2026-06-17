using System;
using TwitchBorn.Models;
using TwitchBorn.Settings;

namespace TwitchBorn.Platforms.Twitch
{
    public class TwitchTriggerMatcher
    {
        private readonly TwitchTriggerSettingsOwner _settingsOwner;

        public TwitchTriggerMatcher(TwitchTriggerSettingsOwner settingsOwner)
        {
            _settingsOwner = settingsOwner;
        }

        public bool TryMatchChatCommand(
            string message,
            out TwitchRequestMatch match)
        {
            match = null;

            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            if (TryMatchChatTrigger(
                    message,
                    PlatformRequestType.BeaverClaim,
                    _settingsOwner.ClaimBeaverTriggerText.Value,
                    _settingsOwner.ClaimBeaverIsChannelPointReward.Value,
                    out match))
            {
                return true;
            }

            if (TryMatchChatTrigger(
                    message,
                    PlatformRequestType.BeaverStatus,
                    _settingsOwner.BeaverStatusTriggerText.Value,
                    _settingsOwner.BeaverStatusIsChannelPointReward.Value,
                    out match))
            {
                return true;
            }

            if (TryMatchChatTrigger(
                    message,
                    PlatformRequestType.BeaverRename,
                    _settingsOwner.BeaverRenameTriggerText.Value,
                    _settingsOwner.BeaverRenameIsChannelPointReward.Value,
                    out match))
            {
                return true;
            }

            if (TryMatchChatTrigger(
                message,
                PlatformRequestType.ViewerNameColour,
                _settingsOwner.ViewerNameColourTriggerText.Value,
                _settingsOwner.ViewerNameColourIsChannelPointReward.Value,
                out match))
            {
                return true;
            }

            if (TryMatchChatTrigger(
                    message,
                    PlatformRequestType.ViewerNameShadow,
                    _settingsOwner.ViewerNameShadowTriggerText.Value,
                    _settingsOwner.ViewerNameShadowIsChannelPointReward.Value,
                    out match))
            {
                return true;
            }

            return false;
        }

        public bool HasAnyChannelPointTrigger()
        {
            return IsConfiguredChannelPointTrigger(
                    _settingsOwner.ClaimBeaverTriggerText.Value,
                    _settingsOwner.ClaimBeaverIsChannelPointReward.Value)
                || IsConfiguredChannelPointTrigger(
                    _settingsOwner.BeaverStatusTriggerText.Value,
                    _settingsOwner.BeaverStatusIsChannelPointReward.Value)
                || IsConfiguredChannelPointTrigger(
                    _settingsOwner.BeaverRenameTriggerText.Value,
                    _settingsOwner.BeaverRenameIsChannelPointReward.Value)
                || IsConfiguredChannelPointTrigger(
                    _settingsOwner.ViewerNameColourTriggerText.Value,
                    _settingsOwner.ViewerNameColourIsChannelPointReward.Value)
                || IsConfiguredChannelPointTrigger(
                    _settingsOwner.ViewerNameShadowTriggerText.Value,
                    _settingsOwner.ViewerNameShadowIsChannelPointReward.Value);
        }

        public bool IsUnhandledCommand(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            return message.TrimStart().StartsWith("!", StringComparison.Ordinal);
        }

        private static bool TryMatchChatTrigger(
            string message,
            PlatformRequestType requestType,
            string triggerText,
            bool isChannelPointReward,
            out TwitchRequestMatch match)
        {
            match = null;

            if (isChannelPointReward)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(triggerText))
            {
                return false;
            }

            var trimmedMessage = message.Trim();
            var trimmedTrigger = triggerText.Trim();

            if (string.IsNullOrEmpty(trimmedTrigger))
            {
                return false;
            }

            if (string.Equals(trimmedMessage, trimmedTrigger, StringComparison.OrdinalIgnoreCase))
            {
                match = new TwitchRequestMatch(requestType, "");
                return true;
            }

            if (!trimmedMessage.StartsWith(trimmedTrigger, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (trimmedMessage.Length <= trimmedTrigger.Length)
            {
                match = new TwitchRequestMatch(requestType, "");
                return true;
            }

            if (!char.IsWhiteSpace(trimmedMessage[trimmedTrigger.Length]))
            {
                return false;
            }

            var arguments = trimmedMessage.Substring(trimmedTrigger.Length).Trim();

            match = new TwitchRequestMatch(requestType, arguments);
            return true;
        }

        private static bool IsConfiguredChannelPointTrigger(
            string triggerText,
            bool isChannelPointReward)
        {
            return isChannelPointReward && !string.IsNullOrWhiteSpace(triggerText);
        }
    }
}