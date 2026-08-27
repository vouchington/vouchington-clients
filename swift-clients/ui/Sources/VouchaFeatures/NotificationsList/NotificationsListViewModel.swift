import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaModels

// MARK: - Response model

private struct NotificationsPage: Decodable {
    struct ResultItem: Decodable {
        let id: String
    }

    struct PageInfo: Decodable {
        let hasNextPage: Bool
        let endCursor: String?
    }

    let results: [ResultItem]
    let pageInfo: PageInfo
    let notifications: [String: VouchaNotification]
    let communities: [String: NotificationCommunity]?
}

private struct NotificationRedirectTargetResponse: Decodable {
    let targetUrl: String
}

@Observable
@MainActor
public final class NotificationsListViewModel {
    private var pagination = CursorPaginationState<VouchaNotification>()
    public var items: [VouchaNotification] {
        pagination.items
    }

    public var state: LoadState {
        pagination.state
    }

    public var hasMore: Bool {
        pagination.hasMore
    }

    /// Locally-read notification IDs — optimistic overlay on top of server `readAt`.
    public private(set) var locallyReadIds: Set<String> = []

    public var isLoading: Bool {
        pagination.isLoading
    }

    public var canAutomaticallyLoad: Bool {
        pagination.canAutomaticallyLoad
    }

    public var hasPaginationError: Bool {
        pagination.lastError != nil
    }

    /// Returns `true` when the notification is read (either server-side or locally).
    public func isRead(_ notification: VouchaNotification) -> Bool {
        notification.readAt != nil || locallyReadIds.contains(notification.id)
    }

    private var communities: [String: NotificationCommunity] = [:]

    private let client: APIClient

    public init(client: APIClient) {
        self.client = client
    }

    /// Load the first page if idle and there is more to load.
    public func load() async {
        guard let request = pagination.beginInitialPageIfNeeded() else { return }
        await loadPage(request)
    }

    /// Fetch the next page and append results.
    public func loadNextPage() async {
        guard let request = pagination.beginNextPage() else { return }
        await loadPage(request)
    }

    private func loadPage(_ request: CursorPageRequest) async {
        do {
            let endpoint = Endpoint.notifications(after: request.cursor)
            let page: NotificationsPage = try await client.send(endpoint)
            guard pagination.isCurrent(request) else { return }
            let newItems = page.results.compactMap { result in
                page.notifications[result.id]
            }
            communities.merge(page.communities ?? [:]) { _, newer in newer }
            pagination.complete(
                request,
                items: newItems,
                endCursor: page.pageInfo.endCursor,
                hasNextPage: page.pageInfo.hasNextPage
            )
        } catch let error as VouchaError {
            if Task.isCancelled {
                pagination.cancel(request)
            } else {
                pagination.fail(request, error: error)
            }
        } catch {
            if Task.isCancelled {
                pagination.cancel(request)
            } else {
                pagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
            }
        }
    }

    /// Reset all pagination state.
    public func reset() {
        pagination.reset()
        locallyReadIds = []
        communities = [:]
    }

    /// Clear and reload from the first page.
    public func reload() async {
        reset()
        await loadNextPage()
    }

    /// Mark a single notification as read optimistically.
    public func markRead(notificationId: String) async {
        locallyReadIds.insert(notificationId)
        do {
            let _: EmptyResponse = try await client.send(.markNotificationRead(notificationId: notificationId))
        } catch {
            locallyReadIds.remove(notificationId)
        }
    }

    /// Mark a notification as read, then return its trimmed target path when navigation should proceed.
    public func activate(notification: VouchaNotification) async -> String? {
        if !isRead(notification) {
            await markRead(notificationId: notification.id)
        }
        guard isRead(notification) else { return nil }
        let structuredTargetPath: String? = if notification.targetEntity?.entityType == "community" {
            if let communityId = notification.targetEntity?.id, let community = communities[communityId] {
                "/communities/\(community.slug)"
            } else {
                "/my/notifications"
            }
        } else if notification.targetIntent == .notificationsInbox {
            "/my/notifications"
        } else {
            notification.targetPath
        }
        guard let targetPath = structuredTargetPath?.trimmingCharacters(in: .whitespacesAndNewlines),
              !targetPath.isEmpty
        else {
            return nil
        }
        return await resolveActivationTargetPath(targetPath)
    }

    private func resolveActivationTargetPath(_ targetPath: String) async -> String? {
        guard let redirectNotificationId = notificationRedirectId(from: targetPath) else {
            return targetPath
        }

        do {
            let response: NotificationRedirectTargetResponse = try await client.send(
                .notificationRedirectTarget(notificationId: redirectNotificationId)
            )
            let resolvedTargetPath = response.targetUrl.trimmingCharacters(in: .whitespacesAndNewlines)
            return resolvedTargetPath.isEmpty ? nil : resolvedTargetPath
        } catch {
            return nil
        }
    }

    private func notificationRedirectId(from targetPath: String) -> String? {
        let normalizedPath = targetPath.hasPrefix("/") ? targetPath : "/\(targetPath)"
        guard let components = URLComponents(string: "https://native.voucha.local\(normalizedPath)"),
              components.path == "/notification-redirect"
        else {
            return nil
        }
        return components.queryItems?.first { $0.name == "notification_id" }?.value
    }

    /// Mark all loaded notifications as read optimistically.
    public func markAllRead() async {
        let newlyRead = Set(items.map(\.id)).subtracting(locallyReadIds)
        locallyReadIds.formUnion(newlyRead)
        do {
            let _: EmptyResponse = try await client.send(.markAllNotificationsRead)
        } catch {
            locallyReadIds.subtract(newlyRead)
        }
    }
}
