import Foundation
import VouchaLocalization
import VouchaModels

extension ImportExportView {
    func statusText(_ result: ImportResult) -> UiVerbatimText {
        if result.status == .pending, let error = result.error {
            return .message(
                .nativeSwiftImportExportRetrying,
                textParameters: ["error": .verbatim(error)]
            )
        }
        if result.status == .error {
            return .message(
                .nativeSwiftImportExportFailed,
                textParameters: [
                    "error": result.error.map(UiVerbatimText.verbatim)
                        ?? .message(.nativeSwiftImportExportUnknownError)
                ]
            )
        }
        return .message(result.status.importExportTitleKey)
    }

    func handleFileSelection(_ selection: Result<[URL], Error>) {
        do {
            guard let url = try selection.get().first else { return }
            viewModel.selectImportFile(url)
        } catch {
            presentedError = (error as? LocalizedError)?.errorDescription.map(UiVerbatimText.verbatim)
                ?? .message(.nativeSwiftImportExportFileSelectionFailed)
        }
    }
}

extension SourceFeedType {
    var importExportTitleKey: UiMessageKey {
        switch self {
        case .article: .nativeSwiftImportExportArticle
        case .podcast: .nativeSwiftImportExportPodcast
        case .video: .nativeSwiftImportExportVideo
        case .mixed: .nativeSwiftImportExportAllSources
        }
    }
}

extension SourceExportFormat {
    var importExportTitleKey: UiMessageKey {
        switch self {
        case .csv: .nativeSwiftImportExportCsv
        case .opml: .nativeSwiftImportExportOpml
        }
    }
}

extension ImportResultStatus {
    var importExportTitleKey: UiMessageKey {
        switch self {
        case .pending: .nativeSwiftImportExportPending
        case .followed: .nativeSwiftImportExportFollowed
        case .imported: .nativeSwiftImportExportImported
        case .sourceCreated: .nativeSwiftImportExportSourceCreated
        case .recommendationCreated: .nativeSwiftImportExportRecommendationCreated
        case .alreadyFollowing: .nativeSwiftImportExportAlreadyFollowing
        case .error: .nativeSwiftImportExportFailed
        }
    }
}
