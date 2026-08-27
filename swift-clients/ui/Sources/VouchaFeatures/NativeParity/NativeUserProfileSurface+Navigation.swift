import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension NativeUserProfileSurface {
    @ViewBuilder
    func tabButton(_ title: UiMessageKey, path: String, selected: Bool) -> some View {
        let localizedTitle = UiMessages.string(title, locale: nativeUiLocale)
        let accessibilityIdentifier = UiMessages.string(title, locale: .english).lowercased()
        if selected {
            Button(localizedTitle) { onNavigate(path) }
                .buttonStyle(.borderedProminent)
                .accessibilityIdentifier("public-profile-tab-\(accessibilityIdentifier)")
        } else {
            Button(localizedTitle) { onNavigate(path) }
                .buttonStyle(.bordered)
                .accessibilityIdentifier("public-profile-tab-\(accessibilityIdentifier)")
        }
    }

    var paginationControls: some View {
        HybridPaginationControl(
            hasMore: viewModel.userProfile.pagination.hasLoadedPage && viewModel.userProfile.pagination.hasMore,
            isLoading: viewModel.userProfile.isLoadingMore,
            hasError: viewModel.userProfile.appendError != nil,
            accessibilityIdentifier: "public-profile-pagination"
        ) { await viewModel.loadMoreUserProfileRows() }
    }

    func localizedCount(_ key: UiMessageKey, _ count: Int) -> Text {
        Text(verbatim: UiMessages.string(
            UiMessage(key, numberParameters: ["count": Double(count)]),
            locale: nativeUiLocale
        ))
    }

    var currentScope: NativeUserProfileScope? {
        viewModel.userProfile.scope
    }

    var username: String {
        viewModel.userProfile.header?.user.username ?? ""
    }

    var basePath: String {
        "/user/\(username)"
    }
}
