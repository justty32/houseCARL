# Record names: strings lookup and encoding

Two independent failures affected Linux reads. Localized records store a string-table ID, not
inline text. Mutagen 0.53.1's default BSA strings discovery constructs Windows plugin-listing
paths even with explicit folder overrides; on Linux this can throw a LocalAppData exception.
Separately, UTF-8 bytes in inline English translated fields are read through Windows-1252,
producing mojibake. Changing the global encoding would also change copy/write semantics.

## Localized lookup

`LoadOrderResolver.OpenOverlay` retains the original public Mutagen factory on Windows. On
Unix, it supplies a strings lookup scoped to the plugin's folder, falling back to the real
game Data folder only when the plugin folder has no strings source. This preserves the
cleaned-master fallback and mod-local strings precedence. Folder/extension detection is
case-insensitive. Loose tables take priority over archived tables. Competing archive tables
for the same plugin, language, and strings type fail explicitly: this API has no archive
load order with which to select a winner. It does not silently guess a profile-wide VFS order.

Mutagen 0.53.1 exposes `ParsingMeta.StringsLookup`, but its public overlay builders rebuild
that metadata and the public strings parameters cannot override archive listing discovery.
The Unix path therefore binds one generated stream factory on an internal type by its exact
signature. API drift fails at this boundary; there is no fallback to Windows discovery.
The factory owns the stream and normal overlay disposal closes it. Upgrading Mutagen requires
revalidating this seam and the guards. Windows behavior is retained by code path; the Linux
guards do not constitute a Windows runtime test.

## MO2 VFS strings source (2026-10-26)

A localized plugin's strings are what the game reads through the MO2 VFS, not necessarily the tables
beside the plugin. Translation mods commonly ship loose `strings/<plugin>_english.strings` (Chinese
text in the English slot, `sLanguage=ENGLISH`), sometimes in a different mod folder from the plugin.
For in-order reads the service injects `LoadOrderResolver.StringsVfs`: for each strings type and
language, the asset resolver's winner for `strings/<plugin>_<language>.<ext>` (loose overwrite > mod
priority > Data, then BSA by plugin rank) supplies the table. Only when the VFS has no table for the
plugin does the folder scan above apply. Off-order opens (`read_plugin_file`, off-order CopyFrom and
donor lanes) keep the folder scan. Windows keeps Mutagen's public factory and is unchanged.

Writes that copy a localized winner into a patch now carry the VFS text (what the game displays)
instead of the vanilla archive text.

## Tokens, display, and predicates

`read_record` retains its byte-preserving `value` token for write replay. For translated text,
it can also emit a plain Unicode `display`; text output shows that display and JSON consumers
should prefer `display` for human labels, falling back to `value`. `resolve` and
`resolve_names` use the same semantic name. Existing translated-text comparisons, including
wildcard predicates, compare this interpretation; ordinary strings such as EditorID, flags,
numbers, and FormLink tokens retain their existing behavior.

The display interpretation requires reversible Windows-1252 encoding and strict UTF-8
decoding. ASCII, already-Unicode CJK, and invalid UTF-8 remain unchanged. Some byte sequences
are valid in both encodings: the literal Windows-1252 text `Ã©` shares bytes with UTF-8 `é`.
Display and predicates deliberately prefer UTF-8 in this ambiguous case. This is an explicit
interpretation contract, not universal encoding detection. No traditional/simplified Chinese
conversion occurs. Do not feed display strings back into the legacy writer in place of tokens.
Raw token replay and numeric-only copy/override writes remain covered by byte-level guards.

## Offline verification

With the repository's .NET dependencies available, run:

```sh
dotnet run --project src/housecarl-generator -- record-names-guard
dotnet run --project src/housecarl-generator -- record-name-display-guard
dotnet run --project src/housecarl-generator -- strings-vfs-guard
```

All three are registered in `ci-all`. They create temporary synthetic plugins and a BSA, exercise
localized and inline names, cleaned-master fallback, own-folder precedence, ambiguity errors,
overlay disposal on Linux, link names, predicates, and write-token/copy preservation. The
localized Unix cases unset LocalAppData for the probe and restore it afterward.

An optional read-only real-file probe accepts user-supplied paths:

```sh
dotnet run --project src/housecarl-generator -- record-names-read \
  "$GAME_DATA_DIR" "$USSEP_PLUGIN_PATH" '小麦'
```

It checks English vanilla Gold001 and HearthFires Wheat, plus the supplied USSEP wheat name
(third argument), text/JSON reads, resolved/link identities, and a name predicate. It prints
input and assembly SHA-256 hashes. It loads exactly five vanilla masters plus that USSEP file,
not the whole active profile, and does not modify any input file or service. Choose the
expected name from the actual supplied translation; a different Chinese glyph is not equal.
Passing this executable does not mean a running MCP service has loaded these changes.
