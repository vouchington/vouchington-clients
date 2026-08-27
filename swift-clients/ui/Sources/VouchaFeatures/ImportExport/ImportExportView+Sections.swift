import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension ImportExportView {
    var importSection: some View {
        Section(UiMessages.string(
            route.isTopics ? .nativeSwiftImportExportImportTopics : .nativeSwiftImportExportImportSources,
            locale: nativeUiLocale
        )) {
            TextEditor(text: $viewModel.inputText)
                .frame(minHeight: 120)
                .accessibilityLabel(UiMessages.string(
                    route.isTopics ? .nativeSwiftImportExportTopicNames : .nativeSwiftImportExportSourceUrls,
                    locale: nativeUiLocale
                ))
            Text(UiMessages.string(
                route.isTopics
                    ? .nativeSwiftImportExportTopicInstructions
                    : .nativeSwiftImportExportSourceInstructions,
                locale: nativeUiLocale
            ))
            .font(.caption)
            .foregroundStyle(.secondary)
            HStack {
                Button(UiMessages.string(
                    route.isTopics ? .nativeSwiftImportExportImportTopics : .nativeSwiftImportExportImportUrls,
                    locale: nativeUiLocale
                )) {
                    Task { await viewModel.importText() }
                }
                .disabled(viewModel.isWorking)
                if !route.isTopics {
                    Button(UiMessages.string(
                        .nativeSwiftImportExportChooseFile,
                        locale: nativeUiLocale
                    )) { showingFileImporter = true }
                        .disabled(viewModel.isWorking)
                }
            }
        }
    }

    var progressSection: some View {
        Section(UiMessages.string(.nativeSwiftImportExportImportProgress, locale: nativeUiLocale)) {
            if let summary = viewModel.sourceSummary {
                ProgressView(
                    value: Double(summary.completedRows + summary.failedRows),
                    total: Double(max(1, summary.totalRows))
                )
                Text(UiMessages.string(
                    UiMessage(
                        .nativeSwiftImportExportProgressSummary,
                        numberParameters: [
                            "completed": Double(summary.completedRows),
                            "failed": Double(summary.failedRows),
                            "pending": Double(summary.pendingRows)
                        ]
                    ),
                    locale: nativeUiLocale
                ))
                .font(.caption)
                if viewModel.isMonitoring {
                    Button(UiMessages.string(
                        .nativeSwiftImportExportStopMonitoring,
                        locale: nativeUiLocale
                    ), role: .cancel) { viewModel.stopMonitoring() }
                } else if viewModel.canResumeMonitoring {
                    Button(UiMessages.string(
                        viewModel.errorMessage == nil
                            ? .nativeSwiftImportExportResumeMonitoring
                            : .nativeSwiftImportExportTryStatusAgain,
                        locale: nativeUiLocale
                    )) {
                        viewModel.resumeMonitoring()
                    }
                }
            }
            ForEach(viewModel.resultRows) { row in
                let result = row.result
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    Text(result.input.isEmpty
                        ? UiMessages.string(.nativeSwiftImportExportUntitledRow, locale: nativeUiLocale)
                        : result.input)
                    Text(UiMessages.string(statusText(result), locale: nativeUiLocale))
                        .font(.caption)
                        .foregroundStyle(result.status == .error ? .red : .secondary)
                }
                .accessibilityElement(children: .combine)
            }
        }
    }

    var exportSection: some View {
        Section(UiMessages.string(
            route.isTopics ? .nativeSwiftImportExportExportTopics : .nativeSwiftImportExportExportSources,
            locale: nativeUiLocale
        )) {
            if !route.isTopics {
                Picker(
                    UiMessages.string(.nativeSwiftImportExportSourceType, locale: nativeUiLocale),
                    selection: $viewModel.exportFilter
                ) {
                    Text(UiMessages.string(.nativeSwiftImportExportAllSources, locale: nativeUiLocale))
                        .tag(SourceFeedType?.none)
                    ForEach(ImportExportViewModel.selectableExportFilters) { type in
                        Text(UiMessages.string(type.importExportTitleKey, locale: nativeUiLocale))
                            .tag(Optional(type))
                    }
                }
                Picker(
                    UiMessages.string(.nativeSwiftImportExportFormat, locale: nativeUiLocale),
                    selection: $viewModel.exportFormat
                ) {
                    ForEach(SourceExportFormat.allCases) { format in
                        Text(UiMessages.string(format.importExportTitleKey, locale: nativeUiLocale)).tag(format)
                    }
                }
                .pickerStyle(.segmented)
            }
            Button(UiMessages.string(
                .nativeSwiftImportExportPrepareExport,
                locale: nativeUiLocale
            )) { Task { await viewModel.prepareExport() } }
                .disabled(viewModel.isWorking)
            if let exportURL = viewModel.exportURL {
                ShareLink(item: exportURL) {
                    Label(
                        UiMessages.string(.nativeSwiftImportExportShareExport, locale: nativeUiLocale),
                        systemImage: "square.and.arrow.up"
                    )
                }
            }
        }
    }
}
