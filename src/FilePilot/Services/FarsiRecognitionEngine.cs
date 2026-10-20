using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FilePilot.Services;

public class FarsiCharacterStats
{
    public bool HasFarsi { get; set; }
    public int FarsiCharCount { get; set; }
    public int TotalCharCount { get; set; }
    public int PersianSpecificCharCount { get; set; }
    public double FarsiPercentage { get; set; }
    public List<char> DetectedPersianLetters { get; set; } = new();
    public string NormalizedText { get; set; } = string.Empty;
    public bool IsRightToLeft { get; set; }
}

public static class FarsiRecognitionEngine
{
    // Persian specific letters
    public static readonly HashSet<char> PersianSpecificLetters = new()
    {
        '\u06AF', // گ (Gaf)
        '\u0686', // چ (Che)
        '\u067E', // پ (Pe)
        '\u0698', // ژ (Zhe)
        '\u06A9', // ک (Persian Ke)
        '\u06CC', // ی (Persian Ye)
        '\u06C0', // ۀ (Heh with Yeh above)
        '\u06BE', // ھ (Heh Do-cheshme)
    };

    // Standard Persian alphabet in collation order
    public static readonly List<char> PersianAlphabetOrder = new()
    {
        'آ', 'ا', 'ب', 'پ', 'ت', 'ث', 'ج', 'چ', 'ح', 'خ',
        'د', 'ذ', 'ر', 'ز', 'ژ', 'س', 'ش', 'ص', 'ض', 'ط',
        'ظ', 'ع', 'غ', 'ف', 'ق', 'ک', 'گ', 'ل', 'م', 'ن',
        'و', 'ه', 'ی'
    };

    private static readonly Dictionary<char, int> AlphabetRank = PersianAlphabetOrder
        .Select((c, idx) => (c, idx))
        .ToDictionary(x => x.c, x => x.idx);

    public static bool IsFarsiCharacter(char c)
    {
        // Check Persian-specific characters
        if (PersianSpecificLetters.Contains(c)) return true;

        // Persian digits (۰ - ۹)
        if (c >= '\u06F0' && c <= '\u06F9') return true;

        // Zero-width non-joiner (نیم‌فاصله)
        if (c == '\u200C') return true;

        // Standard Arabic/Persian letter range (excluding pure non-Persian Arabic signs)
        if (c >= '\u0600' && c <= '\u06FF') return true;

        // Arabic presentation forms (often used in shaped Persian glyphs)
        if ((c >= '\uFB50' && c <= '\uFDFF') || (c >= '\uFE70' && c <= '\uFEFF')) return true;

        return false;
    }

    public static bool ContainsFarsi(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (char c in text)
        {
            if (IsFarsiCharacter(c))
                return true;
        }
        return false;
    }

    public static FarsiCharacterStats Analyze(string? text)
    {
        var stats = new FarsiCharacterStats();
        if (string.IsNullOrEmpty(text))
            return stats;

        stats.TotalCharCount = text.Length;
        var distinctChars = new HashSet<char>();

        foreach (char c in text)
        {
            if (IsFarsiCharacter(c))
            {
                stats.FarsiCharCount++;
                if (PersianSpecificLetters.Contains(c))
                {
                    stats.PersianSpecificCharCount++;
                    distinctChars.Add(c);
                }
                else if (c >= '\u0621' && c <= '\u064A')
                {
                    distinctChars.Add(c);
                }
            }
        }

        stats.HasFarsi = stats.FarsiCharCount > 0;
        stats.FarsiPercentage = stats.TotalCharCount > 0 ? (double)stats.FarsiCharCount / stats.TotalCharCount * 100.0 : 0;
        stats.DetectedPersianLetters = distinctChars.OrderBy(c => c).ToList();
        stats.NormalizedText = Normalize(text);
        stats.IsRightToLeft = stats.FarsiCharCount > (stats.TotalCharCount / 3);

        return stats;
    }

    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            switch (c)
            {
                // Arabic Yeh & Alef Maksura -> Persian Ye
                case '\u064A':
                case '\u0649':
                    sb.Append('\u06CC');
                    break;

                // Arabic Kaf -> Persian Ke
                case '\u0643':
                    sb.Append('\u06A9');
                    break;

                // Arabic Teh Marbuta -> Heh or Teh
                case '\u0629':
                    sb.Append('\u0647');
                    break;

                // Persian digits -> English ASCII digits for uniform numeric matching
                case '\u06F0': sb.Append('0'); break;
                case '\u06F1': sb.Append('1'); break;
                case '\u06F2': sb.Append('2'); break;
                case '\u06F3': sb.Append('3'); break;
                case '\u06F4': sb.Append('4'); break;
                case '\u06F5': sb.Append('5'); break;
                case '\u06F6': sb.Append('6'); break;
                case '\u06F7': sb.Append('7'); break;
                case '\u06F8': sb.Append('8'); break;
                case '\u06F9': sb.Append('9'); break;

                // Arabic digits -> English ASCII digits
                case '\u0660': sb.Append('0'); break;
                case '\u0661': sb.Append('1'); break;
                case '\u0662': sb.Append('2'); break;
                case '\u0663': sb.Append('3'); break;
                case '\u0664': sb.Append('4'); break;
                case '\u0665': sb.Append('5'); break;
                case '\u0666': sb.Append('6'); break;
                case '\u0667': sb.Append('7'); break;
                case '\u0668': sb.Append('8'); break;
                case '\u0669': sb.Append('9'); break;

                // Strip Tatweel / Kashida
                case '\u0640':
                    break;

                // Strip Harakat / Diacritics
                case '\u064B': // Tanwin Fath
                case '\u064C': // Tanwin Damm
                case '\u064D': // Tanwin Kasr
                case '\u064E': // Fatha
                case '\u064F': // Damma
                case '\u0650': // Kasra
                case '\u0651': // Tashdid
                case '\u0652': // Sukun
                case '\u0653': // Maddah
                case '\u0654': // Hamza Above
                    break;

                // Zero-Width Non-Joiner normalized to space or kept
                case '\u200C':
                    sb.Append(' ');
                    break;

                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString().Trim();
    }

    public static bool MatchesFarsi(string query, string target)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        if (string.IsNullOrWhiteSpace(target)) return false;

        string normQuery = Normalize(query).ToLowerInvariant();
        string normTarget = Normalize(target).ToLowerInvariant();

        return normTarget.Contains(normQuery, StringComparison.OrdinalIgnoreCase);
    }

    public static int CompareFarsi(string? s1, string? s2)
    {
        if (s1 == null && s2 == null) return 0;
        if (s1 == null) return -1;
        if (s2 == null) return 1;

        string n1 = Normalize(s1);
        string n2 = Normalize(s2);

        int minLen = Math.Min(n1.Length, n2.Length);
        for (int i = 0; i < minLen; i++)
        {
            char c1 = n1[i];
            char c2 = n2[i];

            if (c1 == c2) continue;

            bool hasRank1 = AlphabetRank.TryGetValue(c1, out int r1);
            bool hasRank2 = AlphabetRank.TryGetValue(c2, out int r2);

            if (hasRank1 && hasRank2)
            {
                return r1.CompareTo(r2);
            }
            if (hasRank1) return -1; // Persian letters sorted cleanly
            if (hasRank2) return 1;

            return c1.CompareTo(c2);
        }

        return n1.Length.CompareTo(n2.Length);
    }
}
