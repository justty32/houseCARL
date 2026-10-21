using System.Security.Cryptography;
using System.Text.Json;
using HousecarlMcp;
using Mutagen.Bethesda.Plugins;

namespace HousecarlGenerator;

/// <summary>Read-only acceptance of the six supplied vanilla/USSEP files, not a live MO2 profile claim.</summary>
public static class RecordNamesLiveProbe
{
    public static int Run(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("usage: record-names-read <game-Data-folder> <USSEP-plugin-path> <expected-wheat-name>");
            return 2;
        }
        var expectedWheat = args[2];
        var paths = new[] { "Skyrim.esm", "Update.esm", "Dawnguard.esm", "HearthFires.esm", "Dragonborn.esm" }
            .Select(p => Path.Combine(args[0], p)).Append(args[1]).ToArray();
        string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
        foreach (var path in paths) Console.WriteLine($"input SHA256 {Hash(path)} {path}");
        foreach (var path in new[] { typeof(LoadOrderResolver).Assembly.Location, typeof(LoadOrderService).Assembly.Location, typeof(RecordNamesLiveProbe).Assembly.Location })
            Console.WriteLine($"assembly SHA256 {Hash(path)} {path}");
        using var resolver = LoadOrderResolver.Build(paths);
        if (resolver.LoadFailures.Count > 0) throw new InvalidOperationException("Input plugin indexing failed.");
        var config = Path.Combine(Path.GetTempPath(), "hc-names-read-" + Guid.NewGuid().ToString("N") + ".user.json");
        var svc = LoadOrderService.ForGuard(resolver, new UserConfigStore(config));
        int failures = 0;
        foreach (var formid in new[] { "00000F:Skyrim.esm", "0110CD:HearthFires.esm" })
        {
          try
          {
            Console.WriteLine(ReadTools.ReadRecord(svc, formid, fields: new[] { "EditorID", "Name" }));
            var wire = ReadTools.ReadRecord(svc, formid, fields: new[] { "Name" }, format: "json");
            Console.WriteLine(wire);
            var resolved = svc.ResolveRefs(new[] { formid }).Single();
            Console.WriteLine(ReadTools.Resolve(svc, new[] { formid }));
            using var json = JsonDocument.Parse(wire);
            var field = json.RootElement.GetProperty("fields")[0];
            var name = field.TryGetProperty("display", out var display) ? display.GetString() : field.GetProperty("value").GetString();
            bool pass = resolved.Resolved && !string.IsNullOrEmpty(name) && name == resolved.Name
                && (formid.StartsWith("00000F") ? resolved.EditorId == "Gold001" && name == "Gold" : name == expectedWheat);
            Console.WriteLine($"{(pass ? "PASS" : "FAIL")}: {formid} read_record/resolve agree: {name}");
            if (!pass) failures++;
          }
          catch (Exception ex) { Console.WriteLine($"FAIL: {formid}: {ex.GetType().Name}: {ex.Message}"); failures++; }
        }
        var wheat = FormKey.Factory("0110CD:HearthFires.esm");
        var original = svc.ResolveRead(wheat, "HearthFires.esm", new[] { "Name" }, false);
        var originalName = original.Record?.Fields.Single();
        bool originalPass = originalName is { HasValue: true, Token: "Wheat" };
        Console.WriteLine($"{(originalPass ? "PASS" : "FAIL")}: original localized HearthFires Name={originalName?.Token}");
        if (!originalPass) failures++;
        try
        {
            var linked = svc.ResolveRead(FormKey.Factory("008246:HearthFires.esm"), null,
                new[] { "Items[31]" }, false, resolveNames: true).Record!.Fields.Single();
            bool linkPass = linked.Token == wheat.ToString() && linked.Link?.Name == expectedWheat;
            Console.WriteLine(ReadTools.ReadRecord(svc, "008246:HearthFires.esm", fields: new[] { "Items[31]" }, resolve_names: true));
            Console.WriteLine($"{(linkPass ? "PASS" : "FAIL")}: real FormList resolve_names exposes wheat identity");
            if (!linkPass) failures++;
            var view = resolver.Capture();
            using var session = resolver.OpenSession();
            var body = view.GetRecord(session, view.ResolveWinner(wheat)!.Value.WinnerPlugin, wheat)!;
            var predicate = FieldPredicateSet.Parse(new[] { "Name contains " + expectedWheat }).Set!;
            bool predicatePass = predicate.Matches(body);
            Console.WriteLine($"{(predicatePass ? "PASS" : "FAIL")}: real winning Name contains {expectedWheat}");
            if (!predicatePass) failures++;
            var otherGlyph = expectedWheat == "小麦" ? "小麥" : "小麦";
            bool negativePass = !FieldPredicateSet.Parse(new[] { "Name = " + otherGlyph }).Set!.Matches(body);
            Console.WriteLine($"{(negativePass ? "PASS" : "FAIL")}: no implicit glyph conversion to {otherGlyph}");
            if (!negativePass) failures++;
        }
        catch (Exception ex) { Console.WriteLine($"FAIL: linked-name/predicate: {ex.GetType().Name}: {ex.Message}"); failures++; }
        Console.WriteLine($"read-only real-file acceptance: {failures} failures; no service or deployment was changed");
        return failures == 0 ? 0 : 1;
    }
}
