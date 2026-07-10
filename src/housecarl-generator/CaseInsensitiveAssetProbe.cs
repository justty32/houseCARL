using HousecarlCore;

namespace HousecarlGenerator;

/// <summary>
/// Case-insensitivity guard for the loose asset layer and INI discovery.
///
/// The engine, Wine and MO2's VFS all resolve Data-relative paths case-INSENSITIVELY, and a mod folder carries
/// whatever casing its author's archive happened to have. houseCARL's canonical asset path is therefore only ever
/// compared OrdinalIgnoreCase — but three code paths reached the filesystem with a caller's or a hardcoded casing,
/// which a case-SENSITIVE filesystem (any Linux install) then answers wrongly. Each returned a silent ABSENT, the
/// one failure mode the resolver's contract forbids:
///
///   1  subtree-cache poisoning — LooseCache keys OrdinalIgnoreCase, but WarmSubtree built the probe path from
///      whichever casing arrived FIRST. One lowercase `scripts\` query hid every mod's real `Scripts\` for the
///      snapshot's whole life (downstream: validate_dialogue reporting a compiled script as WILL NOT FIRE).
///   2  a hardcoded casing — CheckSeq asks for `SEQ\<plugin>.seq`; the CK writes `Seq\`, so an existing .seq read
///      as missing and the validator told the modder to regenerate a file that was already there.
///   3  ArchiveDiscovery probed `Skyrim.ini`; MO2 under Wine writes `skyrim.ini`, so every base-game BSA silently
///      dropped out of the asset scan and vanilla assets resolved ABSENT.
///
/// Every arm is self-contained (synthetic temp folders, no game data, no external tool) and runs on both platforms.
/// On a case-INSENSITIVE filesystem the arms pass trivially — that is the point: they pin the behaviour Windows has
/// always had, so it cannot regress on the platform where it is not free. Verified FAIL-before / PASS-after on a
/// case-sensitive filesystem (Manjaro, ext4).
///
/// Run: dotnet run --project src/housecarl-generator case-insensitive-asset-guard
/// </summary>
internal static class CaseInsensitiveAssetProbe
{
    public static int RunGuard(string[] args)
    {
        Console.WriteLine("================================================================");
        Console.WriteLine(" case-insensitive-asset guard — loose paths + INI discovery");
        Console.WriteLine("================================================================");
        Console.WriteLine();
        int fail = 0;
        void Check(bool c, string label) { Console.WriteLine((c ? "  PASS  " : "  FAIL  ") + label); if (!c) fail++; }

        var root = Path.Combine(Path.GetTempPath(), "hc-case-guard-" + Guid.NewGuid().ToString("N"));
        try
        {
            var overwrite = Path.Combine(root, "overwrite");
            var mods = Path.Combine(root, "mods");
            var data = Path.Combine(root, "Data");
            var high = Path.Combine(mods, "HighMod");
            var low = Path.Combine(mods, "LowMod");
            var profile = Path.Combine(root, "profiles", "Default");
            foreach (var d in new[] { overwrite, mods, data, high, low, profile }) Directory.CreateDirectory(d);
            var enabled = new[] { "HighMod", "LowMod" };   // highest priority FIRST

            // Write a Data-relative (backslash) path as a REAL nested file, preserving each segment's casing.
            // Path.Combine cannot be handed a backslash path here: on Unix a backslash is a filename character,
            // so the probe's own fixture would collapse into one absurdly-named file.
            void Touch(string baseDir, string relBackslash)
            {
                var p = Path.Combine(baseDir, relBackslash.Replace('\\', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                File.WriteAllText(p, "x");
            }

            AssetResolver Build() => AssetResolver.Build(overwrite, mods, data, enabled, Array.Empty<ActiveArchive>());

            Touch(high, @"Scripts\MyFragment.pex");        // the casing an author's archive actually shipped
            Touch(low, @"Scripts\MyFragment.pex");         // a lower-priority provider of the same path
            Touch(high, @"Seq\MyPlugin.seq");              // the CK's casing; CheckSeq asks for SEQ\
            Touch(data, @"scripts\Vanilla.pex");           // a base-game path whose real casing is lowercase

            // ---- 1: the FIRST query's casing must not decide what the subtree contains (cache poisoning) ----
            Console.WriteLine("--- 1: subtree cache is not poisoned by the first query's casing ---");
            using (var r = Build())
            {
                var seed = r.Resolve(@"scripts\MyFragment.pex");   // lowercase 'scripts' — warms the OrdinalIgnoreCase key
                Check(seed.Exists && seed.Winner is { Source: "HighMod" },
                      $"a lowercase subtree query resolves — winner={seed.Winner?.Source ?? "(none)"}");

                var real = r.Resolve(@"Scripts\MyFragment.pex");   // the on-disk casing, into the SAME warmed key
                Check(real.Exists && real.Winner is { Source: "HighMod" },
                      $"…and the on-disk casing still resolves afterwards — winner={real.Winner?.Source ?? "(none)"}");

                Check(real.Providers.Count == 2 && real.Providers[0].Source == "HighMod" && real.Providers[1].Source == "LowMod",
                      $"…with precedence intact across both providers — [{string.Join(", ", real.Providers.Select(p => p.Source))}]");
            }

            // ---- 2: a hardcoded casing (SEQ\) finds the directory the CK actually wrote (Seq\) ----
            Console.WriteLine();
            Console.WriteLine("--- 2: a hardcoded directory casing resolves onto the real directory ---");
            using (var r = Build())
            {
                var seq = r.Resolve(@"SEQ\MyPlugin.seq");
                Check(seq.Exists && seq.Winner is { Source: "HighMod" },
                      $"'SEQ\\MyPlugin.seq' finds the mod's 'Seq\\' — winner={seq.Winner?.Source ?? "(none)"}");
            }
            using (var r = Build())
            {
                var vanilla = r.Resolve(@"Scripts\Vanilla.pex");   // uppercase query, lowercase dir on disk
                Check(vanilla.Exists && vanilla.Winner is { Source: "Data" },
                      $"an uppercase query finds a lowercase on-disk directory — winner={vanilla.Winner?.Source ?? "(none)"}");
            }

            // ---- 3: a case-insensitive HIT must yield a path that OPENS (real on-disk casing, not the query's) ----
            Console.WriteLine();
            Console.WriteLine("--- 3: the resolved loose path opens on a case-sensitive filesystem ---");
            using (var r = Build())
            {
                var src = r.ResolveForPlacement(@"scripts\myfragment.pex").Sources.FirstOrDefault();
                Check(src?.LooseFilePath is not null, "an all-lowercase query resolves to a loose source");
                Check(src?.LooseFilePath is not null && File.Exists(src.LooseFilePath),
                      $"…and its LooseFilePath exists on disk — {src?.LooseFilePath ?? "(none)"}");
            }

            // ---- 4: the fix must not invent hits — a truly absent asset stays absent, loud and clean ----
            Console.WriteLine();
            Console.WriteLine("--- 4: absence is preserved (the fix widens matching, not existence) ---");
            using (var r = Build())
            {
                var missFile = r.Resolve(@"Scripts\NoSuchScript.pex");    // real dir, absent file
                Check(!missFile.Exists && missFile.Winner is null, "an absent file in a present directory → ABSENT");
                var missDir = r.Resolve(@"NoSuchDir\Whatever.pex");       // absent dir entirely
                Check(!missDir.Exists && missDir.Winner is null, "an absent directory → ABSENT");
            }

            // ---- 5: ArchiveDiscovery reads the profile INI whatever its casing ----
            Console.WriteLine();
            Console.WriteLine("--- 5: the profile's game INI is found case-insensitively ---");
            foreach (var f in new[] { "modlist.txt", "plugins.txt", "loadorder.txt" }) File.WriteAllText(Path.Combine(profile, f), "");
            File.WriteAllText(Path.Combine(data, "Skyrim - Misc.bsa"), "");      // path-mapped only; never read here

            File.WriteAllText(Path.Combine(profile, "skyrim.ini"),                // MO2-under-Wine's casing
                "[Archive]\nsResourceArchiveList=Skyrim - Misc.bsa\n");
            var found = ArchiveDiscovery.Discover(profile, mods, data, overwrite, root);
            Check(!found.Warnings.Any(w => w.Contains("sResourceArchiveList")),
                  $"a lowercase 'skyrim.ini' is read — {found.Warnings.Count} discovery warning(s)");
            Check(found.Archives.Any(a => a.OwningPlugin == "Skyrim.ini [Archive]"),
                  $"…and its base archive is bound — {found.Archives.Count} archive(s)");

            // negative: with NO ini at all the loud warning must survive (fail-loud, never a silent empty scan)
            File.Delete(Path.Combine(profile, "skyrim.ini"));
            var none = ArchiveDiscovery.Discover(profile, mods, data, overwrite, root);
            Check(none.Warnings.Any(w => w.Contains("sResourceArchiveList")),
                  "with no INI at all the base-archive warning is still raised LOUD");

            Console.WriteLine();
            Console.WriteLine(fail == 0 ? "case-insensitive-asset guard: PASS" : $"case-insensitive-asset guard: {fail} FAILED");
            return fail == 0 ? 0 : 1;
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* best-effort temp cleanup, as the sibling guards do */ }
        }
    }
}
