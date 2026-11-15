import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativeChatCopyrightNoticesFactView: View {
    let notices: [NativeChatCopyrightNoticeFact]
    let locale: Locale

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(.nativeMcpFactsCopyrightNoticesSearchTitle, locale: locale))
                .foregroundStyle(Colors.secondaryLabel)
            if notices.isEmpty {
                Text(UiMessages.string(.nativeSwiftRouteSurfaceNoResults, locale: locale))
            }
            ForEach(notices) { notice in
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    if let claimant = notice.claimant {
                        row(.nativeCopyrightNoticesClaimant, .userContent(claimant))
                    }
                    row(
                        .nativeMcpFactsCopyrightNoticesJurisdiction,
                        .protocolValue(notice.jurisdiction == .euDSA ? "DSA" : "DMCA")
                    )
                    Text(UiMessages.string(.message(
                        .nativeCopyrightNoticesReceivedDate,
                        dateParameters: ["date": .init(notice.receivedAt, dateStyle: .abbreviated)]
                    ), locale: locale))
                    if let work = notice.workDescription {
                        row(.nativeMcpFactsCopyrightNoticesWorkDescription, .userContent(work))
                    }
                }
            }
        }
        .accessibilityIdentifier("native-chat-copyright-notices-fact")
    }

    private func row(_ key: UiMessageKey, _ value: UiVerbatimText) -> some View {
        NativeChatFactRow(label: key, value: value, locale: locale)
    }
}

struct NativeChatRepeatInfringersFactView: View {
    let accounts: [NativeChatRepeatInfringerFact]
    let locale: Locale

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(.nativeMcpFactsRepeatInfringersTitle, locale: locale))
                .foregroundStyle(Colors.secondaryLabel)
            if accounts.isEmpty {
                Text(UiMessages.string(.nativeSwiftRouteSurfaceNoResults, locale: locale))
            }
            ForEach(accounts) { account in
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    if let username = account.username {
                        row(.nativeMcpFactsRepeatInfringersAccountUsername, .userContent(username))
                    }
                    row(.nativeMcpFactsRepeatInfringersOperative, yesNo(account.isOperative))
                    row(.nativeMcpFactsRepeatInfringersTerminationInEffect, yesNo(account.terminationInEffect))
                    row(.nativeMcpFactsRepeatInfringersOpenReview, yesNo(account.hasOpenReview))
                    row(
                        account.incidentCount == 1
                            ? .nativeMcpFactsRepeatInfringersIncident : .nativeMcpFactsRepeatInfringersIncidents,
                        .message(
                            .nativeSwiftRouteSurfaceNumberValue,
                            numberParameters: ["value": Double(account.incidentCount)]
                        )
                    )
                }
            }
        }
        .accessibilityIdentifier("native-chat-repeat-infringers-fact")
    }

    private func row(_ key: UiMessageKey, _ value: UiVerbatimText) -> some View {
        NativeChatFactRow(label: key, value: value, locale: locale)
    }

    private func yesNo(_ value: Bool) -> UiVerbatimText {
        .message(value ? .nativeSwiftRouteSurfaceTrue : .nativeSwiftRouteSurfaceFalse)
    }
}
