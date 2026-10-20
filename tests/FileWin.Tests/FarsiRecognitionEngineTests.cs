using System;
using System.Linq;
using FileWin.Models;
using FileWin.Services;
using Xunit;

namespace FileWin.Tests;

public class FarsiRecognitionEngineTests
{
    [Fact]
    public void DetectsPersianSpecificCharacters()
    {
        // Persian specific: گ چ پ ژ ک ی
        string text = "گندم چشمه پروانه ژاله کتاب چای";
        var stats = FarsiRecognitionEngine.Analyze(text);

        Assert.True(stats.HasFarsi);
        Assert.True(stats.PersianSpecificCharCount >= 6);
        Assert.Contains('گ', stats.DetectedPersianLetters);
        Assert.Contains('چ', stats.DetectedPersianLetters);
        Assert.Contains('پ', stats.DetectedPersianLetters);
        Assert.Contains('ژ', stats.DetectedPersianLetters);
        Assert.Contains('ک', stats.DetectedPersianLetters);
        Assert.Contains('ی', stats.DetectedPersianLetters);
    }

    [Fact]
    public void DistinguishesPureAsciiFromFarsi()
    {
        string asciiText = "Project_Report_2026_Final.docx";
        var stats = FarsiRecognitionEngine.Analyze(asciiText);

        Assert.False(stats.HasFarsi);
        Assert.Equal(0, stats.FarsiCharCount);
        Assert.False(stats.IsRightToLeft);
    }

    [Fact]
    public void UnifiesArabicAndPersianCharactersInSearch()
    {
        // Arabic Kaf (\u0643) vs Persian Ke (\u06A9)
        string targetWithPersianKe = "کتابخانه‌های ملی";
        string queryWithArabicKaf = "كتابخانه";

        bool matches = FarsiRecognitionEngine.MatchesFarsi(queryWithArabicKaf, targetWithPersianKe);
        Assert.True(matches);

        // Arabic Yeh (\u064A) vs Persian Ye (\u06CC)
        string targetWithPersianYe = "فایل‌های صوتی";
        string queryWithArabicYeh = "فايل";

        bool matchesYe = FarsiRecognitionEngine.MatchesFarsi(queryWithArabicYeh, targetWithPersianYe);
        Assert.True(matchesYe);
    }

    [Fact]
    public void NormalizesPersianDigitsForSearch()
    {
        string filename = "سند_مالی_۱۴۰۵.xlsx";
        string query = "1405";

        bool matches = FarsiRecognitionEngine.MatchesFarsi(query, filename);
        Assert.True(matches);
    }

    [Fact]
    public void SortsUsingPersianAlphabetCollation()
    {
        // Alphabet: آ، ا، ب، پ، ت ... ژ ... گ
        string[] items = new[] { "ژاله", "آرش", "پروانه", "بهار", "گندم" };
        Array.Sort(items, FarsiRecognitionEngine.CompareFarsi);

        // Expected: آرش, بهار, پروانه, ژاله, گندم
        Assert.Equal("آرش", items[0]);
        Assert.Equal("بهار", items[1]);
        Assert.Equal("پروانه", items[2]);
        Assert.Equal("ژاله", items[3]);
        Assert.Equal("گندم", items[4]);
    }

    [Fact]
    public void FormatsFileBytesCorrectly()
    {
        Assert.Equal("500 B", FileItem.FormatBytes(500));
        Assert.Equal("1.5 KB", FileItem.FormatBytes(1536));
        Assert.Equal("2.00 GB", FileItem.FormatBytes(2L * 1024 * 1024 * 1024));
    }
}
