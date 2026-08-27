import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct MemberModerationReportCard: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let report: MemberModerationReport
    let onNavigate: (String) -> Void

    var body: some View {
        GroupBox {
            VStack(alignment: .leading, spacing: Spacing.sm) {
                reportHeader(
                    label: report.targetLabel ?? report.entityType,
                    status: report.status,
                    path: report.targetPath,
                    locale: nativeUiLocale,
                    onNavigate: onNavigate
                )
                Label(reportCountText(report.reportCount), systemImage: "flag")
                Text(UiMessages.string(
                    .nativeSwiftModerationReportsReason,
                    parameters: ["reason": report.reason],
                    locale: nativeUiLocale
                ))
                Text(UiMessages.date(
                    report.createdAt,
                    date: .abbreviated,
                    time: .shortened,
                    locale: nativeUiLocale,
                    timeZone: .current
                )).foregroundStyle(.secondary)
                if let context = report.postModerationContext {
                    Text(UiMessages.string(
                        .nativeSwiftModerationReportsModerationContext,
                        parameters: ["context": String(describing: context)],
                        locale: nativeUiLocale
                    ))
                    .font(Typography.caption)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }

    private func reportCountText(_ count: Int) -> String {
        UiMessages.string(
            UiMessage(
                .nativeSwiftModerationReportsReportCount,
                numberParameters: ["count": Double(count)]
            ),
            locale: nativeUiLocale
        )
    }
}
