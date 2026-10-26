using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Binary.Streams;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Strings;
using Mutagen.Bethesda.Strings.DI;

namespace HousecarlCore;

/// <summary>
/// Per-plugin strings in an explicitly selected folder. Mutagen 0.53.1's TypicalFactory discovers
/// Windows plugins.txt/INI paths even when a caller supplied a strings folder. An injected lookup
/// avoids that unrelated global state. Loose tables win; competing archive tables fail explicitly
/// because this per-plugin API has no archive load order with which to choose a winner.
/// </summary>
internal sealed class ExplicitStringsLookup : IStringsFolderLookup
{
    // Mutagen 0.53.1 exposes ParsingMeta but its generated stream-overlay factory's containing
    // type is internal. Bind this ONE pinned seam; no private fields or per-record mappings.
    // Fail at the boundary on API drift, never fall back to Windows environment discovery.
    static readonly Lazy<Func<IMutagenReadStream, ModKey, SkyrimRelease, bool, ISkyrimModGetter>> OverlayFactory = new(() =>
    {
        var type = typeof(SkyrimMod).Assembly.GetType("Mutagen.Bethesda.Skyrim.SkyrimModBinaryOverlay", throwOnError: true)!;
        var method = type.GetMethod("SkyrimModFactory", BindingFlags.Public | BindingFlags.Static, null,
            new[] { typeof(IMutagenReadStream), typeof(ModKey), typeof(SkyrimRelease), typeof(bool) }, null)
            ?? throw new NotSupportedException("Mutagen stream-overlay API changed; explicit strings lookup needs revalidation.");
        return method.CreateDelegate<Func<IMutagenReadStream, ModKey, SkyrimRelease, bool, ISkyrimModGetter>>();
    });

    internal static ISkyrimModGetter OpenOverlay(IMutagenReadStream stream, ModKey key) =>
        OverlayFactory.Value(stream, key, SkyrimRelease.SkyrimSE, true);

    readonly string _folder;
    readonly ModKey _modKey;
    readonly Func<string, PlacementSource?>? _vfs;
    readonly Lazy<Dictionary<(StringsSource, Language), Entry>> _tables;
    sealed record Entry(string Source, bool Loose, Lazy<IStringsLookup> Table);

    public ExplicitStringsLookup(string folder, ModKey modKey, Func<string, PlacementSource?>? vfs = null)
    {
        _folder = folder;
        _modKey = modKey;
        _vfs = vfs;
        _tables = new(LoadTables);
    }

    static readonly (StringsSource Source, string Extension)[] SourceExtensions =
        { (StringsSource.Normal, "strings"), (StringsSource.IL, "ilstrings"), (StringsSource.DL, "dlstrings") };

    /// <summary>Strings tables as the MO2 VFS resolves them (2026-10-26): for each strings type × language, ask the
    /// injected resolver for the WINNING copy of <c>strings/&lt;plugin&gt;_&lt;language&gt;.&lt;ext&gt;</c> — a loose
    /// translation mod beats the vanilla BSA exactly as in game. The resolver already chose one winner per path, so
    /// there is no ambiguity to report here. A resolver fault is not swallowed into "absent" silently: it propagates
    /// to the read, which surfaces it as an unreadable field (Q3).</summary>
    Dictionary<(StringsSource, Language), Entry> LoadVfsTables()
    {
        var tables = new Dictionary<(StringsSource, Language), Entry>();
        foreach (var language in Enum.GetValues<Language>())
        foreach (var (source, ext) in SourceExtensions)
        {
            var fileName = $"{_modKey.Name}_{language.ToString().ToLowerInvariant()}.{ext}";
            // Only names Mutagen's own parser maps back to this (source, language) — never a guessed spelling.
            if (!StringsUtility.TryRetrieveInfoFromString(StringsLanguageFormat.FullName, fileName.AsSpan(),
                    out var parsedSource, out var parsedLanguage, out _)
                || parsedSource != source || parsedLanguage != language) continue;
            var rel = "strings/" + fileName;
            var hit = _vfs!(rel);
            if (hit is null) continue;
            var encoding = MutagenEncoding.GetEncoding(GameRelease.SkyrimSE, language);
            if (hit.Kind == AssetKind.Loose && hit.LooseFilePath is { } file)
                tables[(source, language)] = new(file, true, new(() => new StringsLookupOverlay(file, source, encoding)));
            else if (hit.ArchivePath is { } archive)
                tables[(source, language)] = new(archive + ":" + hit.EntryPath, false, new(() =>
                {
                    var bytes = AssetResolver.TryReadArchiveEntry(archive, hit.EntryPath)
                        ?? throw new InvalidDataException($"strings entry '{hit.EntryPath}' vanished from '{archive}'.");
                    return new StringsLookupOverlay(bytes, source, encoding);
                }));
        }
        return tables;
    }

    Dictionary<(StringsSource, Language), Entry> LoadTables()
    {
        if (_vfs is not null)
        {
            var vfsTables = LoadVfsTables();
            if (vfsTables.Count > 0) return vfsTables;          // nothing in the VFS → the folder scan below, unchanged
        }
        var tables = new Dictionary<(StringsSource, Language), Entry>();
        if (!Directory.Exists(_folder)) return tables;
        bool Match(string name, out StringsSource source, out Language language) =>
            StringsUtility.TryRetrieveInfoFromString(StringsLanguageFormat.FullName, name.AsSpan(),
                out source, out language, out var modName)
            && modName.Equals(_modKey.Name.AsSpan(), StringComparison.OrdinalIgnoreCase);

        void Add((StringsSource, Language) key, Entry entry)
        {
            if (tables.TryGetValue(key, out var existing))
            {
                if (existing.Loose && !entry.Loose) return;
                throw new InvalidDataException($"Ambiguous strings for {_modKey}: '{existing.Source}' and '{entry.Source}'. " +
                    "Supply one winning strings table in the selected plugin folder; no archive winner was guessed.");
            }
            tables.Add(key, entry);
        }

        foreach (var stringsDir in Directory.EnumerateDirectories(_folder)
                     .Where(p => Path.GetFileName(p).Equals("Strings", StringComparison.OrdinalIgnoreCase)))
        foreach (var file in Directory.EnumerateFiles(stringsDir))
        {
            if (!Match(Path.GetFileName(file), out var source, out var language)) continue;
            Add((source, language), new(file, true, new(() => new StringsLookupOverlay(
                file, source, MutagenEncoding.GetEncoding(GameRelease.SkyrimSE, language)))));
        }

        foreach (var path in Directory.EnumerateFiles(_folder)
                     .Where(p => Path.GetExtension(p).Equals(".bsa", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var archive = Archive.CreateReader(GameRelease.SkyrimSE, path);
            if (!archive.TryGetFolder("strings", out var folder)) continue;
            foreach (var file in folder.Files)
            {
                var name = file.Path.Replace('\\', '/').Split('/')[^1];
                if (!Match(name, out var source, out var language)) continue;
                Add((source, language), new(path + ":" + file.Path, false, new(() =>
                    new StringsLookupOverlay(file.GetMemorySlice(), source,
                        MutagenEncoding.GetEncoding(GameRelease.SkyrimSE, language)))));
            }
        }
        return tables;
    }

    public IReadOnlyCollection<Language> AvailableLanguages(StringsSource source) =>
        _tables.Value.Keys.Where(k => k.Item1 == source).Select(k => k.Item2).ToArray();

    public bool TryLookup(StringsSource source, Language language, uint key,
        [MaybeNullWhen(false)] out string str)
    {
        if (_tables.Value.TryGetValue((source, language), out var entry))
            return entry.Table.Value.TryLookup(key, out str);
        str = null;
        return false;
    }

    public TranslatedString CreateString(StringsSource source, uint key, Language targetLanguage)
    {
        var result = new TranslatedString(targetLanguage);
        foreach (var language in AvailableLanguages(source))
            if (TryLookup(source, language, key, out var value)) result.Set(language, value);
        return result;
    }
}
