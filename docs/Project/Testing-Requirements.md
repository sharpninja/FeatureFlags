## 10. Testing Requirements

Per the Byrd Process, TDD drives Implementation. The Testing Requirements artifact begins here.

**Unit tests** shall exist for every public method of every component listed in section 6, written before implementation per TDD. The rule engine specifically requires a property-based test suite that asserts determinism: for any (Manifest, Context) pair, repeated evaluation yields identical results, and the same evaluation on Android, iOS, Windows, macOS, and Linux yields identical results.

**Integration tests** shall cover at minimum: end-to-end Manifest fetch + verification + cache + evaluation against a stub Distribution service; offline boot using only bundled defaults; recovery from corrupt cache; signature-failure rejection; cache eviction under disk pressure; ProductScope enforcement; kill-switch propagation latency.

**Avalonia sample integration tests** shall include a complete Avalonia 12 sample application that exercises project-level and feature-level flag resolution together. The sample shall define two Projects and two Features, with expected output snapshots for every Project x Feature combination, including enabled and disabled paths. The integration suite shall validate that the rendered/sample-observable output matches the manifest-defined expectations for each scenario and that product scoping prevents a Project from resolving flags outside its declared scope.

**Cross-platform validation** shall run the integration test suite on a CI matrix covering all five platforms via .NET MAUI workloads and (for Linux) plain .NET 10.

**Human validation** shall cover: admin-plane workflow usability with at least three operators; verification that a non-engineer can read and understand a rule before it is published; verification that a published kill-switch reaches a sample app on each platform within the documented SLA.

