using System.Text;
using HousecarlCore;
using HousecarlMcp;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace HousecarlGenerator;

/// <summary>strings-vfs-guard (2026-10-26). A localized master's strings must come from the MO2 VFS WINNER, not from
/// the vanilla archive beside the master. Fixture: game Data carries Skyrim.esm + an English-strings BSA ("Gold"),
/// and an enabled translation mod ships a LOOSE <c>strings/skyrim_english.strings</c> whose bytes are UTF-8 Chinese
/// (the common "Chinese in the English slot" layout, sLanguage=ENGLISH). The game shows the loose text; before this
/// fix houseCARL read "Gold". The Chinese fixture also carries every UTF-8 continuation byte Windows-1252 leaves
/// undefined (81 8D 8F 90 9D) to prove none is dropped between the strings file and the display name.</summary>
public static class StringsVfsProbe
{
    const string GoldZh = "金幣";
    const string GapZh = "丁不乏乐九區";   // UTF-8 bytes include 81, 8D, 8F, 90 and 9D

    public static int RunGuard(string[] args)
    {
        var root = Path.Combine(Path.GetTempPath(), "hc-strings-vfs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var prior = Environment.GetEnvironmentVariable("LocalAppData");
        if (!OperatingSystem.IsWindows()) Environment.SetEnvironmentVariable("LocalAppData", null);
        int fails = 0;
        void Check(string label, Func<bool> test)
        {
            try { var ok = test(); Console.WriteLine($"{(ok ? "PASS" : "FAIL")}: {label}"); if (!ok) fails++; }
            catch (Exception e) { Console.WriteLine($"FAIL: {label}: {e}"); fails++; }
        }
        if (OperatingSystem.IsWindows())
        {
            Console.WriteLine("strings-vfs guard: SKIP on Windows (the VFS strings source is the Unix overlay path).");
            return 0;
        }
        try
        {
            var cp1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;
            string AsLegacy(string s) => cp1252.GetString(Encoding.UTF8.GetBytes(s));

            var data = Path.Combine(root, "Data"); Directory.CreateDirectory(data);
            var mods = Path.Combine(root, "mods");
            var prof = Path.Combine(root, "profile");
            Directory.CreateDirectory(prof);

            // English master in game Data, its strings packed into the base archive (the vanilla layout).
            var master = new SkyrimMod(ModKey.FromNameAndExtension("Skyrim.esm"), SkyrimRelease.SkyrimSE) { UsingLocalization = true };
            var gold = master.MiscItems.AddNew(); gold.EditorID = "Gold001"; gold.Name = "Gold";
            var gap = master.MiscItems.AddNew(); gap.EditorID = "GapProbe"; gap.Name = "Gap";
            var masterPath = Path.Combine(data, "Skyrim.esm");
            master.BeginWrite.ToPath(masterPath).WithLoadOrder(Array.Empty<ISkyrimModGetter>()).Write();
            var enStrings = Directory.GetFiles(Path.Combine(data, "Strings"), "*.STRINGS").Single();
            WriteBsa(Path.Combine(data, "Skyrim - Interface.bsa"), Path.GetFileName(enStrings).ToLowerInvariant(), File.ReadAllBytes(enStrings));
            Directory.Delete(Path.Combine(data, "Strings"), recursive: true);

            // The same master re-written with UTF-8 Chinese bytes in its ENGLISH strings → the translation mod's loose table.
            gold.Name = AsLegacy(GoldZh); gap.Name = AsLegacy(GapZh);
            var zhBuild = Path.Combine(root, "zh-build"); Directory.CreateDirectory(zhBuild);
            master.BeginWrite.ToPath(Path.Combine(zhBuild, "Skyrim.esm")).WithLoadOrder(Array.Empty<ISkyrimModGetter>()).Write();
            var zhStrings = Directory.GetFiles(Path.Combine(zhBuild, "Strings"), "*.STRINGS").Single();
            var zhMod = Path.Combine(mods, "ZH Strings"); Directory.CreateDirectory(Path.Combine(zhMod, "Strings"));
            File.Copy(zhStrings, Path.Combine(zhMod, "Strings", Path.GetFileName(zhStrings).ToLowerInvariant()));
            Check("fixture: the loose table really carries all five cp1252-undefined bytes",
                () => new byte[] { 0x81, 0x8D, 0x8F, 0x90, 0x9D }.All(b => File.ReadAllBytes(zhStrings).Contains(b)));

            void WriteProfile(bool zhEnabled)
            {
                File.WriteAllText(Path.Combine(prof, "loadorder.txt"), "# header\r\nSkyrim.esm\r\n");
                File.WriteAllText(Path.Combine(prof, "plugins.txt"), "# header\r\n");
                File.WriteAllText(Path.Combine(prof, "modlist.txt"), "# header\r\n" + (zhEnabled ? "+" : "-") + "ZH Strings\r\n");
                File.WriteAllText(Path.Combine(prof, "Skyrim.ini"), "[Archive]\r\nsResourceArchiveList=Skyrim - Interface.bsa\r\n");
            }
            LoadOrderService Service(string tag) =>
                LoadOrderService.WithExplicitPaths(data, mods, prof, 0, new UserConfigStore(Path.Combine(root, tag + ".user.json")));

            WriteProfile(zhEnabled: true);
            using (var svc = Service("on"))
            {
                Check("resolve: localized Gold001 reads the loose VFS winner, not the vanilla archive", () =>
                    svc.ResolveRefs(new[] { gold.FormKey.ToString() })[0].Name == GoldZh);
                Check("resolve: no cp1252-undefined byte is dropped (丁不乏乐九區 survives exactly)", () =>
                    svc.ResolveRefs(new[] { gap.FormKey.ToString() })[0].Name == GapZh);
                Check("read_record: Name display is the Chinese text, raw token stays the byte-preserving legacy form", () =>
                {
                    var field = svc.ResolveRead(gap.FormKey, null, new[] { "Name" }, false).Record!.Fields.Single();
                    return field.Display == GapZh && field.Token == AsLegacy(GapZh)
                        && cp1252.GetBytes(field.Token!).SequenceEqual(Encoding.UTF8.GetBytes(GapZh));
                });
            }

            // Translation mod disabled → the VFS falls through to the base archive (the BSA arm of the same lookup).
            WriteProfile(zhEnabled: false);
            using (var svc = Service("off"))
                Check("disabled translation mod → the base archive's English wins again", () =>
                    svc.ResolveRefs(new[] { gold.FormKey.ToString() })[0].Name == "Gold");

            // Folder-only open (no VFS injected) is the unchanged pre-fix behaviour: the archive beside the master.
            Check("OpenOverlay without a VFS keeps the folder lookup (English from the Data archive)", () =>
            {
                using var ov = (IDisposable)LoadOrderResolver.OpenOverlay(masterPath, data);
                return ((ISkyrimModGetter)ov).MiscItems[gold.FormKey].Name?.String == "Gold";
            });
        }
        finally { Environment.SetEnvironmentVariable("LocalAppData", prior); }
        if (fails == 0) { try { Directory.Delete(root, recursive: true); } catch { /* temp scratch */ } }
        Console.WriteLine($"strings-vfs guard: {fails} failures" + (fails == 0 ? "" : $"; fixtures kept: {root}"));
        return fails == 0 ? 0 : 1;
    }

    // One uncompressed BSA v104 entry under strings\ (same layout as RecordNamesProbe.WriteBsa).
    static void WriteBsa(string path, string name, byte[] contents)
    {
        var filename = Encoding.ASCII.GetBytes(name + "\0");
        var folder = Encoding.ASCII.GetBytes("strings\0");
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(new byte[] { 66, 83, 65, 0 }); writer.Write(104u); writer.Write(36u);
        writer.Write(3u); writer.Write(1u); writer.Write(1u);
        writer.Write((uint)folder.Length); writer.Write((uint)filename.Length); writer.Write(0u);
        writer.Write(0ul); writer.Write(1u); writer.Write((uint)(52 + filename.Length));
        writer.Write((byte)folder.Length); writer.Write(folder);
        writer.Write(0ul); writer.Write((uint)contents.Length);
        writer.Write((uint)(52 + 1 + folder.Length + 16 + filename.Length));
        writer.Write(filename); writer.Write(contents);
    }
}
