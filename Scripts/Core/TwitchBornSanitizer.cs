using System.Text;
using System.Text.RegularExpressions;

namespace TwitchBorn.Core
{
    public static class TwitchBornTextSanitizer
    {
        private const int HexColorTagLength = 9;
        private const string LinkReplacementText = "[link]";
        private const string BlockedWordReplacementText = "[blocked]";
        private static readonly Regex RichTextLinkRegex = new Regex(
            @"<\s*link\b[^>]*>.*?<\s*/\s*link\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex UrlRegex = new Regex(
            @"\b((?:https?://|www\.)[^\s<>()]+|[a-z0-9][a-z0-9-]*(?:\.[a-z0-9][a-z0-9-]*)+(?:/[^\s<>()]*)?)",
            RegexOptions.IgnoreCase);

        public static string SanitizePlainText(string value, int maxLength)
        {
            var sanitized = CleanSingleLine(RemoveEmojis(StripRichText(ReplaceHyperlinks(value))));

            return TruncateSafely(sanitized, maxLength);
        }

        public static string SanitizeViewerText(
            string value,
            string[] blacklistedTerms)
        {
            return CleanSingleLine(
                RemoveEmojis(
                    StripRichText(
                        ReplaceBlacklistedTerms(
                            ReplaceHyperlinks(value),
                            blacklistedTerms))));
        }

        public static string SanitizeBeaverEntityName(string value, int maxVisibleLength)
        {
            if (value == null)
            {
                return "";
            }

            var trimmed = value.Trim();
            var colorTag = "";
            var nameText = trimmed;

            if (TryExtractLeadingHexColorTag(trimmed, out colorTag, out nameText))
            {
                nameText = nameText.TrimStart();
            }

            var plainName = TruncateSafely(
                CleanSingleLine(RemoveEmojis(StripRichText(ReplaceHyperlinks(nameText)))),
                maxVisibleLength);

            if (string.IsNullOrEmpty(plainName))
            {
                return "";
            }

            return colorTag + plainName;
        }

        public static string SanitizeDisplayName(
            string value,
            int maxLength,
            out string leadingHexColor)
        {
            leadingHexColor = "";

            if (value == null)
            {
                return "";
            }

            var trimmed = value.Trim();
            var nameText = trimmed;

            if (TryExtractLeadingHexColorTag(trimmed, out var colorTag, out nameText))
            {
                leadingHexColor = "#" + colorTag.Substring(2, 6);
                nameText = nameText.TrimStart();
            }

            var sanitized = CleanSingleLine(RemoveEmojis(StripRichText(ReplaceHyperlinks(nameText))));

            return TruncateSafely(sanitized, maxLength);
        }

        public static string ReplaceHyperlinks(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            var withoutRichTextLinks = RichTextLinkRegex.Replace(value, LinkReplacementText);
            return UrlRegex.Replace(withoutRichTextLinks, LinkReplacementText);
        }

        public static string ReplaceBlacklistedTerms(string value, string[] blacklistedTerms)
        {
            if (string.IsNullOrEmpty(value) || blacklistedTerms == null || blacklistedTerms.Length == 0)
            {
                return value ?? "";
            }

            var sanitized = value;

            foreach (var term in blacklistedTerms)
            {
                if (string.IsNullOrEmpty(term))
                {
                    continue;
                }

                sanitized = ReplaceBlacklistedTerm(sanitized, term);
            }

            return sanitized;
        }

        public static string StripRichText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            var builder = new StringBuilder(value.Length);
            var index = 0;

            while (index < value.Length)
            {
                int tagEnd;

                if (value[index] == '<' && TryGetRichTextTagEnd(value, index, out tagEnd))
                {
                    index = tagEnd + 1;
                    continue;
                }

                builder.Append(value[index]);
                index++;
            }

            return builder.ToString();
        }

        public static string RemoveEmojis(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            var builder = new StringBuilder(value.Length);
            var index = 0;

            while (index < value.Length)
            {
                int nextIndex;

                if (TrySkipKeycapEmoji(value, index, out nextIndex))
                {
                    index = nextIndex;
                    continue;
                }

                int charLength;
                var codePoint = ReadCodePoint(value, index, out charLength);

                if (IsEmojiCodePoint(codePoint))
                {
                    index = SkipEmojiSequenceTail(value, index + charLength);
                    continue;
                }

                if (IsEmojiSequenceCodePoint(codePoint))
                {
                    index += charLength;
                    continue;
                }

                builder.Append(value, index, charLength);
                index += charLength;
            }

            return builder.ToString();
        }

        public static bool TryExtractLeadingHexColorTag(
            string value,
            out string colorTag,
            out string remainder)
        {
            colorTag = "";
            remainder = value ?? "";

            if (string.IsNullOrEmpty(value) || value.Length < HexColorTagLength)
            {
                return false;
            }

            if (value[0] != '<' || value[1] != '#' || value[HexColorTagLength - 1] != '>')
            {
                return false;
            }

            for (var i = 2; i < HexColorTagLength - 1; i++)
            {
                if (!IsHex(value[i]))
                {
                    return false;
                }
            }

            colorTag = value.Substring(0, HexColorTagLength);
            remainder = value.Substring(HexColorTagLength);
            return true;
        }

        private static string CleanSingleLine(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            var sanitized = value.Trim();

            sanitized = sanitized.Replace("\r", "");
            sanitized = sanitized.Replace("\n", " ");
            sanitized = sanitized.Replace("\t", " ");

            while (sanitized.Contains("  "))
            {
                sanitized = sanitized.Replace("  ", " ");
            }

            return sanitized.Trim();
        }

        private static string TruncateSafely(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            if (maxLength < 0 || value.Length <= maxLength)
            {
                return value;
            }

            if (maxLength == 0)
            {
                return "";
            }

            var length = maxLength;

            if (char.IsHighSurrogate(value[length - 1]))
            {
                length--;
            }

            return length <= 0 ? "" : value.Substring(0, length);
        }

        private static bool TrySkipKeycapEmoji(
            string value,
            int startIndex,
            out int nextIndex)
        {
            nextIndex = startIndex;

            if (startIndex < 0 || startIndex >= value.Length)
            {
                return false;
            }

            var first = value[startIndex];

            if (!IsKeycapBase(first))
            {
                return false;
            }

            var index = startIndex + 1;

            if (index < value.Length)
            {
                int charLength;
                var codePoint = ReadCodePoint(value, index, out charLength);

                if (IsVariationSelector(codePoint))
                {
                    index += charLength;
                }
            }

            if (index >= value.Length)
            {
                return false;
            }

            int keycapLength;

            if (ReadCodePoint(value, index, out keycapLength) != 0x20E3)
            {
                return false;
            }

            nextIndex = index + keycapLength;
            return true;
        }

        private static int SkipEmojiSequenceTail(string value, int index)
        {
            while (index < value.Length)
            {
                int charLength;
                var codePoint = ReadCodePoint(value, index, out charLength);

                if (IsEmojiSequenceCodePoint(codePoint))
                {
                    index += charLength;
                    continue;
                }

                if (codePoint == 0x200D)
                {
                    index += charLength;

                    if (index < value.Length)
                    {
                        int joinedLength;
                        ReadCodePoint(value, index, out joinedLength);
                        index += joinedLength;
                    }

                    continue;
                }

                return index;
            }

            return index;
        }

        private static int ReadCodePoint(string value, int index, out int charLength)
        {
            if (index + 1 < value.Length && char.IsHighSurrogate(value[index]) && char.IsLowSurrogate(value[index + 1]))
            {
                charLength = 2;
                return char.ConvertToUtf32(value[index], value[index + 1]);
            }

            charLength = 1;
            return value[index];
        }

        private static bool IsEmojiCodePoint(int codePoint)
        {
            return (codePoint >= 0x1F000 && codePoint <= 0x1FAFF)
                || (codePoint >= 0x2600 && codePoint <= 0x27BF);
        }

        private static bool IsEmojiSequenceCodePoint(int codePoint)
        {
            return IsVariationSelector(codePoint)
                || (codePoint >= 0x1F3FB && codePoint <= 0x1F3FF)
                || codePoint == 0x20E3;
        }

        private static bool IsVariationSelector(int codePoint)
        {
            return codePoint == 0xFE0E
                || codePoint == 0xFE0F
                || (codePoint >= 0xE0100 && codePoint <= 0xE01EF);
        }

        private static bool IsKeycapBase(char value)
        {
            return value == '#'
                || value == '*'
                || (value >= '0' && value <= '9');
        }

        private static string ReplaceBlacklistedTerm(string value, string term)
        {
            var escapedTerm = Regex.Escape(term);
            var pattern = ShouldUseWordBoundaries(term)
                ? "(?<![A-Za-z0-9])" + escapedTerm + "(?![A-Za-z0-9])"
                : escapedTerm;

            return Regex.Replace(
                value,
                pattern,
                BlockedWordReplacementText,
                RegexOptions.IgnoreCase);
        }

        private static bool ShouldUseWordBoundaries(string term)
        {
            return !string.IsNullOrEmpty(term)
                && IsAsciiLetterOrDigit(term[0])
                && IsAsciiLetterOrDigit(term[term.Length - 1]);
        }

        private static bool TryGetRichTextTagEnd(
            string value,
            int startIndex,
            out int tagEnd)
        {
            tagEnd = -1;

            var maxTagEnd = startIndex + 64;

            for (var i = startIndex + 1; i < value.Length && i <= maxTagEnd; i++)
            {
                if (value[i] != '>')
                {
                    continue;
                }

                if (!IsPlausibleRichTextTag(value, startIndex + 1, i - startIndex - 1))
                {
                    return false;
                }

                tagEnd = i;
                return true;
            }

            return false;
        }

        private static bool IsPlausibleRichTextTag(
            string value,
            int startIndex,
            int length)
        {
            if (length <= 0 || startIndex < 0 || startIndex + length > value.Length)
            {
                return false;
            }

            var first = value[startIndex];

            if (first == '#')
            {
                if (length != 7)
                {
                    return false;
                }

                for (var i = startIndex + 1; i < startIndex + length; i++)
                {
                    if (!IsHex(value[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (first == '/')
            {
                return length > 1 && IsAsciiLetter(value[startIndex + 1]);
            }

            return IsAsciiLetter(first);
        }

        private static bool IsAsciiLetterOrDigit(char value)
        {
            return IsAsciiLetter(value) || (value >= '0' && value <= '9');
        }

        private static bool IsAsciiLetter(char value)
        {
            return (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z');
        }

        private static bool IsHex(char value)
        {
            return (value >= '0' && value <= '9')
                   || (value >= 'a' && value <= 'f')
                   || (value >= 'A' && value <= 'F');
        }
    }
}
