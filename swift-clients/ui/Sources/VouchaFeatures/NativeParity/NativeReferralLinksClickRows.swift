import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativeReferralClickRows: View {
    @Environment(\.locale)
    var nativeUiLocale
    let viewModel: NativeReferralLinksManagementViewModel

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(UiMessages.string(.nativeSwiftReferralLinksManagementClickAnalytics, locale: nativeUiLocale))
                .font(.headline)
            ForEach(viewModel.clicks) { click in
                NativeReferralClickRow(click: click, user: click.userId.flatMap { viewModel.clickUsers[$0] })
            }
            HybridPaginationControl(
                hasMore: viewModel.hasMoreClicks,
                isLoading: viewModel.clickPagination.isLoading,
                hasError: viewModel.clickPagination.lastError != nil
            ) {
                await viewModel.loadMoreClicks()
            }
        }
    }

    static func timestamp(
        _ click: ReferralClickLogEntry,
        locale: Locale,
        timeZone: TimeZone
    ) -> String {
        UiMessages.date(
            click.createdAt,
            date: .abbreviated,
            time: .shortened,
            locale: locale,
            timeZone: timeZone
        )
    }
}

private struct NativeReferralClickRow: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Environment(\.timeZone)
    var nativeUiTimeZone
    let click: ReferralClickLogEntry
    let user: ReferralClickLogUser?

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(click.landingUrl)
                .font(.body)
            Text(NativeReferralClickRows.timestamp(
                click,
                locale: nativeUiLocale,
                timeZone: nativeUiTimeZone
            ))
            .font(.caption)
            Text(verbatim: UiMessages.string(status, locale: nativeUiLocale))
                .font(.caption)
        }
        .padding(.vertical, Spacing.xs)
    }

    private var status: UiVerbatimText {
        guard click.signedUpAt != nil else {
            return .message(.nativeSwiftPresentationValuesClick)
        }
        guard let username = user?.username else {
            return .message(.nativeSwiftPresentationValuesSignupAnonymous)
        }
        return .message(
            .nativeSwiftPresentationValuesSignupUser,
            textParameters: ["username": .verbatim(username)]
        )
    }
}
