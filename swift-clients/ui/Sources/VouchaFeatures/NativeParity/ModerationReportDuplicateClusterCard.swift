import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct ModerationReportDuplicateClusterCard: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let duplicateCluster: StaffModerationReportDuplicateCluster
    @Bindable
    var viewModel: ModerationReportsViewModel
    let onRemove: () -> Void

    var body: some View {
        let counts = viewModel.presentationCounts(for: duplicateCluster)
        GroupBox {
            VStack(alignment: .leading, spacing: Spacing.sm) {
                Label(UiMessages.string(signalLabel, locale: nativeUiLocale), systemImage: "doc.on.doc.fill")
                    .font(Typography.headline)
                Text(UiMessages.string(
                    .nativeSwiftModerationReportsPostReportCounts,
                    parameters: [
                        "posts": countText(.nativeSwiftModerationReportsPostCount, count: counts.posts),
                        "reports": countText(.nativeSwiftModerationReportsReportCount, count: counts.reports)
                    ],
                    locale: nativeUiLocale
                ))
                Text(UiMessages.string(
                    .nativeSwiftModerationReportsLatest,
                    parameters: [
                        "date": UiMessages.date(
                            duplicateCluster.lastReportedAt,
                            date: .abbreviated,
                            time: .shortened,
                            locale: nativeUiLocale,
                            timeZone: .current
                        )
                    ],
                    locale: nativeUiLocale
                ))
                HStack {
                    ForEach(duplicateCluster.reasonBreakdown, id: \.reason) { reason in
                        Text(UiMessages.string(
                            .nativeSwiftModerationReportsReasonCount,
                            parameters: [
                                "reason": reason.reason,
                                "count": UiMessages.number(reason.count, locale: nativeUiLocale)
                            ],
                            locale: nativeUiLocale
                        ))
                    }
                }
                if viewModel.viewerTier.canRemove, hasLoadedTargets {
                    Button(UiMessages.string(
                        .nativeSwiftModerationReportsRemoveLoadedPosts,
                        locale: nativeUiLocale
                    ), role: .destructive, action: onRemove)
                        .disabled(viewModel.isQueueActionInProgress)
                }
            }
            .font(Typography.caption)
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }

    private var hasLoadedTargets: Bool {
        viewModel.hasActiveReports(in: duplicateCluster)
    }

    private var signalLabel: UiVerbatimText {
        .message(
            duplicateCluster.signal == "embeddings_similarity"
                ? .nativeSwiftModerationReportsSimilarContentWave
                : .nativeSwiftModerationReportsDuplicateContentWave
        )
    }

    private func countText(_ key: UiMessageKey, count: Int) -> String {
        UiMessages.string(
            UiMessage(
                key,
                numberParameters: ["count": Double(count)]
            ),
            locale: nativeUiLocale
        )
    }
}
