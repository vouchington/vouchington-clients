import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func loadUsersBrowsePage(client: APIClient, after: String?) async throws -> NativeForwardPage {
        guard let query = routeMatch?.queryValue("q"), !query.isEmpty else {
            return NativeForwardPage(rows: [], pageInfo: nil as Page<UserSearchResult>.PageInfo?)
        }
        var queryItems = [URLQueryItem(name: "q", value: query), URLQueryItem(name: "limit", value: "25")]
        if let after {
            queryItems.append(URLQueryItem(name: "after", value: after))
        }
        let response: Page<UserSearchResult> = try await client.send(
            Endpoint(.GET, path: "/api/v1/users", queryItems: queryItems)
        )
        let rows = response.results.enumerated().map { index, user in
            usersBrowseRow(user, index: index)
        }
        return NativeForwardPage(rows: rows, pageInfo: response.pageInfo)
    }

    private func usersBrowseRow(_ user: UserSearchResult, index: Int) -> NativeForwardRow {
        let title = UiVerbatimText.verbatim(
            user.username?.ifNotEmpty ?? user.id.ifNotEmpty ?? String(index + 1)
        )
        let detail = usersBrowseDetail(for: user)
        guard isAdministrator else {
            return forwardRow(id: user.id, icon: "person", title: title, detail: detail)
        }
        let target = user.username?.ifNotEmpty ?? user.id
        guard !target.isEmpty else {
            return forwardRow(id: user.id, icon: "person", title: title, detail: detail)
        }
        return forwardRow(
            id: user.id,
            icon: "person.badge.shield.checkmark",
            title: title,
            detail: detail,
            targetPath: NativeUserProfileNavigationTarget.userAdmin(target)
        )
    }

    private func usersBrowseDetail(for user: UserSearchResult) -> UiVerbatimText {
        guard isAdministrator else {
            return .verbatim(user.id)
        }
        let status = appText(
            user.suspendedAt == nil
                ? .nativeSwiftRouteSurfaceUsersBrowseActive
                : .nativeSwiftRouteSurfaceUsersBrowseSuspended
        )
        guard let email = user.emailAddress?.ifNotEmpty else {
            return status
        }
        return .joined([status, .userContent(email)], separator: "\n")
    }
}
