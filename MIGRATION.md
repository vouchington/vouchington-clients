# Native client extraction

This repository was bootstrapped from an exact snapshot of the native clients in
[`jonathanong/filaments`](https://github.com/jonathanong/filaments).

- Source commit: `fbffcbf9acf631940347a06a50daaeeb5d04fc85`
- Swift subtree: `a74933953b41648969c9f990f9953a1eeb0cc956`
- .NET subtree: `e90989e10a58e8ab74b12ef9e1d437fb21aa8f3f`
- Imported paths: `swift-clients/`, `dotnet-clients/`

The import intentionally preserves the source paths, file modes, and blobs. It
does not copy Filaments history. Use the source commit above for pre-extraction
history and provenance.

## Post-snapshot native behavior audit

The extraction snapshot predates Filaments commit
[`47cf489b42d074d2e7e4bfdb0ddee36bbba34962`](https://github.com/jonathanong/filaments/commit/47cf489b42d074d2e7e4bfdb0ddee36bbba34962)
(`fix(streaming): bound large-file memory paths`). This port audited the exact
source window `55084d48f644addc290fe8aed701405c761dd504..47cf489b42d074d2e7e4bfdb0ddee36bbba34962`.
Every one of its 55 native paths is classified below against this repository at
the `56f9f1a19fd919b83fdede6d670d4624a2a241b4` baseline.

- **Added (8):** the source introduced the file and it was absent here.
- **Updated (37):** the pre-port client blob matched the source-window base, so
  the behavior was applied directly. Two dependent call sites retain the
  client’s existing endpoint names while invoking the ported behavior.
- **Semantically merged (9):** the client had post-snapshot import/export or
  fixture work. The port preserves its `ExportTopicsDownload` /
  `exportTopicsDownload` public naming while adopting the bounded-read and
  owned-download behavior.
- **Already ported (1):** the client already matched the source-window result.

### Added (8)

- `dotnet-clients/src/Voucha.Client.Core/Api/CappedSseLineReader.cs`
- `dotnet-clients/src/Voucha.Client.Core/ImportExport/ExportShareFileStager.cs`
- `dotnet-clients/tests/Voucha.Client.Core.Tests/ImportExport/ExportShareFileStagerTests.cs`
- `dotnet-clients/tests/Voucha.Client.Core.Tests/ImportExport/ImportExportTestDocuments.cs`
- `swift-clients/core/Sources/VouchaAPI/ResponseBodyLimit.swift`
- `swift-clients/core/Sources/VouchaAPI/ResponseLinesURLSessionDataDelegateTransport+State.swift`
- `swift-clients/core/Tests/VouchaCoreTests/ResponseBodyLimitTests.swift`
- `swift-clients/ui/Tests/VouchaUITests/ImportExportDownloadFileTests.swift`

### Updated (37)

- `dotnet-clients/src/Voucha.Client.App/Pages/ImportExportPage.xaml.cs`
- `dotnet-clients/src/Voucha.Client.App/Support/ImportExportFileAdapter.cs`
- `dotnet-clients/src/Voucha.Client.Core/Api/VouchaApiClient.Chat.Streaming.cs`
- `dotnet-clients/src/Voucha.Client.Core/Api/VouchaApiClient.ImportExport.cs`
- `dotnet-clients/src/Voucha.Client.Core/Api/VouchaApiClient.Transport.cs`
- `dotnet-clients/src/Voucha.Client.Core/ImportExport/ApiImportExportService.cs`
- `dotnet-clients/src/Voucha.Client.Core/ImportExport/ImportExportModels.cs`
- `dotnet-clients/src/Voucha.Client.Core/ImportExport/ImportExportViewModel.Operations.cs`
- `dotnet-clients/src/Voucha.Client.Core/ImportExport/ImportExportViewModel.cs`
- `dotnet-clients/tests/Voucha.Client.Core.Tests/Api/ChatStreamTerminationTests.cs`
- `dotnet-clients/tests/Voucha.Client.Core.Tests/Api/VouchaApiTransportTests.cs`
- `dotnet-clients/tests/Voucha.Client.Core.Tests/ImportExport/ImportExportExportLifecycleTests.cs`
- `dotnet-clients/tests/Voucha.Client.Core.Tests/ImportExport/ImportExportFileAdapterTests.cs`
- `dotnet-clients/tests/Voucha.Client.Core.Tests/ImportExport/ImportExportViewModelTests.cs`
- `dotnet-clients/tests/Voucha.Client.Core.Tests/Support/ImportExportMauiWiringTests.cs`
- `swift-clients/core/.periphery.yml`
- `swift-clients/core/Sources/VouchaAPI/APIClient+ChatSupport.swift`
- `swift-clients/core/Sources/VouchaAPI/APIClient+RawData.swift`
- `swift-clients/core/Sources/VouchaAPI/ChatSSEReader.swift`
- `swift-clients/core/Sources/VouchaAPI/ResponseLinesURLSessionDataDelegateTransport+Finish.swift`
- `swift-clients/core/Sources/VouchaAPI/ResponseLinesURLSessionDataDelegateTransport.swift`
- `swift-clients/core/Sources/VouchaModels/ChatSSEParser.swift`
- `swift-clients/core/Tests/VouchaCoreTests/ChatSSEReaderTests.swift`
- `swift-clients/core/Tests/VouchaCoreTests/ResponseLinesStreamingTests.swift`
- `swift-clients/ui/.periphery.yml`
- `swift-clients/ui/Sources/VouchaFeatures/ImportExport/ImportExportService.swift`
- `swift-clients/ui/Sources/VouchaFeatures/ImportExport/ImportExportViewModel+Operations.swift`
- `swift-clients/ui/Sources/VouchaFeatures/ImportExport/NativeImportExportFiles.swift`
- `swift-clients/ui/Sources/VouchaFeatures/NativeParity/NativeTopicImageUploadService.swift`
- `swift-clients/ui/Tests/VouchaUITests/ImportExportConcurrencyTests.swift`
- `swift-clients/ui/Tests/VouchaUITests/ImportExportFileSelectionRaceTests.swift`
- `swift-clients/ui/Tests/VouchaUITests/ImportExportSourceExportAcceptanceTests.swift`
- `swift-clients/ui/Tests/VouchaUITests/ImportExportTopicRetryViewTests.swift`
- `swift-clients/ui/Tests/VouchaUITests/ImportExportViewModelTests.swift`
- `swift-clients/ui/Tests/VouchaUITests/ImportExportViewTests.swift`
- `swift-clients/ui/Tests/VouchaUITests/NativeImportExportFilesTests.swift`
- `swift-clients/ui/Tests/VouchaUITests/NativeTopicImageUploadServiceTests.swift`

### Semantically merged (9)

- `dotnet-clients/src/Voucha.Client.Core/Api/VouchaApiEndpoints.ImportExport.cs`
- `dotnet-clients/tests/Voucha.Client.Core.Tests/Api/ApiFixtureCoverage.Endpoints.cs`
- `dotnet-clients/tests/Voucha.Client.Core.Tests/Api/ApiFixtureCoverage.Registry.cs`
- `dotnet-clients/tests/Voucha.Client.Core.Tests/Api/ApiFixtureEndpointCoverageTests.cs`
- `dotnet-clients/tests/Voucha.Client.Core.Tests/Api/ImportExportApiTests.cs`
- `swift-clients/core/Sources/VouchaAPI/Endpoint+ImportExport.swift`
- `swift-clients/core/Tests/VouchaCoreTests/ApiFixtureEndpointCoverageTests.swift`
- `swift-clients/core/Tests/VouchaCoreTests/EndpointManifestCoverage.ImportExport.swift`
- `swift-clients/core/Tests/VouchaCoreTests/ImportExportEndpointTests.swift`

### Already ported (1)

- `swift-clients/core/Tests/VouchaCoreTests/ApiFixtureCoverage.ImportExportRegistry.swift`

The Swift UI package is separately pinned directly to SwiftSoup `2.13.9`
(`18b80329749eca5ea29fc50211dca5c7eff5bfec`). Android retains its transitive
SwiftSoup `2.13.7` lock because this behavior port does not require a resolution
change there.
