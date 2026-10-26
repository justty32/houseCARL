# Record-name source map

This focused map covers record-name reading and its regression checks.

| Entry | Responsibility |
|---|---|
| [LoadOrderResolver](src/housecarl-core/LoadOrderResolver.cs) | Overlay lifetime and strings source folder |
| [ExplicitStringsLookup](src/housecarl-core/ExplicitStringsLookup.cs) | Unix loose/BSA strings without Windows discovery; MO2-VFS winner first when injected |
| [RecordNameDisplay](src/housecarl-core/RecordNameDisplay.cs) | Strict UTF-8 display interpretation |
| [ReadEngine](src/housecarl-core/ReadEngine.cs), [FieldPredicate](src/housecarl-core/FieldPredicate.cs) | Raw tokens, display, semantic comparison |
| [LoadOrderService](src/housecarl-mcp/LoadOrderService.cs) | Resolved identity names; `ResolveStringsWinner` feeds the asset resolver to strings lookup |
| [RecordNamesProbe](src/housecarl-generator/RecordNamesProbe.cs), [RecordNameDisplayProbe](src/housecarl-generator/RecordNameDisplayProbe.cs), [StringsVfsProbe](src/housecarl-generator/StringsVfsProbe.cs) | Synthetic guards |
| [RecordNamesLiveProbe](src/housecarl-generator/RecordNamesLiveProbe.cs) | Opt-in supplied-file acceptance |

Contract and commands: [RECORD_NAMES.md](RECORD_NAMES.md).
