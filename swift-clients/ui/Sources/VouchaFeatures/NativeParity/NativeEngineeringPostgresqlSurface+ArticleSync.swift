import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

extension NativeEngineeringPostgresqlSurface {
    func articleSyncSection(_ viewModel: NativeEngineeringPostgresqlViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            sectionTitle(UiMessages.string(.nativeSwiftEngineeringPostgresqlArticleSync, locale: nativeUiLocale))
            Text(UiMessages.string(
                .nativeSwiftEngineeringPostgresqlSyncArticleContentFromStorageIntoPosts,
                locale: nativeUiLocale
            ))
            .font(.subheadline)
            .foregroundStyle(Colors.secondaryLabel)

            HStack(spacing: Spacing.sm) {
                Button {
                    Task { await viewModel.triggerArticleSync() }
                } label: {
                    Label(
                        UiMessages.string(.nativeSwiftEngineeringPostgresqlSyncArticles, locale: nativeUiLocale),
                        systemImage: "arrow.triangle.2.circlepath"
                    )
                }
                .buttonStyle(.borderedProminent)
                .disabled(viewModel.isActionLoading("article-sync"))

                if let jobId = viewModel.articleSyncJobId {
                    Button {
                        Task { await viewModel.refreshArticleSyncStatus() }
                    } label: {
                        Label(
                            UiMessages.string(.nativeSwiftEngineeringPostgresqlRefreshStatus, locale: nativeUiLocale),
                            systemImage: "arrow.clockwise"
                        )
                    }
                    .buttonStyle(.bordered)
                    .disabled(viewModel.isActionLoading("article-sync-status|\(jobId)"))
                }
            }

            if let status = viewModel.articleSyncStatus {
                articleSyncStatusView(status)
            } else if let jobId = viewModel.articleSyncJobId {
                Text(UiMessages.string(
                    .nativeSwiftEngineeringPostgresqlJobQueued,
                    parameters: ["id": UiMessages.string(.verbatim(jobId), locale: nativeUiLocale)],
                    locale: nativeUiLocale
                ))
                .font(.caption)
                .foregroundStyle(Colors.secondaryLabel)
            }
        }
    }

    @ViewBuilder
    func articleSyncStatusView(_ status: ArticleSyncJobStatus) -> some View {
        switch status {
        case .active:
            Text(UiMessages.string(.nativeSwiftEngineeringPostgresqlArticleSyncIsActive, locale: nativeUiLocale))
                .font(.caption)
        case let .completed(result):
            Text(articleSyncCompletedSummary(result.summary))
                .font(.caption)
        case let .failed(error):
            Text(UiMessages.string(
                .nativeSwiftEngineeringPostgresqlFailedWithError,
                parameters: ["error": UiMessages.string(.verbatim(error), locale: nativeUiLocale)],
                locale: nativeUiLocale
            ))
            .font(.caption)
        }
    }

    private func articleSyncCompletedSummary(_ summary: ArticleSyncSummary) -> String {
        UiMessages.string(
            .nativeSwiftEngineeringPostgresqlCompletedSummary,
            parameters: [
                "created": UiMessages.number(summary.created, locale: nativeUiLocale),
                "updated": UiMessages.number(summary.updated, locale: nativeUiLocale),
                "skipped": UiMessages.number(summary.skipped, locale: nativeUiLocale),
                "errors": UiMessages.number(summary.errored, locale: nativeUiLocale)
            ],
            locale: nativeUiLocale
        )
    }
}
