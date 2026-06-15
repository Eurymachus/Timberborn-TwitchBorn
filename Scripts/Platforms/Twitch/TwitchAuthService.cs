using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Timberborn.SingletonSystem;
using TwitchBorn.Core;
using TwitchBorn.Settings;
using UnityEngine;

namespace TwitchBorn.Platforms.Twitch
{
    public class TwitchAuthService : ILoadableSingleton, IUnloadableSingleton, IUpdatableSingleton
    {
        private const string DeviceEndpoint = "https://id.twitch.tv/oauth2/device";
        private const string TokenEndpoint = "https://id.twitch.tv/oauth2/token";
        private const string ValidateEndpoint = "https://id.twitch.tv/oauth2/validate";
        private const string UsersEndpoint = "https://api.twitch.tv/helix/users";
        private const int TokenExpirySafetySeconds = 120;
        private const int AutoAuthRetrySeconds = 60;

        private string _authorizationStatus = "Not connected.";
        private string _authorizationUrl = "";
        private string _authorizationUserCode = "";
        private bool _forceDeviceAuthorization;

        private readonly object _stateLock = new object();

        private TwitchAuthState _state;
        private Thread _authThread;
        private volatile bool _shouldRun;
        private volatile bool _authThreadRunning;
        private int _authorizationAttemptId;
        private DateTime _nextAutoAuthAttemptUtc;

        private readonly object _channelValidationLock = new object();

        private Thread _channelValidationThread;
        private volatile bool _channelValidationThreadRunning;
        private string _channelValidationLogin = "";
        private string _validatedChannelLogin = "";
        private string _validatedChannelDisplayName = "";
        private TwitchChannelValidationStatus _channelValidationStatus = TwitchChannelValidationStatus.Unknown;

        private readonly PlatformIntegrationSettingsOwner _settingsOwner;

        public TwitchAuthService(PlatformIntegrationSettingsOwner settingsOwner)
        {
            _settingsOwner = settingsOwner;
            _nextAutoAuthAttemptUtc = DateTime.MinValue;
        }

        public string AuthorizationStatus
        {
            get
            {
                lock (_stateLock)
                {
                    if (_state != null && !string.IsNullOrEmpty(_state.botLogin) && IsStateTokenStillUsable(_state))
                    {
                        return "Connected as " + _state.botLogin + ".";
                    }

                    return _authorizationStatus;
                }
            }
        }

        public string AuthorizationUrl
        {
            get
            {
                lock (_stateLock)
                {
                    return _authorizationUrl;
                }
            }
        }

        public string AuthorizationUserCode
        {
            get
            {
                lock (_stateLock)
                {
                    return _authorizationUserCode;
                }
            }
        }

        public bool HasAuthorizationCode
        {
            get
            {
                lock (_stateLock)
                {
                    return !string.IsNullOrEmpty(_authorizationUrl)
                        && !string.IsNullOrEmpty(_authorizationUserCode);
                }
            }
        }

        public bool IsAuthorizationPending
        {
            get
            {
                if (IsAuthorized)
                {
                    return false;
                }

                return _authThreadRunning || HasAuthorizationCode;
            }
        }

        public string ConfiguredChannelLogin
        {
            get
            {
                return GetConfiguredChannel();
            }
        }

        public TwitchChannelValidationStatus ConfiguredChannelValidationStatus
        {
            get
            {
                var configuredChannel = NormalizeLogin(GetConfiguredChannel());

                lock (_channelValidationLock)
                {
                    if (string.IsNullOrEmpty(configuredChannel))
                    {
                        return TwitchChannelValidationStatus.Unknown;
                    }

                    if (!string.Equals(_channelValidationLogin, configuredChannel, StringComparison.Ordinal))
                    {
                        return TwitchChannelValidationStatus.Unknown;
                    }

                    return _channelValidationStatus;
                }
            }
        }

        public string ValidatedChannelLogin
        {
            get
            {
                lock (_channelValidationLock)
                {
                    return _validatedChannelLogin;
                }
            }
        }

        public string ValidatedChannelDisplayName
        {
            get
            {
                lock (_channelValidationLock)
                {
                    return _validatedChannelDisplayName;
                }
            }
        }

        public bool IsConfiguredChannelValidated
        {
            get
            {
                return ConfiguredChannelValidationStatus == TwitchChannelValidationStatus.Valid;
            }
        }

        public void BeginAuthorization()
        {
            if (IsAuthorized)
            {
                return;
            }

            if (_authThreadRunning)
            {
                SetAuthorizationStatus("Authorization already in progress.");
                return;
            }

            _forceDeviceAuthorization = true;
            StartAuthorizationThread();
        }

        public string BotLogin
        {
            get
            {
                lock (_stateLock)
                {
                    return _state == null ? "" : _state.botLogin ?? "";
                }
            }
        }

        public string BotUserId
        {
            get
            {
                lock (_stateLock)
                {
                    return _state == null ? "" : _state.botUserId ?? "";
                }
            }
        }

        public bool IsAuthorized
        {
            get
            {
                string accessToken;
                string botLogin;
                return TryGetValidAccessToken(out accessToken, out botLogin);
            }
        }

        public void Load()
        {
            _shouldRun = true;
            LoadState();
            TwitchBornLog.Info("Twitch auth service loaded.");
        }

        public void Unload()
        {
            _shouldRun = false;

            if (_authThread != null && _authThread.IsAlive)
            {
                if (!_authThread.Join(1000))
                {
                    TwitchBornLog.Warning("Twitch auth thread did not stop cleanly within timeout.");
                }
            }

            _authThread = null;
            TwitchBornLog.Info("Twitch auth service unloaded.");
        }

        public void UpdateSingleton()
        {
            if (!_settingsOwner.EnableTwitchIntegration.Value)
            {
                return;
            }

            if (string.IsNullOrEmpty(GetConfiguredChannel()))
            {
                return;
            }

            string accessToken;
            string botLogin;

            if (TryGetValidAccessToken(out accessToken, out botLogin))
            {
                EnsureChannelValidation(GetConfiguredChannel(), accessToken);
                return;
            }

            if (_authThreadRunning)
            {
                return;
            }

            if (DateTime.UtcNow < _nextAutoAuthAttemptUtc)
            {
                return;
            }

            StartAuthorizationThread();
        }

        public bool TryGetValidAccessToken(out string accessToken, out string botLogin)
        {
            accessToken = "";
            botLogin = "";

            lock (_stateLock)
            {
                if (_state == null || !_state.HasAccessToken)
                {
                    return false;
                }

                if (!IsStateTokenStillUsable(_state))
                {
                    return false;
                }

                accessToken = _state.accessToken ?? "";
                botLogin = _state.botLogin ?? "";

                return !string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(botLogin);
            }
        }

        private void SetAuthorizationPrompt(string url, string userCode)
        {
            lock (_stateLock)
            {
                _authorizationUrl = url ?? "";
                _authorizationUserCode = userCode ?? "";
                _authorizationStatus = "Waiting for Twitch authorization.";
            }
        }

        private void ClearAuthorizationPrompt(string status)
        {
            lock (_stateLock)
            {
                _authorizationUrl = "";
                _authorizationUserCode = "";
                _authorizationStatus = status ?? "";
            }
        }

        private void SetAuthorizationStatus(string status)
        {
            lock (_stateLock)
            {
                _authorizationStatus = status ?? "";
            }
        }

        public void ForgetAuthorization()
        {
            Interlocked.Increment(ref _authorizationAttemptId);

            lock (_stateLock)
            {
                _state = null;
                _authorizationUrl = "";
                _authorizationUserCode = "";
                _authorizationStatus = "Not connected.";
            }

            _authThreadRunning = false;
            _forceDeviceAuthorization = false;
            _nextAutoAuthAttemptUtc = DateTime.UtcNow.AddSeconds(AutoAuthRetrySeconds);

            DeleteStateFile();
            TwitchBornLog.Info("Twitch authorization forgotten.");
        }

        public void MarkCurrentTokenInvalid(string reason)
        {
            lock (_stateLock)
            {
                if (_state != null)
                {
                    _state.accessToken = "";
                    _authorizationUrl = "";
                    _authorizationUserCode = "";
                    _authorizationStatus = string.IsNullOrEmpty(reason)
                        ? "Twitch authorization is no longer valid."
                        : reason;
                }
            }

            SaveState();
            _nextAutoAuthAttemptUtc = DateTime.UtcNow;
        }

        private void ClearStoredAuthorization(string status)
        {
            lock (_stateLock)
            {
                _state = null;
                _authorizationUrl = "";
                _authorizationUserCode = "";
                _authorizationStatus = status ?? "Not connected.";
            }

            DeleteStateFile();
        }

        private void StartAuthorizationThread()
        {
            _nextAutoAuthAttemptUtc = DateTime.UtcNow.AddSeconds(AutoAuthRetrySeconds);
            _authThreadRunning = true;

            var authorizationAttemptId = Interlocked.Increment(ref _authorizationAttemptId);

            _authThread = new Thread(() => RunAuthorizationThread(authorizationAttemptId));
            _authThread.IsBackground = true;
            _authThread.Name = "TwitchBorn Twitch Auth";
            _authThread.Start();
        }

        private void RunAuthorizationThread(int authorizationAttemptId)
        {
            try
            {
                if (!_forceDeviceAuthorization && TryRefreshStoredToken(authorizationAttemptId))
                {
                    return;
                }

                _forceDeviceAuthorization = false;
                RunDeviceCodeFlow(authorizationAttemptId);
            }
            catch (Exception exception)
            {
                if (IsCurrentAuthorizationAttempt(authorizationAttemptId))
                {
                    SetAuthorizationStatus("Twitch auth failed: " + exception.Message);
                }

                TwitchBornLog.Warning("Twitch auth failed: " + exception.Message);
            }
            finally
            {
                if (IsCurrentAuthorizationAttempt(authorizationAttemptId))
                {
                    _authThreadRunning = false;
                }
            }
        }

        private bool TryRefreshStoredToken(int authorizationAttemptId)
        {
            TwitchAuthState state;

            lock (_stateLock)
            {
                state = _state;
            }

            if (state == null || !state.HasRefreshToken)
            {
                return false;
            }

            TwitchBornLog.Info("Refreshing Twitch bot token.");

            var form = new Dictionary<string, string>
            {
                { "client_id", TwitchBornTwitchApplication.ClientId },
                { "grant_type", "refresh_token" },
                { "refresh_token", state.refreshToken }
            };

            var response = PostForm(TokenEndpoint, form);

            if (!response.IsSuccess)
            {
                TwitchBornLog.Warning("Twitch token refresh failed: " + response.Body);
                ClearStoredAuthorization("Twitch authorization expired or was revoked.");
                return false;
            }

            var tokenResponse = JsonUtility.FromJson<TwitchTokenResponse>(response.Body);

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.access_token))
            {
                TwitchBornLog.Warning("Twitch token refresh returned no access token.");
                ClearStoredAuthorization("Twitch authorization expired or was revoked.");
                return false;
            }

            SaveTokenResponse(tokenResponse, authorizationAttemptId);
            TwitchBornLog.Info("Twitch bot token refreshed.");
            return true;
        }

        private void RunDeviceCodeFlow(int authorizationAttemptId)
        {
            var form = new Dictionary<string, string>
            {
                { "client_id", TwitchBornTwitchApplication.ClientId },
                { "scopes", TwitchBornTwitchApplication.ScopeString }
            };

            var response = PostForm(DeviceEndpoint, form);

            if (!response.IsSuccess)
            {
                TwitchBornLog.Warning("Twitch device-code request failed: " + response.Body);
                return;
            }

            var deviceResponse = JsonUtility.FromJson<TwitchDeviceCodeResponse>(response.Body);

            if (deviceResponse == null || string.IsNullOrEmpty(deviceResponse.device_code))
            {
                TwitchBornLog.Warning("Twitch device-code response was invalid.");
                return;
            }

            var interval = deviceResponse.interval <= 0 ? 5 : deviceResponse.interval;
            var expiresAtUtc = DateTime.UtcNow.AddSeconds(deviceResponse.expires_in <= 0 ? 1800 : deviceResponse.expires_in);
            var authorizationUrl = BuildAuthorizationUrl(deviceResponse.verification_uri, deviceResponse.user_code);

            SetAuthorizationPrompt(authorizationUrl, deviceResponse.user_code);

            TwitchBornLog.Info("Twitch bot authorization required.");
            TwitchBornLog.Info("Open this URL: " + authorizationUrl);

            while (_shouldRun && IsCurrentAuthorizationAttempt(authorizationAttemptId) && DateTime.UtcNow < expiresAtUtc)
            {
                Thread.Sleep(interval * 1000);

                var tokenForm = new Dictionary<string, string>
                {
                    { "client_id", TwitchBornTwitchApplication.ClientId },
                    { "scopes", TwitchBornTwitchApplication.ScopeString },
                    { "device_code", deviceResponse.device_code },
                    { "grant_type", "urn:ietf:params:oauth:grant-type:device_code" }
                };

                var tokenResponse = PostForm(TokenEndpoint, tokenForm);

                if (tokenResponse.IsSuccess)
                {
                    var parsedToken = JsonUtility.FromJson<TwitchTokenResponse>(tokenResponse.Body);

                    if (parsedToken == null || string.IsNullOrEmpty(parsedToken.access_token))
                    {
                        TwitchBornLog.Warning("Twitch device-code token response was invalid.");
                        return;
                    }

                    SaveTokenResponse(parsedToken, authorizationAttemptId);
                    TwitchBornLog.Info("Twitch bot authorization completed.");
                    return;
                }

                if (tokenResponse.Body.Contains("authorization_pending"))
                {
                    continue;
                }

                if (tokenResponse.Body.Contains("slow_down"))
                {
                    interval += 5;
                    continue;
                }

                TwitchBornLog.Warning("Twitch device-code token request failed: " + tokenResponse.Body);
                return;
            }

            if (!IsCurrentAuthorizationAttempt(authorizationAttemptId))
            {
                TwitchBornLog.Info("Twitch bot authorization cancelled.");
                return;
            }

            ClearAuthorizationPrompt("Twitch authorization expired.");
            TwitchBornLog.Warning("Twitch bot authorization expired before completion.");
        }

        private void SaveTokenResponse(TwitchTokenResponse tokenResponse, int authorizationAttemptId)
        {
            if (!IsCurrentAuthorizationAttempt(authorizationAttemptId))
            {
                return;
            }

            var validatedToken = ValidateToken(tokenResponse.access_token);

            if (validatedToken == null || string.IsNullOrEmpty(validatedToken.login))
            {
                ClearStoredAuthorization("Twitch token validation failed.");
                return;
            }

            if (!IsCurrentAuthorizationAttempt(authorizationAttemptId))
            {
                return;
            }

            var state = new TwitchAuthState
            {
                accessToken = tokenResponse.access_token ?? "",
                refreshToken = tokenResponse.refresh_token ?? "",
                expiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(0, tokenResponse.expires_in - TokenExpirySafetySeconds)).ToString("o", CultureInfo.InvariantCulture),
                botLogin = validatedToken == null ? "" : validatedToken.login ?? "",
                botUserId = validatedToken == null ? "" : validatedToken.user_id ?? "",
                scopesCsv = tokenResponse.scope == null ? "" : string.Join(",", tokenResponse.scope)
            };

            lock (_stateLock)
            {
                if (!IsCurrentAuthorizationAttempt(authorizationAttemptId))
                {
                    return;
                }

                _state = state;
            }

            SaveState();

            ClearAuthorizationPrompt("Connected as " + state.botLogin + ".");
            TwitchBornLog.Info("Twitch bot connected as " + state.botLogin + ".");
        }

        private TwitchValidatedToken ValidateToken(string accessToken)
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                return null;
            }

            var request = (HttpWebRequest)WebRequest.Create(ValidateEndpoint);
            request.Method = "GET";
            request.Headers["Authorization"] = "OAuth " + accessToken;
            request.Timeout = 10000;

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    var body = reader.ReadToEnd();
                    return JsonUtility.FromJson<TwitchValidatedToken>(body);
                }
            }
            catch (WebException exception)
            {
                TwitchBornLog.Warning("Twitch token validation failed: " + ReadWebExceptionBody(exception));
                return null;
            }
        }

        private bool IsStateTokenStillUsable(TwitchAuthState state)
        {
            if (state == null || !state.HasAccessToken)
            {
                return false;
            }

            DateTime expiresAtUtc;

            if (!DateTime.TryParse(
                    state.expiresAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out expiresAtUtc))
            {
                return false;
            }

            return DateTime.UtcNow < expiresAtUtc;
        }

        private void LoadState()
        {
            try
            {
                var path = GetStateFilePath();

                if (!File.Exists(path))
                {
                    return;
                }

                var json = File.ReadAllText(path);
                var state = JsonUtility.FromJson<TwitchAuthState>(json);

                lock (_stateLock)
                {
                    _state = state;
                }

                if (state != null && !string.IsNullOrEmpty(state.botLogin))
                {
                    TwitchBornLog.Info("Loaded Twitch bot authorization for " + state.botLogin + ".");
                }
            }
            catch (Exception exception)
            {
                TwitchBornLog.Warning("Failed to load Twitch auth state: " + exception.Message);
            }
        }

        private void SaveState()
        {
            try
            {
                var path = GetStateFilePath();
                var directory = Path.GetDirectoryName(path);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                TwitchAuthState state;

                lock (_stateLock)
                {
                    state = _state;
                }

                if (state == null)
                {
                    return;
                }

                var json = JsonUtility.ToJson(state, true);
                File.WriteAllText(path, json);
            }
            catch (Exception exception)
            {
                TwitchBornLog.Warning("Failed to save Twitch auth state: " + exception.Message);
            }
        }

        private void DeleteStateFile()
        {
            try
            {
                var path = GetStateFilePath();

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                TwitchBornLog.Warning("Failed to delete Twitch auth state: " + exception.Message);
            }
        }

        private string GetStateFilePath()
        {
            return Path.Combine(Application.persistentDataPath, "TwitchBorn", "twitch_auth.json");
        }

        private string GetConfiguredChannel()
        {
            var value = _settingsOwner.TwitchChannel.Value;

            if (value == null)
            {
                return "";
            }

            value = value.Trim().ToLowerInvariant();

            if (value.StartsWith("#", StringComparison.Ordinal))
            {
                value = value.Substring(1);
            }

            return value;
        }

        private bool IsCurrentAuthorizationAttempt(int authorizationAttemptId)
        {
            return _authorizationAttemptId == authorizationAttemptId;
        }

        private static string BuildAuthorizationUrl(string verificationUri, string userCode)
        {
            var authorizationUrl = verificationUri ?? "";

            if (string.IsNullOrEmpty(authorizationUrl) || string.IsNullOrEmpty(userCode))
            {
                return authorizationUrl;
            }

            if (authorizationUrl.Contains("device-code"))
            {
                return authorizationUrl;
            }

            var separator = authorizationUrl.Contains("?") ? "&" : "?";
            return authorizationUrl + separator + "device-code=" + Uri.EscapeDataString(userCode);
        }

        private void EnsureChannelValidation(string channelLogin, string accessToken)
        {
            var normalizedChannel = NormalizeLogin(channelLogin);

            if (string.IsNullOrEmpty(normalizedChannel) || string.IsNullOrEmpty(accessToken))
            {
                ClearChannelValidation();
                return;
            }

            lock (_channelValidationLock)
            {
                if (string.Equals(_channelValidationLogin, normalizedChannel, StringComparison.Ordinal)
                    && (_channelValidationStatus == TwitchChannelValidationStatus.Valid
                        || _channelValidationStatus == TwitchChannelValidationStatus.Invalid
                        || _channelValidationStatus == TwitchChannelValidationStatus.Validating))
                {
                    return;
                }

                _channelValidationLogin = normalizedChannel;
                _validatedChannelLogin = "";
                _validatedChannelDisplayName = "";
                _channelValidationStatus = TwitchChannelValidationStatus.Validating;
            }

            if (_channelValidationThreadRunning)
            {
                return;
            }

            _channelValidationThreadRunning = true;
            _channelValidationThread = new Thread(() => RunChannelValidation(normalizedChannel, accessToken));
            _channelValidationThread.IsBackground = true;
            _channelValidationThread.Name = "TwitchBorn Channel Validation";
            _channelValidationThread.Start();
        }

        private void RunChannelValidation(string channelLogin, string accessToken)
        {
            try
            {
                var requestUrl = UsersEndpoint + "?login=" + Uri.EscapeDataString(channelLogin);
                var request = (HttpWebRequest)WebRequest.Create(requestUrl);
                request.Method = "GET";
                request.Headers["Authorization"] = "Bearer " + accessToken;
                request.Headers["Client-Id"] = TwitchBornTwitchApplication.ClientId;
                request.Timeout = 10000;

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    var body = reader.ReadToEnd();

                    string parsedLogin;
                    string parsedDisplayName;

                    if (TryReadTwitchUserFromHelixUsersResponse(body, out parsedLogin, out parsedDisplayName))
                    {
                        lock (_channelValidationLock)
                        {
                            if (string.Equals(_channelValidationLogin, channelLogin, StringComparison.Ordinal))
                            {
                                _validatedChannelLogin = string.IsNullOrEmpty(parsedLogin) ? channelLogin : parsedLogin;
                                _validatedChannelDisplayName = string.IsNullOrEmpty(parsedDisplayName)
                                    ? _validatedChannelLogin
                                    : parsedDisplayName;
                                _channelValidationStatus = TwitchChannelValidationStatus.Valid;
                            }
                        }

                        TwitchBornLog.Info("Validated Twitch channel " + channelLogin + ".");
                        return;
                    }

                    if (IsExplicitEmptyHelixUsersResponse(body))
                    {
                        lock (_channelValidationLock)
                        {
                            if (string.Equals(_channelValidationLogin, channelLogin, StringComparison.Ordinal))
                            {
                                _validatedChannelLogin = "";
                                _validatedChannelDisplayName = "";
                                _channelValidationStatus = TwitchChannelValidationStatus.Invalid;
                            }
                        }

                        TwitchBornLog.Warning("Twitch channel was not found: " + channelLogin + ".");
                        return;
                    }

                    lock (_channelValidationLock)
                    {
                        if (string.Equals(_channelValidationLogin, channelLogin, StringComparison.Ordinal))
                        {
                            _validatedChannelLogin = "";
                            _validatedChannelDisplayName = "";
                            _channelValidationStatus = TwitchChannelValidationStatus.Failed;
                        }
                    }

                    TwitchBornLog.Warning("Twitch channel validation returned an unexpected response for " + channelLogin + ": " + body);
                }
            }
            catch (WebException exception)
            {
                lock (_channelValidationLock)
                {
                    if (string.Equals(_channelValidationLogin, channelLogin, StringComparison.Ordinal))
                    {
                        _validatedChannelLogin = "";
                        _validatedChannelDisplayName = "";
                        _channelValidationStatus = TwitchChannelValidationStatus.Failed;
                    }
                }

                TwitchBornLog.Warning("Twitch channel validation failed: " + ReadWebExceptionBody(exception));
            }
            catch (Exception exception)
            {
                lock (_channelValidationLock)
                {
                    if (string.Equals(_channelValidationLogin, channelLogin, StringComparison.Ordinal))
                    {
                        _validatedChannelLogin = "";
                        _validatedChannelDisplayName = "";
                        _channelValidationStatus = TwitchChannelValidationStatus.Failed;
                    }
                }

                TwitchBornLog.Warning("Twitch channel validation failed: " + exception.Message);
            }
            finally
            {
                _channelValidationThreadRunning = false;
            }
        }

        private static bool TryReadTwitchUserFromHelixUsersResponse(string body, out string login, out string displayName)
        {
            login = "";
            displayName = "";

            if (string.IsNullOrEmpty(body))
            {
                return false;
            }

            var dataIndex = body.IndexOf("\"data\"", StringComparison.Ordinal);

            if (dataIndex < 0)
            {
                return false;
            }

            var arrayStartIndex = body.IndexOf('[', dataIndex);

            if (arrayStartIndex < 0)
            {
                return false;
            }

            var firstObjectStartIndex = body.IndexOf('{', arrayStartIndex);

            if (firstObjectStartIndex < 0)
            {
                return false;
            }

            var firstObjectEndIndex = FindMatchingJsonObjectEnd(body, firstObjectStartIndex);

            if (firstObjectEndIndex < 0)
            {
                return false;
            }

            var firstObject = body.Substring(firstObjectStartIndex, firstObjectEndIndex - firstObjectStartIndex + 1);

            login = ReadJsonStringProperty(firstObject, "login");
            displayName = ReadJsonStringProperty(firstObject, "display_name");

            return !string.IsNullOrEmpty(login);
        }

        private static bool IsExplicitEmptyHelixUsersResponse(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return false;
            }

            var compactBody = RemoveJsonWhitespaceOutsideStrings(body);
            return compactBody.Contains("\"data\":[]");
        }

        private static int FindMatchingJsonObjectEnd(string text, int objectStartIndex)
        {
            var depth = 0;
            var inString = false;
            var escaped = false;

            for (var index = objectStartIndex; index < text.Length; index++)
            {
                var character = text[index];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (character == '\\' && inString)
                {
                    escaped = true;
                    continue;
                }

                if (character == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                {
                    continue;
                }

                if (character == '{')
                {
                    depth++;
                    continue;
                }

                if (character == '}')
                {
                    depth--;

                    if (depth == 0)
                    {
                        return index;
                    }
                }
            }

            return -1;
        }

        private static string ReadJsonStringProperty(string jsonObject, string propertyName)
        {
            if (string.IsNullOrEmpty(jsonObject) || string.IsNullOrEmpty(propertyName))
            {
                return "";
            }

            var propertyPattern = "\"" + propertyName + "\"";
            var propertyIndex = jsonObject.IndexOf(propertyPattern, StringComparison.Ordinal);

            if (propertyIndex < 0)
            {
                return "";
            }

            var colonIndex = jsonObject.IndexOf(':', propertyIndex + propertyPattern.Length);

            if (colonIndex < 0)
            {
                return "";
            }

            var valueStartIndex = jsonObject.IndexOf('"', colonIndex + 1);

            if (valueStartIndex < 0)
            {
                return "";
            }

            var builder = new StringBuilder();
            var escaped = false;

            for (var index = valueStartIndex + 1; index < jsonObject.Length; index++)
            {
                var character = jsonObject[index];

                if (escaped)
                {
                    switch (character)
                    {
                        case '"':
                            builder.Append('"');
                            break;

                        case '\\':
                            builder.Append('\\');
                            break;

                        case '/':
                            builder.Append('/');
                            break;

                        case 'b':
                            builder.Append('\b');
                            break;

                        case 'f':
                            builder.Append('\f');
                            break;

                        case 'n':
                            builder.Append('\n');
                            break;

                        case 'r':
                            builder.Append('\r');
                            break;

                        case 't':
                            builder.Append('\t');
                            break;

                        default:
                            builder.Append(character);
                            break;
                    }

                    escaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == '"')
                {
                    return builder.ToString();
                }

                builder.Append(character);
            }

            return "";
        }

        private static string RemoveJsonWhitespaceOutsideStrings(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            var builder = new StringBuilder();
            var inString = false;
            var escaped = false;

            for (var index = 0; index < text.Length; index++)
            {
                var character = text[index];

                if (escaped)
                {
                    builder.Append(character);
                    escaped = false;
                    continue;
                }

                if (character == '\\' && inString)
                {
                    builder.Append(character);
                    escaped = true;
                    continue;
                }

                if (character == '"')
                {
                    builder.Append(character);
                    inString = !inString;
                    continue;
                }

                if (!inString && char.IsWhiteSpace(character))
                {
                    continue;
                }

                builder.Append(character);
            }

            return builder.ToString();
        }

        private void ClearChannelValidation()
        {
            lock (_channelValidationLock)
            {
                _channelValidationLogin = "";
                _validatedChannelLogin = "";
                _validatedChannelDisplayName = "";
                _channelValidationStatus = TwitchChannelValidationStatus.Unknown;
            }
        }

        private static string NormalizeLogin(string login)
        {
            if (string.IsNullOrEmpty(login))
            {
                return "";
            }

            return login.Trim().TrimStart('#').ToLowerInvariant();
        }

        private static TwitchHttpResponse PostForm(string url, Dictionary<string, string> form)
        {
            var body = BuildFormBody(form);
            var bodyBytes = Encoding.UTF8.GetBytes(body);

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = bodyBytes.Length;
            request.Timeout = 15000;

            using (var requestStream = request.GetRequestStream())
            {
                requestStream.Write(bodyBytes, 0, bodyBytes.Length);
            }

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var responseStream = response.GetResponseStream())
                using (var reader = new StreamReader(responseStream))
                {
                    return new TwitchHttpResponse((int)response.StatusCode, reader.ReadToEnd());
                }
            }
            catch (WebException exception)
            {
                var statusCode = 0;

                if (exception.Response is HttpWebResponse response)
                {
                    statusCode = (int)response.StatusCode;
                }

                return new TwitchHttpResponse(statusCode, ReadWebExceptionBody(exception));
            }
        }

        private static string BuildFormBody(Dictionary<string, string> form)
        {
            var builder = new StringBuilder();
            var first = true;

            foreach (var pair in form)
            {
                if (!first)
                {
                    builder.Append("&");
                }

                builder.Append(Uri.EscapeDataString(pair.Key));
                builder.Append("=");
                builder.Append(Uri.EscapeDataString(pair.Value ?? ""));
                first = false;
            }

            return builder.ToString();
        }

        private static string ReadWebExceptionBody(WebException exception)
        {
            if (exception == null || exception.Response == null)
            {
                return exception == null ? "" : exception.Message;
            }

            try
            {
                using (var stream = exception.Response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch
            {
                return exception.Message;
            }
        }
    }
}