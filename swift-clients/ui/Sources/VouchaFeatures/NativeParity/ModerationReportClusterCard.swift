import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct StaffModerationReportClusterCard: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let cluster: StaffModerationReportEntityCluster
    @Bindable
    var viewModel: ModerationReportsViewModel
    let onNavigate: (String) -> Void
    let onConfirm: (ModerationReportsConfirmation) -> Void

    var body: some View {
        let reports = viewModel.activeReports(in: cluster)
        if !reports.isEmpty {
            DisclosureGroup {
                VStack(alignment: .leading, spacing: Spacing.sm) {
                    reasons
                    indicators
                    if viewModel.status == .pending {
                        Button(UiMessages.string(
                            .nativeSwiftModerationReportsDismissLoadedReports,
                            locale: nativeUiLocale
                        )) {
                            onConfirm(.clusterDismiss(cluster.id))
                        }
                        .disabled(viewModel.isQueueActionInProgress || reports.allSatisfy(\.isSystemGenerated))
                    }
                    ForEach(reports) { report in
                        StaffModerationReportCard(
                            report: report,
                            viewModel: viewModel,
                            onNavigate: onNavigate,
                            onConfirm: onConfirm
                        )
                    }
                }
                .padding(.top, Spacing.sm)
            } label: {
                VStack(alignment: .leading) {
                    Text(cluster.targetLabel ?? cluster.entityType).font(Typography.headline)
                    Text(UiMessages.string(
                        .nativeSwiftModerationReportsClusterSummary,
                        parameters: [
                            "reports": reportCountText(viewModel.activeReportCount(in: cluster)),
                            "reporters": reporterCountText(cluster.reporterCount)
                        ],
                        locale: nativeUiLocale
                    ))
                    .foregroundStyle(.secondary)
                    Text(UiMessages.string(
                        .nativeSwiftModerationReportsLatest,
                        parameters: [
                            "date": UiMessages.date(
                                cluster.lastReportedAt,
                                date: .abbreviated,
                                time: .shortened,
                                locale: nativeUiLocale,
                                timeZone: .current
                            )
                        ],
                        locale: nativeUiLocale
                    ))
                    .font(Typography.caption)
                }
            }
        }
    }

    private var reasons: some View {
        HStack {
            ForEach(cluster.reasonBreakdown, id: \.reason) { reason in
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
        .font(Typography.caption)
    }

    private var indicators: some View {
        HStack {
            if cluster.indicators.contentHashDuplicate {
                Label(
                    UiMessages.string(.nativeSwiftModerationReportsDuplicateContent, locale: nativeUiLocale),
                    systemImage: "doc.on.doc"
                )
            }
            if cluster.indicators.embeddingsSimilarity {
                Label(
                    UiMessages.string(.nativeSwiftModerationReportsSimilarContent, locale: nativeUiLocale),
                    systemImage: "equal.circle"
                )
            }
            if cluster.indicators.velocitySpike {
                Label(
                    UiMessages.string(.nativeSwiftModerationReportsVoteSpike, locale: nativeUiLocale),
                    systemImage: "chart.line.uptrend.xyaxis"
                )
            }
        }
        .font(Typography.caption)
    }

    private func reportCountText(_ count: Int) -> String {
        localizedCount(.nativeSwiftModerationReportsReportCount, count: count)
    }

    private func reporterCountText(_ count: Int) -> String {
        localizedCount(.nativeSwiftModerationReportsReporterCount, count: count)
    }

    private func localizedCount(_ key: UiMessageKey, count: Int) -> String {
        UiMessages.string(
            UiMessage(
                key,
                numberParameters: ["count": Double(count)]
            ),
            locale: nativeUiLocale
        )
    }
}
