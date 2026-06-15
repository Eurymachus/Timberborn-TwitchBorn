using System;
using ModSettings.CommonUI;
using ModSettings.Core;
using ModSettings.CoreUI;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.TooltipSystem;
using TwitchBorn.Platforms.Twitch;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwitchBorn.Settings
{
    public class TwitchAuthorizationSettingElementFactory : IModSettingElementFactory
    {
        private const string BotConnectionLocKey = "Eurymachus.TwitchBorn.Settings.BotConnection";
        private const string ConnectLocKey = "Eurymachus.TwitchBorn.Settings.Auth.Connect";
        private const string WaitingLocKey = "Eurymachus.TwitchBorn.Settings.Auth.Waiting";
        private const string ConnectedLocKey = "Eurymachus.TwitchBorn.Settings.Auth.Connected";
        private const string DisconnectLocKey = "Eurymachus.TwitchBorn.Settings.Auth.Disconnect";
        private const string OpenAuthorizationPageLocKey = "Eurymachus.TwitchBorn.Settings.Auth.OpenAuthorizationPage";
        private const string AuthorizeHelperLocKey = "Eurymachus.TwitchBorn.Settings.Auth.AuthorizeHelper";
        private const string RequestingHelperLocKey = "Eurymachus.TwitchBorn.Settings.Auth.RequestingHelper";
        private const string WaitingHelperLocKey = "Eurymachus.TwitchBorn.Settings.Auth.WaitingHelper";
        private const string ConnectTooltipLocKey = "Eurymachus.TwitchBorn.Settings.Auth.ConnectTooltip";
        private const string WaitingTooltipLocKey = "Eurymachus.TwitchBorn.Settings.Auth.WaitingTooltip";
        private const string ConnectedTooltipLocKey = "Eurymachus.TwitchBorn.Settings.Auth.ConnectedTooltip";
        private const string ConnectedToChannelTooltipLocKey = "Eurymachus.TwitchBorn.Settings.Auth.ConnectedToChannelTooltip";
        private const string ConnectedGenericTooltipLocKey = "Eurymachus.TwitchBorn.Settings.Auth.ConnectedGenericTooltip";
        private const string DisconnectTooltipLocKey = "Eurymachus.TwitchBorn.Settings.Auth.DisconnectTooltip";
        private const string CancelAuthorizationTooltipLocKey = "Eurymachus.TwitchBorn.Settings.Auth.CancelAuthorizationTooltip";
        private const string NoConnectionTooltipLocKey = "Eurymachus.TwitchBorn.Settings.Auth.NoConnectionTooltip";
        private const string OpenAuthorizationPageTooltipLocKey = "Eurymachus.TwitchBorn.Settings.Auth.OpenAuthorizationPageTooltip";

        private const string ChannelRequiredLocKey = "Eurymachus.TwitchBorn.Settings.Auth.ChannelRequired";
        private const string ConnectedChannelRequiredLocKey = "Eurymachus.TwitchBorn.Settings.Auth.ConnectedChannelRequired";
        private const string ConnectedMissingChannelTooltipLocKey = "Eurymachus.TwitchBorn.Settings.Auth.ConnectedMissingChannelTooltip";

        private const string ChannelValidatingLocKey = "Eurymachus.TwitchBorn.Settings.Auth.ChannelValidating";
        private const string ChannelNotFoundLocKey = "Eurymachus.TwitchBorn.Settings.Auth.ChannelNotFound";
        private const string ChannelValidationFailedLocKey = "Eurymachus.TwitchBorn.Settings.Auth.ChannelValidationFailed";
        private const string ConnectedChannelValidatingTooltipLocKey = "Eurymachus.TwitchBorn.Settings.Auth.ConnectedChannelValidatingTooltip";
        private const string ConnectedChannelNotFoundTooltipLocKey = "Eurymachus.TwitchBorn.Settings.Auth.ConnectedChannelNotFoundTooltip";
        private const string ConnectedChannelValidationFailedTooltipLocKey = "Eurymachus.TwitchBorn.Settings.Auth.ConnectedChannelValidationFailedTooltip";

        private readonly TwitchAuthService _twitchAuthService;
        private readonly ITooltipRegistrar _tooltipRegistrar;
        private readonly ILoc _loc;
        private readonly PlatformIntegrationSettingsOwner _settingsOwner;

        public TwitchAuthorizationSettingElementFactory(
            TwitchAuthService twitchAuthService,
            ITooltipRegistrar tooltipRegistrar,
            ILoc loc,
            PlatformIntegrationSettingsOwner settingsOwner)
        {
            _twitchAuthService = twitchAuthService;
            _tooltipRegistrar = tooltipRegistrar;
            _loc = loc;
            _settingsOwner = settingsOwner;
        }

        public int Priority => 100;

        public bool TryCreateElement(ModSetting modSetting, out IModSettingElement element)
        {
            if (modSetting is TwitchAuthorizationSetting twitchAuthorizationSetting)
            {
                var openAuthorizationUrlWhenReady = false;
                var authorizationUrlOpened = false;

                var root = new VisualElement();
                root.name = "TwitchAuthorizationSetting";
                root.AddToClassList("settings-element");
                root.style.flexDirection = FlexDirection.Column;
                root.style.alignItems = Align.Center;
                root.style.alignSelf = Align.Stretch;
                root.style.width = Length.Percent(99);
                root.style.maxWidth = Length.Percent(99);
                root.style.minWidth = 0;
                root.style.flexShrink = 1;
                root.style.marginTop = 4;
                root.style.marginBottom = 12;

                var title = new Label(_loc.T(BotConnectionLocKey));
                title.AddToClassList("text--default");
                title.AddToClassList("text--bold");
                title.style.unityTextAlign = TextAnchor.MiddleCenter;
                title.style.marginTop = 2;
                title.style.marginBottom = 6;
                root.Add(title);

                var buttonRow = new VisualElement();
                buttonRow.style.flexDirection = FlexDirection.Row;
                buttonRow.style.justifyContent = Justify.Center;
                buttonRow.style.alignItems = Align.Center;
                buttonRow.style.alignSelf = Align.Center;
                buttonRow.style.minWidth = 0;
                buttonRow.style.flexShrink = 1;
                buttonRow.style.marginBottom = 6;
                root.Add(buttonRow);

                var connectButton = CreateVisualButton(_loc.T(ConnectLocKey), () =>
                {
                    if (_twitchAuthService.IsAuthorized || _twitchAuthService.IsAuthorizationPending)
                    {
                        return;
                    }

                    openAuthorizationUrlWhenReady = true;
                    authorizationUrlOpened = false;
                    _twitchAuthService.BeginAuthorization();
                });

                connectButton.Root.style.marginRight = 8;
                buttonRow.Add(connectButton.Root);

                var disconnectButton = CreateVisualButton(_loc.T(DisconnectLocKey), () =>
                {
                    if (!_twitchAuthService.IsAuthorized && !_twitchAuthService.IsAuthorizationPending)
                    {
                        return;
                    }

                    openAuthorizationUrlWhenReady = false;
                    authorizationUrlOpened = false;
                    _twitchAuthService.ForgetAuthorization();
                });

                buttonRow.Add(disconnectButton.Root);

                var helperLabel = new Label();
                helperLabel.AddToClassList("text--default");
                helperLabel.style.whiteSpace = WhiteSpace.Normal;
                helperLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                helperLabel.style.flexShrink = 1;
                helperLabel.style.minWidth = 0;
                helperLabel.style.maxWidth = Length.Percent(100);
                helperLabel.style.marginTop = 0;
                helperLabel.style.marginBottom = 0;
                root.Add(helperLabel);

                var openAuthorizationButton = CreateVisualButton(_loc.T(OpenAuthorizationPageLocKey), () =>
                {
                    OpenAuthorizationUrl();
                });

                openAuthorizationButton.Root.style.marginTop = 8;
                openAuthorizationButton.Root.style.alignSelf = Align.Center;
                openAuthorizationButton.Root.style.width = 250;
                openAuthorizationButton.Root.style.minWidth = 220;
                openAuthorizationButton.Root.style.maxWidth = 280;
                root.Add(openAuthorizationButton.Root);

                _tooltipRegistrar.Register(connectButton.Root, BuildConnectTooltip);
                _tooltipRegistrar.Register(disconnectButton.Root, BuildDisconnectTooltip);
                _tooltipRegistrar.Register(openAuthorizationButton.Root, () => _loc.T(OpenAuthorizationPageTooltipLocKey));

                void Refresh()
                {
                    var isAuthorized = _twitchAuthService.IsAuthorized;
                    var isPending = _twitchAuthService.IsAuthorizationPending;
                    var hasAuthorizationUrl = _twitchAuthService.HasAuthorizationCode;
                    var isMissingRequiredChannel = IsMissingRequiredChannel();

                    if (isAuthorized)
                    {
                        connectButton.SetText(_loc.T(ConnectedLocKey));
                        connectButton.SetInteractive(false);

                        disconnectButton.SetText(_loc.T(DisconnectLocKey));
                        disconnectButton.SetInteractive(true);

                        if (isMissingRequiredChannel)
                        {
                            helperLabel.text = _loc.T(ConnectedChannelRequiredLocKey);
                            helperLabel.style.display = DisplayStyle.Flex;
                        }
                        else
                        {
                            var connectedHelperText = GetConnectedHelperText();

                            if (string.IsNullOrEmpty(connectedHelperText))
                            {
                                helperLabel.text = "";
                                helperLabel.style.display = DisplayStyle.None;
                            }
                            else
                            {
                                helperLabel.text = connectedHelperText;
                                helperLabel.style.display = DisplayStyle.Flex;
                            }
                        }

                        openAuthorizationButton.Root.style.display = DisplayStyle.None;

                        openAuthorizationUrlWhenReady = false;
                        authorizationUrlOpened = false;
                        return;
                    }

                    if (isPending)
                    {
                        connectButton.SetText(_loc.T(WaitingLocKey));
                        connectButton.SetInteractive(false);

                        disconnectButton.SetText(_loc.T(DisconnectLocKey));
                        disconnectButton.SetInteractive(true);

                        helperLabel.text = hasAuthorizationUrl
                            ? _loc.T(WaitingHelperLocKey)
                            : _loc.T(RequestingHelperLocKey);
                        helperLabel.style.display = DisplayStyle.Flex;

                        openAuthorizationButton.Root.style.display = hasAuthorizationUrl
                            ? DisplayStyle.Flex
                            : DisplayStyle.None;
                        openAuthorizationButton.SetInteractive(hasAuthorizationUrl);

                        if (openAuthorizationUrlWhenReady && hasAuthorizationUrl && !authorizationUrlOpened)
                        {
                            authorizationUrlOpened = OpenAuthorizationUrl();
                        }

                        return;
                    }

                    connectButton.SetText(_loc.T(ConnectLocKey));
                    connectButton.SetInteractive(true);

                    disconnectButton.SetText(_loc.T(DisconnectLocKey));
                    disconnectButton.SetInteractive(false);

                    helperLabel.text = isMissingRequiredChannel
                        ? _loc.T(ChannelRequiredLocKey)
                        : _loc.T(AuthorizeHelperLocKey);
                    helperLabel.style.display = DisplayStyle.Flex;

                    openAuthorizationButton.Root.style.display = DisplayStyle.None;
                }

                root.schedule.Execute(Refresh).Every(250);
                Refresh();

                element = new ModSettingElement(root, twitchAuthorizationSetting);
                return true;
            }

            element = null;
            return false;
        }

        private VisualButton CreateVisualButton(string text, Action onClick)
        {
            var root = new NineSliceVisualElement();
            root.AddToClassList("menu-button");
            root.pickingMode = PickingMode.Position;
            root.focusable = true;
            root.style.height = 34;
            root.style.width = 150;
            root.style.minWidth = 140;
            root.style.maxWidth = 160;
            root.style.flexGrow = 0;
            root.style.flexShrink = 1;
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;
            root.style.paddingLeft = 0;
            root.style.paddingRight = 0;
            root.style.marginLeft = 0;
            root.style.marginRight = 0;

            var label = new Label(text);
            label.AddToClassList("text--default");
            label.AddToClassList("text--bold");
            label.pickingMode = PickingMode.Ignore;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            root.Add(label);

            var visualButton = new VisualButton(root, label, onClick);
            root.RegisterCallback<ClickEvent>(_ => visualButton.Click());

            return visualButton;
        }

        private bool IsMissingRequiredChannel()
        {
            return _settingsOwner.EnableTwitchIntegration.Value
                && string.IsNullOrWhiteSpace(_settingsOwner.TwitchChannel.Value);
        }

        private string GetConnectedHelperText()
        {
            if (IsMissingRequiredChannel())
            {
                return _loc.T(ConnectedChannelRequiredLocKey);
            }

            var channelLogin = _settingsOwner.TwitchChannel.Value ?? "";

            switch (_twitchAuthService.ConfiguredChannelValidationStatus)
            {
                case TwitchChannelValidationStatus.Validating:
                    return _loc.T(ChannelValidatingLocKey, channelLogin);

                case TwitchChannelValidationStatus.Invalid:
                    return _loc.T(ChannelNotFoundLocKey, channelLogin);

                case TwitchChannelValidationStatus.Failed:
                    return _loc.T(ChannelValidationFailedLocKey, channelLogin);

                default:
                    return "";
            }
        }

        private bool OpenAuthorizationUrl()
        {
            var authorizationUrl = _twitchAuthService.AuthorizationUrl;

            if (string.IsNullOrEmpty(authorizationUrl))
            {
                return false;
            }

            Application.OpenURL(authorizationUrl);
            return true;
        }

        private string BuildConnectTooltip()
        {
            if (IsMissingRequiredChannel())
            {
                return _loc.T(ConnectedMissingChannelTooltipLocKey);
            }
            if (_twitchAuthService.IsAuthorized)
            {
                return BuildConnectedTooltip();
            }

            if (_twitchAuthService.IsAuthorizationPending)
            {
                return _loc.T(WaitingTooltipLocKey);
            }

            return _loc.T(ConnectTooltipLocKey);
        }

        private string BuildDisconnectTooltip()
        {
            if (_twitchAuthService.IsAuthorized)
            {
                return _loc.T(DisconnectTooltipLocKey);
            }

            if (_twitchAuthService.IsAuthorizationPending)
            {
                return _loc.T(CancelAuthorizationTooltipLocKey);
            }

            return _loc.T(NoConnectionTooltipLocKey);
        }

        private string BuildConnectedTooltip()
        {
            var botLogin = _twitchAuthService.BotLogin;

            if (IsMissingRequiredChannel())
            {
                return _loc.T(ConnectedMissingChannelTooltipLocKey);
            }

            var channelLogin = _settingsOwner.TwitchChannel.Value ?? "";

            switch (_twitchAuthService.ConfiguredChannelValidationStatus)
            {
                case TwitchChannelValidationStatus.Validating:
                    return _loc.T(ConnectedChannelValidatingTooltipLocKey, channelLogin);

                case TwitchChannelValidationStatus.Invalid:
                    return _loc.T(ConnectedChannelNotFoundTooltipLocKey, channelLogin);

                case TwitchChannelValidationStatus.Failed:
                    return _loc.T(ConnectedChannelValidationFailedTooltipLocKey, channelLogin);
            }

            if (!string.IsNullOrEmpty(botLogin))
            {
                return _loc.T(ConnectedTooltipLocKey, botLogin);
            }

            return _loc.T(ConnectedGenericTooltipLocKey);
        }

        private sealed class VisualButton
        {
            private readonly Label _label;
            private readonly Action _onClick;
            private bool _interactive;

            public VisualButton(NineSliceVisualElement root, Label label, Action onClick)
            {
                Root = root;
                _label = label;
                _onClick = onClick;
                SetInteractive(true);
            }

            public NineSliceVisualElement Root { get; }

            public void SetText(string text)
            {
                _label.text = text ?? "";
            }

            public void SetInteractive(bool interactive)
            {
                _interactive = interactive;

                Root.pickingMode = PickingMode.Position;
                Root.focusable = interactive;
                Root.style.opacity = interactive ? 1.0f : 0.45f;
            }

            public void Click()
            {
                if (!_interactive)
                {
                    return;
                }

                _onClick?.Invoke();
            }
        }
    }
}