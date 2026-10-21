using HousecarlMcp;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;

namespace HousecarlGenerator;

/// <summary>Real serialized fixtures, no game installation or platform discovery.</summary>
public static class RecordNamesProbe
{
    public static int RunGuard(string[] args)
    {
        var root = Path.Combine(Path.GetTempPath(), "hc-record-names-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var prior = Environment.GetEnvironmentVariable("LocalAppData");
        if (!OperatingSystem.IsWindows()) Environment.SetEnvironmentVariable("LocalAppData", null);
        int fails = 0;
        void Check(string label, Func<bool> test)
        {
            try { var ok = test(); Console.WriteLine($"{(ok ? "PASS" : "FAIL")}: {label}"); if (!ok) fails++; }
            catch (Exception e) { Console.WriteLine($"FAIL: {label}: {e}"); fails++; }
        }
        try
        {
            var data = Path.Combine(root, "Data"); Directory.CreateDirectory(data);
            var master = new SkyrimMod(ModKey.FromNameAndExtension("Skyrim.esm"), SkyrimRelease.SkyrimSE);
            master.UsingLocalization = true;
            var gold = master.MiscItems.AddNew(); gold.EditorID = "Gold001"; gold.Name = "Gold";
            var masterPath = Path.Combine(data, "Skyrim.esm");
            master.BeginWrite.ToPath(masterPath).WithLoadOrder(Array.Empty<ISkyrimModGetter>()).Write();
            var strings = Directory.GetFiles(Path.Combine(data, "Strings"), "*.STRINGS").Single();
            WriteBsa(Path.Combine(data, "Skyrim - Interface.bsa"), Path.GetFileName(strings), File.ReadAllBytes(strings));
            Directory.Move(Path.Combine(data, "Strings"), Path.Combine(root, "saved-strings"));
            if (!OperatingSystem.IsWindows()) Check("localized Name resolves without LocalAppData", () =>
            {
                using var resolver = LoadOrderResolver.Build(new[] { masterPath });
                var svc = LoadOrderService.ForGuard(resolver, new UserConfigStore(Path.Combine(root, "guard.user.json")));
                return svc.ResolveRefs(new[] { gold.FormKey.ToString() })[0].Name == "Gold";
            });
            if (!OperatingSystem.IsWindows())
            {
                if (OperatingSystem.IsLinux()) Check("overlay owns and closes its plugin file handle", () =>
                {
                    bool Held() => Directory.EnumerateFiles("/proc/self/fd").Any(p =>
                    {
                        try { return new FileInfo(p).LinkTarget == masterPath; } catch (IOException) { return false; }
                    });
                    var ov = (IDisposable)LoadOrderResolver.OpenOverlay(masterPath, data);
                    bool opened = Held();
                    ov.Dispose();
                    return opened && !Held();
                });
                Check("cleaned master falls back to explicit Data archive", () =>
                {
                    var cleaned = Path.Combine(root, "cleaned"); Directory.CreateDirectory(cleaned);
                    var copy = Path.Combine(cleaned, "Skyrim.esm"); File.Copy(masterPath, copy);
                    using var ov = (IDisposable)LoadOrderResolver.OpenOverlay(copy, data);
                    return ((ISkyrimModGetter)ov).MiscItems[gold.FormKey].Name?.String == "Gold";
                });
                Check("case-insensitive own loose table wins over fallback Data", () =>
                {
                    var own = Path.Combine(root, "own"); Directory.CreateDirectory(own);
                    var copy = Path.Combine(own, "Skyrim.esm"); File.Copy(masterPath, copy);
                    var ownStrings = Path.Combine(own, "strings"); Directory.CreateDirectory(ownStrings);
                    var bytes = File.ReadAllBytes(Path.Combine(root, "saved-strings", Path.GetFileName(strings)));
                    var goldBytes = System.Text.Encoding.ASCII.GetBytes("Gold");
                    for (int i = 0; i <= bytes.Length - goldBytes.Length; i++)
                        if (bytes.AsSpan(i, goldBytes.Length).SequenceEqual(goldBytes)) System.Text.Encoding.ASCII.GetBytes("Own!").CopyTo(bytes, i);
                    File.WriteAllBytes(Path.Combine(ownStrings, Path.GetFileName(strings).ToLowerInvariant()), bytes);
                    using var ov = (IDisposable)LoadOrderResolver.OpenOverlay(copy, data);
                    return ((ISkyrimModGetter)ov).MiscItems[gold.FormKey].Name?.String == "Own!";
                });
                Check("ambiguous archived tables fail instead of choosing an arbitrary winner", () =>
                {
                    var duplicate = Path.Combine(data, "Second.bsa"); File.Copy(Path.Combine(data, "Skyrim - Interface.bsa"), duplicate);
                    try
                    {
                        using var ov = (IDisposable)LoadOrderResolver.OpenOverlay(masterPath, data);
                        _ = ((ISkyrimModGetter)ov).MiscItems[gold.FormKey].Name?.String;
                        return false;
                    }
                    catch (InvalidDataException e) { return e.Message.Contains("Ambiguous strings"); }
                    finally { File.Move(duplicate, Path.Combine(root, "second.saved")); }
                });
            }
            var inline = new SkyrimMod(ModKey.FromNameAndExtension("Inline.esp"), SkyrimRelease.SkyrimSE);
            var wheat = inline.MiscItems.AddNew(); wheat.EditorID = "Wheat"; wheat.Name = "小麥";
            var container = inline.Containers.AddNew(); container.EditorID = "WheatBox";
            container.Items = new() { new ContainerEntry { Item = new ContainerItem { Item = wheat.FormKey.ToLink<IItemGetter>(), Count = 1 } } };
            var inlinePath = Path.Combine(root, "Inline.esp");
            inline.BeginWrite.ToPath(inlinePath).WithLoadOrder(Array.Empty<ISkyrimModGetter>()).WithUtf8Encoding().Write();
            Check("UTF-8 inline Name resolves as Chinese", () =>
            {
                using var resolver = LoadOrderResolver.Build(new[] { inlinePath });
                var svc = LoadOrderService.ForGuard(resolver, new UserConfigStore(Path.Combine(root, "guard.user.json")));
                return svc.ResolveRefs(new[] { wheat.FormKey.ToString() })[0].Name == "小麥";
            });
            Check("resolve_names carries Unicode identity while preserving FormLink token", () =>
            {
                using var resolver = LoadOrderResolver.Build(new[] { inlinePath });
                var svc = LoadOrderService.ForGuard(resolver, new UserConfigStore(Path.Combine(root, "guard.user.json")));
                var result = svc.ResolveRead(container.FormKey, null, new[] { "Items[0].Item.Item" }, false, resolveNames: true);
                var field = result.Record!.Fields.Single();
                return field.Link?.Name == "小麥" && field.Token == wheat.FormKey.ToString();
            });
        }
        finally { Environment.SetEnvironmentVariable("LocalAppData", prior); }
        Console.WriteLine($"record-names guard: {fails} failures; fixtures: {root}");
        return fails == 0 ? 0 : 1;
    }

    // One uncompressed BSA v104 entry; exercises Mutagen's real archive parser, no external tool.
    static void WriteBsa(string path, string name, byte[] contents)
    {
        var filename = System.Text.Encoding.ASCII.GetBytes(name + "\0");
        var folder = System.Text.Encoding.ASCII.GetBytes("strings\0");
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
