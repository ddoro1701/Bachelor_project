using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WebApplication1.Services
{
    public class TextPreprocessor
    {
        private static readonly Dictionary<char, char> OcrMap = new()
        {
            ['0'] = 'o', ['O'] = 'o',
            ['1'] = 'l', ['I'] = 'l', ['l'] = 'l',
            ['5'] = 's', ['S'] = 's',
            ['8'] = 'b', ['B'] = 'b',
        };

        // Titel und Adressstopwörter zum Ausfiltern
        private static readonly HashSet<string> Titles = new(StringComparer.OrdinalIgnoreCase)
        { "mr","mrs","ms","miss","dr","prof","professor","sir","madam","fr","sr","jr" };

        private static readonly HashSet<string> AddressStop = new(StringComparer.OrdinalIgnoreCase)
        {
            "road","rd","street","st","lane","ln","avenue","ave","way","close","cl","drive","dr",
            "mold","wrexham","united","kingdom","uk","ll","post","code","postcode","ll11","2aw",
            "phone","telefon","tel","department","school","building","room","postcode","ll11","ll12"
        };

        // Häufige Kosenamen → Kanonisch
        private static readonly Dictionary<string,string> NameMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["dan"] = "daniel", ["danny"] = "daniel",
            ["mike"] = "michael", ["matt"] = "matthew",
            ["tom"] = "thomas", ["nick"] = "nicholas",
            ["alex"] = "alexander", ["andy"] = "andrew",
            ["kate"] = "katherine", ["katy"] = "katherine",
            ["beth"] = "elizabeth", ["liz"] = "elizabeth"
        };

        // Normalize text (lowercase, strip diacritics, fix OCR)
        public string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            // De-obfuscate common “at/dot” encodings before normalization
            string t = input.Replace("(at)", "@", StringComparison.OrdinalIgnoreCase)
                            .Replace("[at]", "@", StringComparison.OrdinalIgnoreCase)
                            .Replace(" at ", " @ ", StringComparison.OrdinalIgnoreCase)
                            .Replace("(dot)", ".", StringComparison.OrdinalIgnoreCase)
                            .Replace("[dot]", ".", StringComparison.OrdinalIgnoreCase)
                            .Replace(" dot ", " . ", StringComparison.OrdinalIgnoreCase);

            // Unicode → remove diacritics
            t = t.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(t.Length);
            foreach (var ch in t)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(ch);
                }
            }
            t = sb.ToString().Normalize(NormalizationForm.FormC);

            // Lower + OCR map
            t = new string(t.ToLowerInvariant().Select(c => OcrMap.TryGetValue(c, out var m) ? m : c).ToArray());

            // Collapse whitespace
            t = Regex.Replace(t, @"\s+", " ").Trim();

            return t;
        }

        // Extract explicit or obfuscated email candidates
        public List<string> ExtractEmailCandidates(string rawText)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(rawText)) return list;

            var emailRx = new Regex(@"\b[a-z0-9._%+\-]+@[a-z0-9.\-]+\.[a-z]{2,}\b", RegexOptions.IgnoreCase);
            foreach (Match m in emailRx.Matches(rawText))
                list.Add(m.Value);

            string deobf = rawText.Replace("[at]", "@", StringComparison.OrdinalIgnoreCase)
                                  .Replace("(at)", "@", StringComparison.OrdinalIgnoreCase)
                                  .Replace(" at ", "@", StringComparison.OrdinalIgnoreCase)
                                  .Replace("[dot]", ".", StringComparison.OrdinalIgnoreCase)
                                  .Replace("(dot)", ".", StringComparison.OrdinalIgnoreCase)
                                  .Replace(" dot ", ".", StringComparison.OrdinalIgnoreCase);
            foreach (Match m in emailRx.Matches(deobf))
                list.Add(m.Value);

            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        // Tokenize name string: remove titles, address words, punctuation, map nicknames
        public string CleanNameLike(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var t = Normalize(s);
            // remove punctuation except spaces
            t = Regex.Replace(t, @"[^a-z\s]", " ");
            var tokens = t.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                          .Where(tok => !Titles.Contains(tok) && !AddressStop.Contains(tok))
                          .Select(tok => NameMap.TryGetValue(tok, out var mapped) ? mapped : tok)
                          .ToArray();
            return string.Join(" ", tokens);
        }

        // Extract probable name n-grams (2-3 tokens) from text
        public List<string> ExtractNameCandidates(string normalized)
        {
            if (string.IsNullOrWhiteSpace(normalized)) return new();

            var clean = Regex.Replace(normalized, @"[^a-z\s.]", " "); // keep dot for initials
            // strip titles/address
            clean = string.Join(" ", clean.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                          .Where(tok => !Titles.Contains(tok) && !AddressStop.Contains(tok)));

            var tokens = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var windows = new List<string>();

            // two- and three-token windows first (more reliable than singles)
            for (int i = 0; i < tokens.Length; i++)
            {
                if (i + 1 < tokens.Length)
                    windows.Add(tokens[i] + " " + tokens[i + 1]);
                if (i + 2 < tokens.Length)
                    windows.Add(tokens[i] + " " + tokens[i + 1] + " " + tokens[i + 2]);
            }

            // singles (likely surnames)
            windows.AddRange(tokens.Where(t => t.Length >= 3));

            return windows.Distinct().ToList();
        }

        // Noise-Pattern
        private static readonly Regex UkPostcode = new(@"\b[A-Z]{1,2}\d{1,2}[A-Z]?\s*\d[A-Z]{2}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled); // LL11 2AW etc.
        private static readonly Regex Phone = new(@"\+?\d[\d\s\-()]{6,}", RegexOptions.Compiled);
        private static readonly Regex Tracking = new(@"\b(1Z[0-9A-Z]{16}|TBA[0-9A-Z]+|[A-Z]{2}\d{9}GB|\d{12}|\d{14}|\d{15}|\d{20}|\d{22})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex OrderLike = new(@"\b(order|po|p/o|invoice|ref|reference|consignment|shipment|track|id|no\.?)\s*[:#]?\s*[A-Z0-9\-]{4,}\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ShipWords = new(@"\b(ship\s*to|ship\s*from|delivery|address|dept|department|school|building|room)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex MostlyDigits = new(@"^\s*[\d\W]{6,}\s*$", RegexOptions.Compiled);

        private static bool IsNoiseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return true;
            var s = line.Trim();
            return UkPostcode.IsMatch(s)
                   || Phone.IsMatch(s)
                   || Tracking.IsMatch(s)
                   || OrderLike.IsMatch(s)
                   || ShipWords.IsMatch(s)
                   || MostlyDigits.IsMatch(s);
        }

        // Preferiere obere Labelzeilen, filtere Noise, reinige Tokens
        public List<string> PreferTopLines(List<string>? rawLines)
        {
            if (rawLines == null || rawLines.Count == 0) return new();
            return rawLines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Take(8) // etwas großzügiger
                .Where(l => !IsNoiseLine(l))
                .Select(CleanNameLike)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // Map legacy/synonym domains to canonical
        public string NormalizeDomain(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return email;
            var parts = email.Split('@');
            if (parts.Length != 2) return email;

            var user = parts[0];
            var domain = parts[1].ToLowerInvariant();

            domain = domain
                .Replace("glyndwr.ac.uk", "wrexham.ac.uk")
                .Replace("wrexhamglyndwr.ac.uk", "wrexham.ac.uk");

            return $"{user}@{domain}";
        }
    }
}