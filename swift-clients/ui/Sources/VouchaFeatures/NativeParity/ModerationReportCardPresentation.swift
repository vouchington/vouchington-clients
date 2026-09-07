import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension StaffModerationReportCard {
    @ViewBuilder var judgement: some View {
        if let judgement = report.judgement {
            VStack(alignment: .leading) {
                Text(UiMessages.string(
                    .nativeSwiftModerationReportsAiJudgement,
                    parameters: ["action": judgement.recommendedAction],
                    locale: nativeUiLocale
                ))
                Text(judgement.internalResponse)
                if !judgement.publicResponse.isEmpty {
                    Text(judgement.publicResponse)
                }
                if judgement.isStale {
                    Label(
                        UiMessages.string(.nativeSwiftModerationReportsOutdated, locale: nativeUiLocale),
                        systemImage: "clock.badge.exclamationmark"
                    )
                }
            }
        } else {
            Text(UiMessages.string(
                .nativeSwiftModerationReportsAiJudgementUnavailable,
                locale: nativeUiLocale
            )).foregroundStyle(.secondary)
        }
    }

    @ViewBuilder var banEvasion: some View {
        if let context = report.communityBanEvasion {
            VStack(alignment: .leading) {
                Label(
                    UiMessages.string(.nativeSwiftModerationReportsBanEvasionSignal, locale: nativeUiLocale),
                    systemImage: "person.crop.circle.badge.exclamationmark"
                )
                Text(UiMessages.string(
                    .nativeSwiftModerationReportsCommunity,
                    parameters: ["community": context.communitySlug],
                    locale: nativeUiLocale
                ))
                Text(UiMessages.string(
                    .nativeSwiftModerationReportsMatchedAccount,
                    parameters: ["account": context.sourceUsername ?? context.sourceUserId],
                    locale: nativeUiLocale
                ))
                Text(UiMessages.string(
                    .nativeSwiftModerationReportsScore,
                    parameters: ["score": UiMessages.percent(context.score, locale: nativeUiLocale)],
                    locale: nativeUiLocale
                ))
            }
        }
    }

    func reportCountText(_ count: Int) -> String {
        UiMessages.string(UiMessage(
            .nativeSwiftModerationReportsReportCount,
            numberParameters: ["count": Double(count)]
        ), locale: nativeUiLocale)
    }
}
