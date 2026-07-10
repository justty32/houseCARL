using HousecarlCore;

namespace HousecarlGenerator;

/// <summary>
/// Dialogue text-encoding lint guard.
///
/// The engine reads a non-localized plugin's inline strings as Windows-1252. The lint used to flag EVERY char above
/// ASCII as "usually renders as in-game mojibake" — a claim its own premise refutes for the chars that codepage
/// holds natively. An em-dash is one byte (0x97) and an e-acute is one byte (0xE9): the plugin's bytes are correct
/// and no encoding step can corrupt them. Crying mojibake over them buries the finding that IS a defect — a char
/// cp1252 has no byte for (CJK, an emoji, a C1 control), which the write path turns into '?'.
///
/// So the classifier splits, and this guard pins the split:
///   • representable but non-ASCII → reported as a FONT-GLYPH question, explicitly "will NOT mojibake".
///   • not representable         → reported as destroyed ('?'), the provable defect.
///   • both present              → BOTH reported, separately (nothing is silently folded away).
///   • pure ASCII                → silent.
///
/// Drives the classifier directly (no load order, no game data, no plugin) — CI-safe on a fresh checkout.
///
/// Run: dotnet run --project src/housecarl-generator dialogue-encoding-guard
/// </summary>
internal static class DialogueEncodingProbe
{
    public static int RunGuard(string[] args)
    {
        Console.WriteLine("================================================================");
        Console.WriteLine(" dialogue-encoding guard — Windows-1252 representability, not 'non-ASCII'");
        Console.WriteLine("================================================================");
        Console.WriteLine();
        int fail = 0;
        void Check(bool c, string label) { Console.WriteLine((c ? "  PASS  " : "  FAIL  ") + label); if (!c) fail++; }

        static List<DialogueIssue> Lint(string s)
        {
            var issues = new List<DialogueIssue>();
            DialogueValidate.CheckEncoding(s, "T", issues);
            return issues;
        }
        static bool Destroyed(List<DialogueIssue> i) => i.Any(x => x.Message.Contains("cannot represent"));
        static bool GlyphOnly(List<DialogueIssue> i) => i.Any(x => x.Message.Contains("will NOT mojibake"));

        // ---- representable: the bytes are right; only the font is in question ----
        Console.WriteLine("--- valid Windows-1252, non-ASCII → a glyph question, never 'mojibake' ---");
        foreach (var (text, what) in new[] { ("a — b", "em-dash U+2014 (0x97)"), ("café", "e-acute U+00E9 (0xE9)"),
                                             ("wait…", "ellipsis U+2026 (0x85)"), ("it’s", "smart quote U+2019 (0x92)"),
                                             ("€5", "euro U+20AC (0x80)") })
        {
            var i = Lint(text);
            Check(GlyphOnly(i) && !Destroyed(i), $"{what} → glyph note, not a corruption claim");
        }

        // ---- not representable: the write path has no byte, so it really is destroyed ----
        Console.WriteLine();
        Console.WriteLine("--- outside Windows-1252 → destroyed ('?'), the provable defect ---");
        var cjk = Lint("天際");
        Check(Destroyed(cjk) && !GlyphOnly(cjk), "CJK → destroyed, and NOT filed as a glyph question");
        var c1 = Lint("x\u0081y");     // 0x81 is undefined in cp1252 — the misdecoded-UTF-8 signature
        Check(Destroyed(c1), "an undefined cp1252 slot (U+0081) → destroyed");

        // ---- both classes present: reported separately, nothing folded away ----
        Console.WriteLine();
        Console.WriteLine("--- mixed text reports both classes, separately ---");
        var mixed = Lint("café 天");
        Check(Destroyed(mixed) && GlyphOnly(mixed) && mixed.Count == 2, $"both classes reported — {mixed.Count} issue(s)");

        // ---- silence where there is nothing to say ----
        Console.WriteLine();
        Console.WriteLine("--- ASCII and empty input stay silent ---");
        Check(Lint("plain ascii text").Count == 0, "pure ASCII → no issue");
        Check(Lint("").Count == 0, "empty string → no issue");

        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "dialogue-encoding guard: PASS" : $"dialogue-encoding guard: {fail} FAILED");
        return fail == 0 ? 0 : 1;
    }
}
