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
    readonly Lazy<Dictionary<(StringsSource, Language), Entry>> _tables;
    sealed record Entry(string Source, bool Loose, Lazy<IStringsLookup> Table);

    public ExplicitStringsLookup(string folder, ModKey modKey)
    {
        _folder = folder;
        _modKey = modKey;
        _tables = new(LoadTables);
    }

    Dictionary<(StringsSource, Language), Entry> LoadTables()
    {
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
