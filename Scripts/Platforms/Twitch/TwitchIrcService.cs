using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Timberborn.SingletonSystem;
using TwitchBorn.Core;
using TwitchBorn.Models;
using TwitchBorn.Api;
using TwitchBorn.Settings;
using Timberborn.Localization;

namespace TwitchBorn.Platforms.Twitch
{
    public class TwitchIrcService : ILoadableSingleton, IUnloadableSingleton, IUpdatableSingleton
    {
        private const bool LogRawIrc = false;

        private const string Host = "irc.chat.twitch.tv";
        private const int Port = 6697;
        private const int MaxMessagesPerFrame = 20;
        private const int ReconnectDelayMilliseconds = 5000;

        private const string ReplyClaimedLocKey = "Eurymachus.TwitchBorn.Twitch.Reply.Claimed";
        private const string ReplyAlreadyClaimedLocKey = "Eurymachus.TwitchBorn.Twitch.Reply.AlreadyClaimed";
        private const string ReplyQueuedLocKey = "Eurymachus.TwitchBorn.Twitch.Reply.Queued";
        private const string ReplyAlreadyQueuedLocKey = "Eurymachus.TwitchBorn.Twitch.Reply.AlreadyQueued";
        private const string ReplyInvalidViewerLocKey = "Eurymachus.TwitchBorn.Twitch.Reply.InvalidViewer";
        private const string ReplyNoAvailableBeaverLocKey = "Eurymachus.TwitchBorn.Twitch.Reply.NoAvailableBeaver";
        private const string ReplyAssignedFromQueueLocKey = "Eurymachus.TwitchBorn.Twitch.Reply.AssignedFromQueue";
        private const string ReplyStatusLocKey = "Eurymachus.TwitchBorn.Twitch.Reply.Status";
        private const string ReplyRenamedLocKey = "Eurymachus.TwitchBorn.Twitch.Reply.Renamed";
        private const string ReplyNoClaimedBeaverLocKey = "Eurymachus.TwitchBorn.Twitch.Reply.NoClaimedBeaver";
        private const string ReplyInvalidRequestedNameLocKey = "Eurymachus.TwitchBorn.Twitch.Reply.InvalidRequestedName";
        private const string ReplyBeaverStatusLocKey = "Eurymachus.TwitchBorn.Twitch.Reply.BeaverStatus";
        private const string ReplyUnknownBeaverNameLocKey = "Eurymachus.TwitchBorn.Twitch.Reply.UnknownBeaverName";
        private const string ReplyUnknownBeaverStatusLocKey = "Eurymachus.TwitchBorn.Twitch.Reply.UnknownBeaverStatus";

        private readonly PlatformIntegrationSettingsOwner _settingsOwner;
        private readonly TwitchTriggerSettingsOwner _twitchTriggerSettingsOwner;
        private readonly TwitchTriggerMatcher _twitchTriggerMatcher;
        private readonly ITwitchBornApi _twitchBornApi;
        private readonly TwitchAuthService _twitchAuthService;
        private readonly object _queueLock = new object();
        private readonly object _writerLock = new object();
        private readonly Queue<TwitchIrcMessage> _messageQueue = new Queue<TwitchIrcMessage>();
        private readonly ILoc _loc;

        private Thread _thread;
        private volatile bool _shouldRun;
        private string _activeChannel;
        private string _activeLogin;
        private string _activeToken;
        private StreamWriter _writer;
        private bool _missingChannelWarningLogged;

        public TwitchIrcService(
            PlatformIntegrationSettingsOwner settingsOwner,
            TwitchTriggerSettingsOwner twitchTriggerSettingsOwner,
            TwitchTriggerMatcher twitchTriggerMatcher,
            ITwitchBornApi twitchBornApi,
            TwitchAuthService twitchAuthService,
            ILoc loc)
        {
            _settingsOwner = settingsOwner;
            _twitchTriggerSettingsOwner = twitchTriggerSettingsOwner;
            _twitchTriggerMatcher = twitchTriggerMatcher;
            _twitchBornApi = twitchBornApi;
            _twitchAuthService = twitchAuthService;
            _loc = loc;
        }

        public void Load()
        {
            TwitchBornLog.Info("Twitch IRC service loaded.");
        }

        public void Unload()
        {
            StopClient();
            TwitchBornLog.Info("Twitch IRC service unloaded.");
        }

        private bool isLoggingEnabled()
        {
            return LogRawIrc;
        }

        public void UpdateSingleton()
        {
            if (_settingsOwner.EnableTwitchIntegration.Value && string.IsNullOrWhiteSpace(GetConfiguredChannel()))
            {
                if (!_missingChannelWarningLogged)
                {
                    TwitchBornLog.Warning("Twitch integration is enabled, but no Twitch channel login is configured.");
                    _missingChannelWarningLogged = true;
                }

                if (IsClientRunning())
                {
                    StopClient();
                }

                DrainMessages();
                return;
            }

            _missingChannelWarningLogged = false;

            var shouldBeRunning = ShouldBeRunning();

            if (shouldBeRunning && !IsClientRunning())
            {
                StartClient();
            }

            if (!shouldBeRunning && IsClientRunning())
            {
                StopClient();
            }

            if (shouldBeRunning && SettingsChanged())
            {
                StopClient();
                StartClient();
            }

            DrainMessages();
        }

        public bool SendChatMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            lock (_writerLock)
            {
                if (_writer == null)
                {
                    TwitchBornLog.Warning("Cannot send Twitch chat message because IRC writer is not available.");
                    return false;
                }

                try
                {
                    var line = "PRIVMSG #" + _activeChannel + " :" + message;
                    if (isLoggingEnabled())
                    {
                        TwitchBornLog.Info("IRC -> " + line);
                    }
                    _writer.WriteLine(line);
                    return true;
                }
                catch (Exception exception)
                {
                    TwitchBornLog.Warning("Failed to send Twitch chat message: " + exception.Message);
                    return false;
                }
            }
        }

        private bool ShouldBeRunning()
        {
            string accessToken;
            string botLogin;

            return _settingsOwner.EnableTwitchIntegration.Value
                && !string.IsNullOrEmpty(GetConfiguredChannel())
                && _twitchAuthService.TryGetValidAccessToken(out accessToken, out botLogin)
                && _twitchAuthService.IsConfiguredChannelValidated;
        }

        private bool IsClientRunning()
        {
            return _thread != null && _thread.IsAlive;
        }

        private bool SettingsChanged()
        {
            string accessToken;
            string botLogin;

            if (!_twitchAuthService.TryGetValidAccessToken(out accessToken, out botLogin))
            {
                return true;
            }

            return !string.Equals(_activeChannel, GetConfiguredChannel(), StringComparison.Ordinal)
                   || !string.Equals(_activeLogin, NormalizeLogin(botLogin), StringComparison.Ordinal)
                   || !string.Equals(_activeToken, accessToken, StringComparison.Ordinal);
        }

        private void StartClient()
        {
            string accessToken;
            string botLogin;

            if (!_twitchAuthService.TryGetValidAccessToken(out accessToken, out botLogin))
            {
                return;
            }

            _activeChannel = GetConfiguredChannel();
            _activeLogin = NormalizeLogin(botLogin);
            _activeToken = accessToken;

            _shouldRun = true;
            _thread = new Thread(RunClient);
            _thread.IsBackground = true;
            _thread.Name = "TwitchBorn IRC";
            _thread.Start();

            TwitchBornLog.Info("Starting Twitch IRC for channel " + _activeChannel + " as " + _activeLogin + ".");
        }

        private void StopClient()
        {
            _shouldRun = false;

            lock (_writerLock)
            {
                _writer = null;
            }

            if (_thread != null && _thread.IsAlive)
            {
                if (!_thread.Join(1000))
                {
                    TwitchBornLog.Warning("Twitch IRC thread did not stop cleanly within timeout.");
                }
            }

            _thread = null;
            _activeChannel = "";
            _activeLogin = "";
            _activeToken = "";
        }

        private void RunClient()
        {
            while (_shouldRun)
            {
                try
                {
                    RunConnectedClient();
                }
                catch (Exception exception)
                {
                    TwitchBornLog.Warning("Twitch IRC disconnected: " + exception.Message);
                }

                lock (_writerLock)
                {
                    _writer = null;
                }

                if (_shouldRun)
                {
                    Thread.Sleep(ReconnectDelayMilliseconds);
                }
            }
        }

        private void RunConnectedClient()
        {
            using (var tcpClient = new TcpClient())
            {
                tcpClient.ReceiveTimeout = 0;
                tcpClient.SendTimeout = 10000;
                tcpClient.Connect(Host, Port);

                using (var sslStream = new SslStream(tcpClient.GetStream(), false))
                {
                    sslStream.AuthenticateAsClient(Host);

                    var utf8WithoutBom = new UTF8Encoding(false);

                    using (var reader = new StreamReader(sslStream, utf8WithoutBom))
                    using (var writer = new StreamWriter(sslStream, utf8WithoutBom))
                    {
                        writer.NewLine = "\r\n";
                        writer.AutoFlush = true;

                        lock (_writerLock)
                        {
                            _writer = writer;
                        }

                        SendLogin(writer);

                        TwitchBornLog.Info("Twitch IRC socket opened for #" + _activeChannel + " as " + _activeLogin + ".");

                        while (_shouldRun && tcpClient.Connected)
                        {
                            var line = reader.ReadLine();

                            if (line == null)
                            {
                                return;
                            }

                            HandleLine(writer, line);
                        }
                    }
                }
            }
        }

        private void SendLogin(StreamWriter writer)
        {
            var token = _activeToken;

            if (!token.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase))
            {
                token = "oauth:" + token;
            }

            writer.WriteLine("PASS " + token);
            writer.WriteLine("NICK " + _activeLogin);
            writer.WriteLine("CAP REQ :twitch.tv/tags twitch.tv/commands");
            writer.WriteLine("JOIN #" + _activeChannel);
        }

        private void HandleLine(StreamWriter writer, string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            if (isLoggingEnabled())
            {
                TwitchBornLog.Info("IRC <- " + line);
            }

            if (line.StartsWith("PING", StringComparison.Ordinal))
            {
                writer.WriteLine(line.Replace("PING", "PONG"));
                TwitchBornLog.Info("IRC -> PONG");
                return;
            }

            if (line.IndexOf("Login authentication failed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                TwitchBornLog.Warning("Twitch IRC authentication failed. Marking token invalid.");
                _twitchAuthService.MarkCurrentTokenInvalid("Twitch IRC authentication failed. Reauthorizing.");
                _shouldRun = false;
                return;
            }

            if (line.Contains(" NOTICE "))
            {
                TwitchBornLog.Warning("Twitch IRC notice: " + line);
            }

            if (line.Contains(" 001 "))
            {
                TwitchBornLog.Info("Twitch IRC authentication accepted.");
            }

            if (line.Contains(" ROOMSTATE "))
            {
                TwitchBornLog.Info("Twitch IRC joined channel #" + _activeChannel + ".");
            }

            if (!line.Contains(" PRIVMSG "))
            {
                return;
            }

            TwitchBornLog.Info("Twitch IRC PRIVMSG received.");

            var ircMessage = TryParsePrivMsg(line);

            if (ircMessage == null)
            {
                TwitchBornLog.Warning("Twitch IRC PRIVMSG could not be parsed.");
                return;
            }

            TwitchBornLog.Info(
                "Twitch chat from " +
                ircMessage.Viewer.LoginName +
                " / " +
                ircMessage.Viewer.SourceUserId +
                ": " +
                ircMessage.Message);

            lock (_queueLock)
            {
                _messageQueue.Enqueue(ircMessage);
            }
        }

        private void DrainMessages()
        {
            var processed = 0;

            while (processed < MaxMessagesPerFrame)
            {
                TwitchIrcMessage message = null;

                lock (_queueLock)
                {
                    if (_messageQueue.Count > 0)
                    {
                        message = _messageQueue.Dequeue();
                    }
                }

                if (message == null)
                {
                    return;
                }

                ProcessMessage(message);
                processed++;
            }
        }

        private void ProcessMessage(TwitchIrcMessage twitchMessage)
        {
            if (twitchMessage == null || twitchMessage.Viewer == null || !twitchMessage.Viewer.IsValid)
            {
                return;
            }

            var message = twitchMessage.Message ?? "";

            TwitchRequestMatch match;

            if (_twitchTriggerMatcher.TryMatchChatCommand(message, out match))
            {
                var reply = DispatchRequest(
                    twitchMessage.Viewer,
                    match);

                if (!string.IsNullOrEmpty(reply))
                {
                    SendChatMessage(reply);
                }

                return;
            }

            if (_twitchTriggerSettingsOwner.IgnoreUnhandledCommands.Value
                && _twitchTriggerMatcher.IsUnhandledCommand(message))
            {
                TwitchBornLog.Info("Ignoring unhandled Twitch command: " + message);
                return;
            }

            _twitchBornApi.SendSpeech(twitchMessage.Viewer, message);
        }

        private string DispatchRequest(
            ViewerIdentity viewer,
            TwitchRequestMatch match)
        {
            if (match == null)
            {
                return "";
            }

            BeaverCommandResult result;

            switch (match.RequestType)
            {
                case PlatformRequestType.BeaverClaim:
                    result = _twitchBornApi.ClaimCharacter(viewer);
                    return BuildBeaverCommandReply(result);

                case PlatformRequestType.BeaverStatus:
                    result = _twitchBornApi.GetCharacterStatus(viewer);
                    return BuildBeaverCommandReply(result);

                case PlatformRequestType.BeaverRename:
                    result = _twitchBornApi.RenameCharacter(
                        viewer,
                        match.Arguments);
                    return BuildBeaverCommandReply(result);

                default:
                    return "";
            }
        }

        private string BuildBeaverCommandReply(BeaverCommandResult result)
        {
            if (result == null || result.Viewer == null)
            {
                return "";
            }

            var mention = "@" + GetReplyName(result.Viewer);
            var status = FormatBeaverStatus(result);

            switch (result.Type)
            {
                case BeaverCommandResultType.Claimed:
                    return _loc.T(ReplyClaimedLocKey, mention, status);

                case BeaverCommandResultType.AlreadyClaimed:
                    return _loc.T(ReplyAlreadyClaimedLocKey, mention, status);

                case BeaverCommandResultType.Queued:
                    return _loc.T(ReplyQueuedLocKey, mention);

                case BeaverCommandResultType.AlreadyQueued:
                    return _loc.T(ReplyAlreadyQueuedLocKey, mention);

                case BeaverCommandResultType.InvalidViewer:
                    return _loc.T(ReplyInvalidViewerLocKey, mention);

                case BeaverCommandResultType.NoAvailableBeaver:
                    return _loc.T(ReplyNoAvailableBeaverLocKey, mention);

                case BeaverCommandResultType.AssignedFromQueue:
                    return _loc.T(ReplyAssignedFromQueueLocKey, mention, status);

                case BeaverCommandResultType.Status:
                    return _loc.T(ReplyStatusLocKey, mention, status);

                case BeaverCommandResultType.Renamed:
                    return _loc.T(ReplyRenamedLocKey, mention, status);

                case BeaverCommandResultType.NoClaimedBeaver:
                    return _loc.T(ReplyNoClaimedBeaverLocKey, mention);

                case BeaverCommandResultType.InvalidRequestedName:
                    return _loc.T(ReplyInvalidRequestedNameLocKey, mention);

                default:
                    return "";
            }
        }

        private string FormatBeaverStatus(BeaverCommandResult result)
        {
            if (result == null)
            {
                return _loc.T(
                    ReplyBeaverStatusLocKey,
                    _loc.T(ReplyUnknownBeaverNameLocKey),
                    _loc.T(ReplyUnknownBeaverStatusLocKey));
            }

            var beaverName = string.IsNullOrEmpty(result.BeaverName)
                ? _loc.T(ReplyUnknownBeaverNameLocKey)
                : result.BeaverName;

            var beaverStatus = string.IsNullOrEmpty(result.BeaverStatus)
                ? _loc.T(ReplyUnknownBeaverStatusLocKey)
                : result.BeaverStatus;

            return _loc.T(
                ReplyBeaverStatusLocKey,
                beaverName,
                beaverStatus);
        }

        private static string GetReplyName(ViewerIdentity viewer)
        {
            if (viewer == null)
            {
                return "viewer";
            }

            if (!string.IsNullOrEmpty(viewer.LoginName))
            {
                return TwitchBornTextSanitizer.SanitizePlainText(viewer.LoginName, 64);
            }

            var plainName = TwitchBornTextSanitizer.SanitizePlainText(viewer.SafeDisplayName, 64);
            return string.IsNullOrEmpty(plainName) ? "viewer" : plainName;
        }

        private TwitchIrcMessage TryParsePrivMsg(string line)
        {
            var tags = ParseTags(line);
            var userId = GetTag(tags, "user-id");

            if (string.IsNullOrEmpty(userId))
            {
                TwitchBornLog.Info("Twitch chat ignored because PRIVMSG had no stable user-id tag.");
                return null;
            }

            var displayName = GetTag(tags, "display-name");
            var loginName = ParseLoginName(line);
            var message = ParseMessageText(line);

            if (string.IsNullOrEmpty(displayName))
            {
                displayName = loginName;
            }

            var viewer = _twitchBornApi.CreateViewerIdentity(
                ViewerIdentity.TwitchSource,
                userId,
                loginName,
                displayName);

            return new TwitchIrcMessage(viewer, message, tags);
        }

        private static Dictionary<string, string> ParseTags(string line)
        {
            var tags = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(line) || line[0] != '@')
            {
                return tags;
            }

            var tagEndIndex = line.IndexOf(' ');

            if (tagEndIndex <= 1)
            {
                return tags;
            }

            var tagText = line.Substring(1, tagEndIndex - 1);
            var tagParts = tagText.Split(';');

            foreach (var tagPart in tagParts)
            {
                var equalsIndex = tagPart.IndexOf('=');

                if (equalsIndex <= 0)
                {
                    continue;
                }

                var key = tagPart.Substring(0, equalsIndex);
                var value = UnescapeTagValue(tagPart.Substring(equalsIndex + 1));

                tags[key] = value;
            }

            return tags;
        }

        private static string ParseLoginName(string line)
        {
            var bangIndex = line.IndexOf('!');
            var prefixIndex = line.IndexOf(':');

            if (prefixIndex >= 0 && bangIndex > prefixIndex)
            {
                return line.Substring(prefixIndex + 1, bangIndex - prefixIndex - 1);
            }

            return "";
        }

        private static string ParseMessageText(string line)
        {
            var privMsgIndex = line.IndexOf(" PRIVMSG ", StringComparison.Ordinal);

            if (privMsgIndex < 0)
            {
                return "";
            }

            var messageStartIndex = line.IndexOf(" :", privMsgIndex, StringComparison.Ordinal);

            if (messageStartIndex < 0)
            {
                return "";
            }

            return line.Substring(messageStartIndex + 2);
        }

        private static string GetTag(Dictionary<string, string> tags, string key)
        {
            string value;

            if (tags != null && tags.TryGetValue(key, out value))
            {
                return value ?? "";
            }

            return "";
        }

        private static string UnescapeTagValue(string value)
        {
            if (value == null)
            {
                return "";
            }

            return value
                .Replace("\\s", " ")
                .Replace("\\:", ";")
                .Replace("\\r", "\r")
                .Replace("\\n", "\n")
                .Replace("\\\\", "\\");
        }

        private string GetConfiguredChannel()
        {
            return NormalizeLogin(_settingsOwner.TwitchChannel.Value);
        }

        private static string NormalizeLogin(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            var normalized = value.Trim();

            if (normalized.StartsWith("@", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(1);
            }

            return normalized.ToLowerInvariant();
        }
    }
}