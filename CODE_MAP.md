# Record-name source map

This focused map covers record-name reading and its regression checks.

| Entry | Responsibility |
|---|---|
| [LoadOrderResolver](src/housecarl-core/LoadOrderResolver.cs) | Overlay lifetime and strings source folder |
| [ExplicitStringsLookup](src/housecarl-core/ExplicitStringsLookup.cs) | Unix loose/BSA strings without Windows discovery |
| [RecordNameDisplay](src/housecarl-core/RecordNameDisplay.cs) | Strict UTF-8 display interpretation |
| [ReadEngine](src/housecarl-core/ReadEngine.cs), [FieldPredicate](src/housecarl-core/FieldPredicate.cs) | Raw tokens, display, semantic comparison |
| [LoadOrderService](src/housecarl-mcp/LoadOrderService.cs) | Resolved identity names |
| [RecordNamesProbe](src/housecarl-generator/RecordNamesProbe.cs), [RecordNameDisplayProbe](src/housecarl-generator/RecordNameDisplayProbe.cs) | Synthetic guards |
| [RecordNamesLiveProbe](src/housecarl-generator/RecordNamesLiveProbe.cs) | Opt-in supplied-file acceptance |

Contract and commands: [RECORD_NAMES.md](RECORD_NAMES.md).
