using System.Text;
using HousecarlCore;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;

namespace HousecarlGenerator;

/// <summary>Synthetic inline-name display, predicate, and raw-token replay regressions. No game data.</summary>
public static class RecordNameDisplayProbe
{
    public static int RunGuard(string[] args)
    {
        int fails = 0;
        void Check(string label, Func<bool> test)
        {
            try { var ok = test(); Console.WriteLine($"{(ok ? "PASS" : "FAIL")}: {label}"); if (!ok) fails++; }
            catch (Exception ex) { Console.WriteLine($"FAIL: {label}: {ex}"); fails++; }
        }
        var cp1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;
        var raw = cp1252.GetString(Encoding.UTF8.GetBytes("小麥"));
        Check("strict UTF-8 interpretation restores Chinese", () => RecordNameDisplay.Utf8Interpretation(raw) == "小麥");
        foreach (var unchanged in new[] { "", "Wheat", "小麥", "café", "a — b", "\u00c3(", "\u00c0\u00af", "\ud800" })
            Check($"no alternate interpretation for {System.Text.Json.JsonSerializer.Serialize(unchanged)}",
                () => RecordNameDisplay.Utf8Interpretation(unchanged) is null);
        Check("null has no interpretation", () => RecordNameDisplay.Utf8Interpretation(null) is null);
        Check("ambiguous valid bytes explicitly use UTF-8 preference", () => RecordNameDisplay.Utf8Interpretation("Ã©") == "é");

        var root = Path.Combine(Path.GetTempPath(), "hc-name-display-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var mod = new SkyrimMod(ModKey.FromNameAndExtension("Inline.esp"), SkyrimRelease.SkyrimSE);
        var wheat = mod.MiscItems.AddNew(); wheat.EditorID = "Wheat"; wheat.Name = "小麥";
        var source = Path.Combine(root, "Inline.esp");
        mod.BeginWrite.ToPath(source).WithLoadOrder(Array.Empty<ISkyrimModGetter>()).WithUtf8Encoding().Write();
        using var overlay = SkyrimMod.CreateFromBinaryOverlay(source, SkyrimRelease.SkyrimSE);
        var record = overlay.MiscItems[wheat.FormKey];
        foreach (var depth in new[] { 1, 2 })
            Check($"depth {depth}: raw Token and pure Unicode Display coexist", () =>
            {
                var field = ReadEngine.ReadFields(record, new[] { "Name" }, depth).Fields.Single();
                return field.HasValue && field.Token == raw && field.Display == "小麥";
            });
        bool Matches(IMajorRecordGetter body, string predicate)
        {
            var parsed = FieldPredicateSet.Parse(new[] { predicate });
            if (parsed.Error is not null) throw new InvalidOperationException(parsed.Error);
            var result = parsed.Set!.Matches(body);
            if (parsed.Set.FatalError is not null) throw new InvalidOperationException(parsed.Set.FatalError);
            return result;
        }
        Check("Name contains Chinese uses semantic text", () => Matches(record, "Name contains 小麥"));
        Check("Name equality uses semantic text", () => Matches(record, "Name = 小麥"));
        Check("Name inequality complements semantic equality", () => !Matches(record, "Name != 小麥"));
        Check("unrelated Name substring does not match", () => !Matches(record, "Name contains 小花"));

        var info = new DialogResponses(mod.GetNextFormKey(), SkyrimRelease.SkyrimSE);
        info.Responses.Add(new DialogResponse { Text = "Not the target" });
        info.Responses.Add(new DialogResponse { Text = raw });
        Check("wildcard translated text uses semantic interpretation", () =>
            Matches(info, "Responses[*].Text contains 小麥") && !Matches(info, "Responses[*].Text contains 小花"));
        Check("nested translated text retains raw Token and semantic Display", () =>
        {
            var field = ReadEngine.ReadFields(info, new[] { "Responses[1].Text" }).Fields.Single();
            return field.Token == raw && field.Display == "小麥";
        });

        var legacy = new SkyrimMod(ModKey.FromNameAndExtension("Legacy.esp"), SkyrimRelease.SkyrimSE);
        var cafe = legacy.MiscItems.AddNew(); cafe.EditorID = "Cafe"; cafe.Name = "café —";
        var legacyPath = Path.Combine(root, "Legacy.esp");
        legacy.BeginWrite.ToPath(legacyPath).WithLoadOrder(Array.Empty<ISkyrimModGetter>()).Write();
        using var legacyOverlay = SkyrimMod.CreateFromBinaryOverlay(legacyPath, SkyrimRelease.SkyrimSE);
        var legacyRecord = legacyOverlay.MiscItems[cafe.FormKey];
        Check("serialized Windows-1252 names preserve bytes, text, and predicate semantics", () =>
        {
            var field = ReadEngine.ReadFields(legacyRecord, new[] { "Name" }).Fields.Single();
            return field.Token == "café —" && field.Display is null && Matches(legacyRecord, "Name contains CAFÉ")
                && FullPayload(legacyPath).SequenceEqual(cp1252.GetBytes("café —\0"));
        });

        var plain = mod.MiscItems.AddNew(); plain.EditorID = raw; plain.Name = "café";
        Check("cp1252 and ASCII predicates remain intact", () => Matches(plain, "Name contains CAFÉ") && Matches(record, "EditorID = Wheat"));
        Check("EditorID is not interpreted as translated text", () =>
        {
            var field = ReadEngine.ReadFields(plain, new[] { "EditorID" }).Fields.Single();
            return field.Token == raw && field.Display is null && !Matches(plain, "EditorID contains 小麥")
                && Matches(plain, "EditorID = " + raw);
        });
        plain.Name = "小麥";
        Check("already-Unicode Name preserves token without annotation", () =>
        {
            var field = ReadEngine.ReadFields(plain, new[] { "Name" }).Fields.Single();
            return field.Token == "小麥" && field.Display is null && Matches(plain, "Name contains 小麥");
        });

        var mutable = SkyrimMod.CreateFromBinary(source, SkyrimRelease.SkyrimSE);
        var target = mutable.MiscItems[wheat.FormKey];
        var token = ReadEngine.ReadFields(record, new[] { "Name" }).Fields.Single().Token!;
        WriteEngine.ApplyVerb(target, new WriteRequest { RecordType = "MiscItem", Path = new[] { "Name" }, Verb = "Set", Value = token });
        var replayDir = Path.Combine(root, "replay"); Directory.CreateDirectory(replayDir);
        var replay = Path.Combine(replayDir, "Inline.esp");
        mutable.BeginWrite.ToPath(replay).WithLoadOrder(Array.Empty<ISkyrimModGetter>()).Write();
        Check("raw Token replay through Coerce and default writer is byte-identical", () =>
            File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(replay)));

        var patch = new SkyrimMod(ModKey.FromNameAndExtension("NumericPatch.esp"), SkyrimRelease.SkyrimSE);
        var copied = WriteEngine.GenericGetOrAddAsOverride(patch, record);
        WriteEngine.ApplyVerb(copied, new WriteRequest { RecordType = "MiscItem", Path = new[] { "Weight" }, Verb = "Set", Value = "2.5" });
        var patchPath = Path.Combine(root, "NumericPatch.esp");
        patch.BeginWrite.ToPath(patchPath).WithLoadOrder(new ISkyrimModGetter[] { overlay }).Write();
        using var patchOverlay = SkyrimMod.CreateFromBinaryOverlay(patchPath, SkyrimRelease.SkyrimSE);
        Check("numeric-only edit forwards copied Name payload unchanged", () =>
        {
            var copiedRecord = patchOverlay.MiscItems[wheat.FormKey];
            var field = ReadEngine.ReadFields(copiedRecord, new[] { "Name" }).Fields.Single();
            return copiedRecord.Weight == 2.5f && FullPayload(source).SequenceEqual(FullPayload(patchPath))
                && field.Token == raw && field.Display == "小麥";
        });
        Console.WriteLine($"record-name-display guard: {fails} failures; fixtures: {root}");
        return fails == 0 ? 0 : 1;
    }

    // These tiny fixtures contain exactly one uncompressed MISC record and one FULL subrecord.
    // Inspect its physical payload independently of Mutagen's decoded Name.
    static byte[] FullPayload(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var offset = bytes.AsSpan().IndexOf("FULL"u8);
        if (offset < 0 || offset + 6 > bytes.Length) throw new InvalidDataException("Fixture FULL subrecord missing.");
        var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 4, 2));
        return bytes.AsSpan(offset + 6, length).ToArray();
    }
}
