using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace InventiFind;

public static class ItemMatcher
{
    // ── Main entry point ───────────────────────────────────────────────────
    // Returns a score 0-100 between two items
    public static int CalculateSimilarity(ItemData lost, ItemData found)
    {
        int score = 0;

        // Category must match — hard requirement
        if (!lost.Category.Equals(found.Category, StringComparison.OrdinalIgnoreCase))
            return 0;

        // Name similarity (up to 30 pts)
        score += (int)(NameSimilarity(lost.Name, found.Name) * 30);

        // Description similarity (up to 40 pts)
        score += (int)(DescriptionSimilarity(lost.Description, found.Description) * 40);

        // Location similarity (up to 20 pts)
        score += (int)(LocationSimilarity(lost.Location, found.Location) * 20);

        // Date proximity (up to 10 pts)
        score += DateProximityScore(lost.Date, found.Date);

        return Math.Min(score, 100);
    }

    // ── Name similarity ────────────────────────────────────────────────────
    // Exact → contains → word Jaccard → character bigram fallback
    private static double NameSimilarity(string a, string b)
    {
        a = Normalize(a);
        b = Normalize(b);

        if (a == b) return 1.0;
        if (a.Contains(b) ||
            b.Contains(a)) return 0.8;

        double wordScore = JaccardSimilarity(WordTokens(a), WordTokens(b));
        double bigramScore = JaccardSimilarity(Bigrams(a), Bigrams(b));

        // Weighted average — word overlap matters more for names
        return wordScore * 0.7 + bigramScore * 0.3;
    }

    // ── Description similarity ─────────────────────────────────────────────
    // Word Jaccard + character bigram, averaged
    // Falls back gracefully when one or both descriptions are empty
    private static double DescriptionSimilarity(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return 0.0;

        a = Normalize(a);
        b = Normalize(b);

        double wordScore = JaccardSimilarity(WordTokens(a), WordTokens(b));
        double bigramScore = JaccardSimilarity(Bigrams(a), Bigrams(b));

        // Bigrams catch partial word matches (e.g. "crackd" vs "cracked")
        return wordScore * 0.6 + bigramScore * 0.4;
    }

    // ── Location similarity ────────────────────────────────────────────────
    private static double LocationSimilarity(string a, string b)
    {
        a = Normalize(a);
        b = Normalize(b);

        if (a == b) return 1.0;
        if (a.Contains(b) ||
            b.Contains(a)) return 0.7;

        double wordScore = JaccardSimilarity(WordTokens(a), WordTokens(b));
        double bigramScore = JaccardSimilarity(Bigrams(a), Bigrams(b));

        return wordScore * 0.6 + bigramScore * 0.4;
    }

    // ── Date proximity ─────────────────────────────────────────────────────
    private static int DateProximityScore(DateTime a, DateTime b)
    {
        int days = Math.Abs((a.Date - b.Date).Days);
        return days switch
        {
            0 => 10,
            <= 1 => 8,
            <= 3 => 6,
            <= 7 => 4,
            <= 14 => 2,
            _ => 0
        };
    }

    // ── Jaccard similarity ─────────────────────────────────────────────────
    // |A ∩ B| / |A ∪ B|
    private static double JaccardSimilarity(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 && b.Count == 0) return 0.0;

        var intersection = new HashSet<string>(a);
        intersection.IntersectWith(b);

        var union = new HashSet<string>(a);
        union.UnionWith(b);

        return (double)intersection.Count / union.Count;
    }

    // ── Word tokenizer ─────────────────────────────────────────────────────
    // Splits on whitespace and punctuation, drops very short tokens
    private static HashSet<string> WordTokens(string text)
    {
        return text
            .Split(new[] { ' ', ',', '.', '-', '/', '(', ')', '_', ':', ';' },
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1)
            .ToHashSet();
    }

    // ── Character bigram generator ─────────────────────────────────────────
    // Breaks "wallet" → {"wa","al","ll","le","et"}
    // Good for catching typos and partial matches
    private static HashSet<string> Bigrams(string text)
    {
        var bigrams = new HashSet<string>();
        string clean = text.Replace(" ", "");

        for (int i = 0; i < clean.Length - 1; i++)
            bigrams.Add(clean.Substring(i, 2));

        return bigrams;
    }

    // ── Normalize ─────────────────────────────────────────────────────────
    // Lowercase + trim only — no stopwords, no hardcoded lists
    private static string Normalize(string text)
        => text.Trim().ToLower();
}

// ── Data transfer object ───────────────────────────────────────────────────
public class ItemData
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public DateTime Date { get; set; }
}
