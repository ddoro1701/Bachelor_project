using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    public class LecturerMatcher
    {
        private readonly LecturerService _lecturerService;
        private readonly TextPreprocessor _prep;
        private const double Accept = 0.87;
        private const double WeakAccept = 0.82;

        public LecturerMatcher(LecturerService lecturerService, TextPreprocessor prep)
        {
            _lecturerService = lecturerService;
            _prep = prep;
        }

        // Back-compat: old callers (string only)
        public async Task<string?> FindLecturerEmailAsync(string ocrText)
        {
            return await FindLecturerEmailAsync(ocrText, null);
        }

        // New: with optional lines to prioritize top-of-label
        public async Task<string?> FindLecturerEmailAsync(string ocrText, List<string>? lines)
        {
            if (string.IsNullOrWhiteSpace(ocrText)) return null;

            // 1) Emails fast-path
            var emailsInText = _prep.ExtractEmailCandidates(ocrText)
                                    .Select(_prep.NormalizeDomain)
                                    .ToList();
            if (emailsInText.Count > 0)
            {
                var lecturers = await _lecturerService.GetAllLecturersAsync();
                var set = new HashSet<string>(lecturers.Select(l => l.Email), StringComparer.OrdinalIgnoreCase);
                var hit = emailsInText.FirstOrDefault(e => set.Contains(e));
                if (hit != null) return hit;

                var org = lecturers.Select(l => l.Email.Split('@').Last())
                                   .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var orgHit = emailsInText.FirstOrDefault(e => org.Contains(e.Split('@').Last(), StringComparer.OrdinalIgnoreCase));
                if (orgHit != null) return orgHit;
            }

            // 2) Name candidates
            string normalized = _prep.Normalize(ocrText);
            var windows = _prep.ExtractNameCandidates(normalized);

            // Boost windows coming from top lines on the label (if provided)
            var topLines = _prep.PreferTopLines(lines);
            var boosted = new HashSet<string>(topLines, StringComparer.OrdinalIgnoreCase);
            var all = await _lecturerService.GetAllLecturersAsync();

            string? bestEmail = null;
            double bestScore = -1;

            foreach (var lec in all)
            {
                if (string.IsNullOrWhiteSpace(lec.Email)) continue;

                var firstRaw = lec.FirstName ?? "";
                var lastRaw  = lec.LastName  ?? "";
                var legacy   = lec.Name ?? "";

                var composed = (!string.IsNullOrWhiteSpace(firstRaw) || !string.IsNullOrWhiteSpace(lastRaw))
                    ? $"{firstRaw} {lastRaw}".Trim()
                    : legacy;

                if (string.IsNullOrWhiteSpace(composed)) continue;

                var first = _prep.CleanNameLike(firstRaw);
                var last  = _prep.CleanNameLike(lastRaw);
                var full  = _prep.CleanNameLike(composed);

                if (string.IsNullOrWhiteSpace(last) && !string.IsNullOrWhiteSpace(full))
                {
                    // Fallback: try to split legacy full name
                    var parts = full.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2) { first = string.Join(' ', parts[..^1]); last = parts[^1]; }
                }

                double maxSim = 0;

                foreach (var cand in windows)
                {
                    var c = _prep.CleanNameLike(cand);
                    if (string.IsNullOrWhiteSpace(c)) continue;

                    // First/Last similarities (best token-to-token)
                    double firstSim = 0;
                    double lastSim  = 0;
                    if (!string.IsNullOrWhiteSpace(first))
                        firstSim = SimilarityBestToken(first, c);
                    if (!string.IsNullOrWhiteSpace(last))
                        lastSim  = SimilarityBestToken(last, c);

                    // Full-name similarity
                    double fullSim = JaroWinkler(full, c) * 0.9 + LevenshteinRatio(full, c) * 0.1;

                    // Initialen-Match (z.B. D. Doroschenko)
                    bool initialsMatch = InitialsMatch(first, last, c);

                    // Titel-Boost falls Kandidat Titel enthielt (bereits gefiltert, aber heuristischer Bonus)
                    bool titleBoost = Regex.IsMatch(cand, @"\b(dr|prof|professor)\b", RegexOptions.IgnoreCase);

                    // Gewichtete Kombination (Nachname am wichtigsten)
                    double sim = 0.50 * lastSim + 0.35 * firstSim + 0.15 * fullSim
                                 + (initialsMatch ? 0.02 : 0.0)
                                 + (titleBoost ? 0.01 : 0.0);

                    // Boost für Top-of-label Kandidaten
                    if (boosted.Contains(c)) sim += 0.02;

                    if (sim > maxSim) maxSim = sim;
                    if (maxSim > 0.995) break;
                }

                if (maxSim > bestScore)
                {
                    bestScore = maxSim;
                    bestEmail = lec.Email;
                }
            }

            if (bestScore >= Accept || (bestScore >= WeakAccept && HasShortName(all, bestEmail)))
                return _prep.NormalizeDomain(bestEmail!);

            return null;
        }

        private static bool InitialsMatch(string first, string last, string candidate)
        {
            if (string.IsNullOrWhiteSpace(last)) return false;
            var lastInitial = last.FirstOrDefault();
            var firstInitial = first.FirstOrDefault();
            if (lastInitial == default) return false;

            // Patterns: "d. doroschenko", "doroschenko, d", "d doroschenko"
            return Regex.IsMatch(candidate, $@"\b{Regex.Escape(last)}\b", RegexOptions.IgnoreCase)
                   && (firstInitial == default
                       || Regex.IsMatch(candidate, $@"\b{char.ToLowerInvariant(firstInitial)}\.?\s+\b{Regex.Escape(last)}\b", RegexOptions.IgnoreCase)
                       || Regex.IsMatch(candidate, $@"\b{Regex.Escape(last)}\b[, ]\s*{char.ToLowerInvariant(firstInitial)}\.?", RegexOptions.IgnoreCase));
        }

        private static double SimilarityBestToken(string needle, string haystack)
        {
            var tokens = haystack.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            double best = 0;
            foreach (var tok in tokens)
            {
                double jw = JaroWinkler(needle, tok);
                double lev = LevenshteinRatio(needle, tok);
                best = Math.Max(best, 0.7 * jw + 0.3 * lev);
            }
            return best;
        }

        private bool HasShortName(IEnumerable<Lecturer> all, string? email)
        {
            var lec = all.FirstOrDefault(l => l.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            if (lec == null) return false;
            var composed = (!string.IsNullOrWhiteSpace(lec.FirstName) || !string.IsNullOrWhiteSpace(lec.LastName))
                ? $"{lec.FirstName} {lec.LastName}".Trim()
                : (lec.Name ?? string.Empty);
            var n = _prep.CleanNameLike(composed);
            return n.Length <= 10 || n.Split(' ').Last().Length <= 4;
        }

        private static double LevenshteinRatio(string s, string t)
        {
            int d = DamerauLevenshtein(s, t);
            return 1.0 - (d / (double)Math.Max(1, Math.Max(s.Length, t.Length)));
        }

        // Damerau‑Levenshtein (adjacent transpositions)
        private static int DamerauLevenshtein(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
            if (string.IsNullOrEmpty(t)) return s.Length;

            int n = s.Length, m = t.Length;
            var dp = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) dp[i, 0] = i;
            for (int j = 0; j <= m; j++) dp[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost);

                    if (i > 1 && j > 1 && s[i - 1] == t[j - 2] && s[i - 2] == t[j - 1])
                    {
                        dp[i, j] = Math.Min(dp[i, j], dp[i - 2, j - 2] + 1);
                    }
                }
            }
            return dp[n, m];
        }

        // Jaro‑Winkler similarity (0..1)
        private static double JaroWinkler(string s1, string s2)
        {
            if (s1 == s2) return 1.0;
            if (s1.Length == 0 || s2.Length == 0) return 0.0;

            int matchDistance = Math.Max(s1.Length, s2.Length) / 2 - 1;
            bool[] s1Matches = new bool[s1.Length];
            bool[] s2Matches = new bool[s2.Length];

            int matches = 0;
            for (int i = 0; i < s1.Length; i++)
            {
                int start = Math.Max(0, i - matchDistance);
                int end = Math.Min(i + matchDistance + 1, s2.Length);
                for (int j = start; j < end; j++)
                {
                    if (s2Matches[j]) continue;
                    if (s1[i] != s2[j]) continue;
                    s1Matches[i] = s2Matches[j] = true;
                    matches++;
                    break;
                }
            }
            if (matches == 0) return 0.0;

            double t = 0; // transpositions
            int k = 0;
            for (int i = 0; i < s1.Length; i++)
            {
                if (!s1Matches[i]) continue;
                while (!s2Matches[k]) k++;
                if (s1[i] != s2[k]) t++;
                k++;
            }
            t /= 2.0;

            double jaro = ((matches / (double)s1.Length) + (matches / (double)s2.Length) + ((matches - t) / matches)) / 3.0;

            int prefix = 0;
            int maxPrefix = Math.Min(4, Math.Min(s1.Length, s2.Length));
            while (prefix < maxPrefix && s1[prefix] == s2[prefix]) prefix++;

            return jaro + 0.1 * prefix * (1 - jaro);
        }
    }
}
