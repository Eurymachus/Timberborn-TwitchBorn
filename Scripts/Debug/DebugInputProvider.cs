using Timberborn.InputSystem;
using Timberborn.SingletonSystem;
using TwitchBorn.Core;
using TwitchBorn.Models;
using TwitchBorn.Services;
using TwitchBorn.Settings;

namespace TwitchBorn.Debug
{
    public class DebugInputProvider : ILoadableSingleton, IUnloadableSingleton
    {
        private readonly KeyboardListener _keyboardListener;
        private readonly DebugSettingsOwner _settingsOwner;
        private readonly PlatformRequestService _platformRequestService;

        private readonly ViewerIdentity[] _debugUsers =
        {
            ViewerIdentity.FromDebug("Slackus"),
            ViewerIdentity.FromDebug("TestViewer"),
            ViewerIdentity.FromDebug("Damothy"),
            ViewerIdentity.FromDebug("BeaverFan"),
            ViewerIdentity.FromDebug("LogLord"),
            ViewerIdentity.FromDebug("DamBuilder")
        };

        private int _messageCounter;

        public DebugInputProvider(
            KeyboardListener keyboardListener,
            DebugSettingsOwner settingsOwner,
            PlatformRequestService platformRequestService)
        {
            _keyboardListener = keyboardListener;
            _settingsOwner = settingsOwner;
            _platformRequestService = platformRequestService;
            _messageCounter = 0;
        }

        public void Load()
        {
            _keyboardListener.KeyPressed += OnKeyPressed;

            TwitchBornLog.Info("Debug input provider loaded.");
            TwitchBornLog.Info("Debug keys: NUM 0 claim all, NUM 1-6 chat users, NUM 7 overlap, NUM 8 long text, NUM 9 max bubble test.");
        }

        public void Unload()
        {
            _keyboardListener.KeyPressed -= OnKeyPressed;

            TwitchBornLog.Info("Debug input provider unloaded.");
        }

        private void OnKeyPressed(object sender, KeyPressedEvent keyPressedEvent)
        {
            if (!_settingsOwner.EnableDebugKeys.Value)
            {
                return;
            }

            if (keyPressedEvent.Key == "NUM 0")
            {
                ClaimAllDebugBeavers();
                return;
            }

            if (keyPressedEvent.Key == "NUM 1")
            {
                SendDebugChat(0, "Hello from Eurymachus!");
                return;
            }

            if (keyPressedEvent.Key == "NUM 2")
            {
                SendDebugChat(1, "Testing bubble two.");
                return;
            }

            if (keyPressedEvent.Key == "NUM 3")
            {
                SendDebugChat(2, "Damothy reporting in.");
                return;
            }

            if (keyPressedEvent.Key == "NUM 4")
            {
                SendDebugChat(3, "I like logs.");
                return;
            }

            if (keyPressedEvent.Key == "NUM 5")
            {
                SendDebugChat(4, "This beaver has opinions.");
                return;
            }

            if (keyPressedEvent.Key == "NUM 6")
            {
                SendDebugChat(5, "Sixth bubble test.");
                return;
            }

            if (keyPressedEvent.Key == "NUM 7")
            {
                SendOverlapTest();
                return;
            }

            if (keyPressedEvent.Key == "NUM 8")
            {
                SendLongMessageTest();
                return;
            }

            if (keyPressedEvent.Key == "NUM 9")
            {
                SendMaxBubbleTest();
                return;
            }
        }

        private void ClaimAllDebugBeavers()
        {
            TwitchBornLog.Info("Debug input: NUM 0 claim all debug beavers.");

            foreach (var viewer in _debugUsers)
            {
                var beaver = _platformRequestService.DebugClaimViewerBeaver(viewer);

                if (beaver == null)
                {
                    TwitchBornLog.Info("Failed to claim debug beaver for " + viewer.SafeDisplayName);
                    continue;
                }

                _platformRequestService.HandleBeaverSpeech(viewer, "I have been claimed!");
            }
        }

        private void SendDebugChat(int userIndex, string message)
        {
            if (userIndex < 0 || userIndex >= _debugUsers.Length)
            {
                TwitchBornLog.Info("Invalid debug user index: " + userIndex);
                return;
            }

            var viewer = _debugUsers[userIndex];

            TwitchBornLog.Info("Debug chat for " + viewer.SafeDisplayName + ": " + message);

            _messageCounter++;

            if (!_platformRequestService.HandleBeaverSpeech(viewer, message + " #" + _messageCounter))
            {
                TwitchBornLog.Info("No registered beaver found for " + viewer.SafeDisplayName + ". Use NUM 0 to claim all first.");
            }
        }

        private void SendOverlapTest()
        {
            TwitchBornLog.Info("Debug input: NUM 7 overlap test.");

            SendDebugChat(0, "Overlap test A.");
            SendDebugChat(1, "Overlap test B.");
            SendDebugChat(2, "Overlap test C.");
        }

        private void SendLongMessageTest()
        {
            TwitchBornLog.Info("Debug input: NUM 8 long message test.");

            SendDebugChat(
                0,
                "This is a longer Twitch chat message designed to test wrapping, trimming, bubble size, and readability at different zoom levels.");
        }

        private void SendMaxBubbleTest()
        {
            TwitchBornLog.Info("Debug input: NUM 9 max bubble test.");

            for (var i = 0; i < _debugUsers.Length; i++)
            {
                SendDebugChat(i, "Max bubble test from " + _debugUsers[i].SafeDisplayName);
            }
        }
    }
}