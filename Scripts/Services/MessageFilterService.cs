using System;
using System.Collections.Generic;
using TwitchBorn.Core;
using TwitchBorn.Settings;

namespace TwitchBorn.Services
{
    public class MessageFilterService
    {
        private readonly MessageFilteringSettingsOwner _settingsOwner;
        private string[] _blacklistedTerms = Array.Empty<string>();

        public MessageFilterService(MessageFilteringSettingsOwner settingsOwner)
        {
            _settingsOwner = settingsOwner;
            RebuildBlacklistedTermsCache();

            if (_settingsOwner != null && _settingsOwner.BlacklistedTerms != null)
            {
                _settingsOwner.BlacklistedTerms.ValueChanged += OnBlacklistedTermsChanged;
            }
        }

        public string SanitizeViewerText(string value)
        {
            return TwitchBornTextSanitizer.SanitizeViewerText(value, _blacklistedTerms);
        }

        private void OnBlacklistedTermsChanged(object sender, string value)
        {
            RebuildBlacklistedTermsCache();
        }

        private void RebuildBlacklistedTermsCache()
        {
            var rawValue = _settingsOwner == null || _settingsOwner.BlacklistedTerms == null
                ? ""
                : _settingsOwner.BlacklistedTerms.Value;

            _blacklistedTerms = ParseBlacklistedTerms(rawValue);
        }

        private static string[] ParseBlacklistedTerms(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            var uniqueTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var terms = new List<string>();

            foreach (var rawTerm in value.Split(',', '\n', '\r'))
            {
                var term = TwitchBornTextSanitizer.SanitizePlainText(rawTerm, -1);

                if (string.IsNullOrEmpty(term) || !uniqueTerms.Add(term))
                {
                    continue;
                }

                terms.Add(term);
            }

            return terms.ToArray();
        }
    }
}
