import VouchaAPI
import VouchaCore
import VouchaModels

extension SettingsViewModel {
    func loadMoreApiKeys() async {
        guard let client, let request = apiKeyPagination.beginNextPage() else { return }
        do {
            let page: SettingsListResponse<ApiKey> = try await client.send(
                .myApiKeys(after: request.cursor)
            )
            apiKeyPagination.complete(
                request,
                items: page.results.filter { !revokedApiKeyIds.contains($0.id) },
                endCursor: page.pageInfo.endCursor,
                hasNextPage: page.pageInfo.hasNextPage
            )
        } catch is CancellationError {
            apiKeyPagination.cancel(request)
        } catch {
            if Task.isCancelled {
                apiKeyPagination.cancel(request)
                return
            }
            apiKeyPagination.fail(request, error: paginationError(error))
        }
    }

    func loadMorePushSubscriptions() async {
        guard let client, let request = pushSubscriptionPagination.beginNextPage() else { return }
        do {
            let page: SettingsListResponse<WebPushSubscription> = try await client.send(
                .myPushSubscriptions(after: request.cursor)
            )
            pushSubscriptionPagination.complete(
                request,
                items: page.results.filter { !revokedPushSubscriptionIds.contains($0.id) },
                endCursor: page.pageInfo.endCursor,
                hasNextPage: page.pageInfo.hasNextPage
            )
        } catch is CancellationError {
            pushSubscriptionPagination.cancel(request)
        } catch {
            if Task.isCancelled {
                pushSubscriptionPagination.cancel(request)
                return
            }
            pushSubscriptionPagination.fail(request, error: paginationError(error))
        }
    }

    func loadMoreSessions() async {
        guard let client, let request = sessionPagination.beginNextPage() else { return }
        do {
            let page: Page<AuthSession> = try await client.send(.authSessions(after: request.cursor))
            sessionPagination.complete(
                request,
                items: page.results.filter { !revokedAllSessions && !revokedSessionIds.contains($0.id) },
                endCursor: page.pageInfo.endCursor,
                hasNextPage: page.pageInfo.hasNextPage
            )
        } catch is CancellationError {
            sessionPagination.cancel(request)
        } catch {
            if Task.isCancelled {
                sessionPagination.cancel(request)
                return
            }
            sessionPagination.fail(request, error: paginationError(error))
        }
    }

    func replaceApiKeyPage(_ page: SettingsListResponse<ApiKey>) {
        apiKeyPagination.reset(items: page.results.filter { !revokedApiKeyIds.contains($0.id) })
        apiKeyPagination.restoreContinuation(endCursor: page.pageInfo.endCursor, hasMore: page.pageInfo.hasNextPage)
    }

    func replacePushSubscriptionPage(_ page: SettingsListResponse<WebPushSubscription>) {
        pushSubscriptionPagination.reset(items: page.results.filter { !revokedPushSubscriptionIds.contains($0.id) })
        pushSubscriptionPagination.restoreContinuation(
            endCursor: page.pageInfo.endCursor,
            hasMore: page.pageInfo.hasNextPage
        )
    }

    func replaceSessionPage(_ page: Page<AuthSession>) {
        sessionPagination.reset(items: page.results.filter {
            !revokedAllSessions && !revokedSessionIds.contains($0.id)
        })
        sessionPagination.restoreContinuation(endCursor: page.pageInfo.endCursor, hasMore: page.pageInfo.hasNextPage)
    }

    private func paginationError(_ error: Error) -> VouchaError {
        error as? VouchaError ?? .unexpected(error.localizedDescription)
    }

    func beginSettingsLoad() -> Int {
        settingsLoadGeneration += 1
        credentialLoadGeneration += 1
        identity = nil
        apiKeyScopeSelection = ApiKeyScopeSelection(type: apiKeyType)
        credentialState = .idle
        oauthGrantState = .idle
        oauthGrantPagination.reset()
        apiKeyPagination.invalidateRequestsPreservingPage()
        pushSubscriptionPagination.invalidateRequestsPreservingPage()
        sessionPagination.invalidateRequestsPreservingPage()
        return settingsLoadGeneration
    }

    func isCurrentSettingsLoad(_ generation: Int) -> Bool {
        generation == settingsLoadGeneration
    }

    func reconcileRevokedApiKey(id: String) {
        settingsLoadGeneration += 1
        revokedApiKeyIds.insert(id)
        apiKeyPagination.invalidateRequestsPreservingPage()
        apiKeyPagination.remove { $0.id == id }
    }

    func reconcileRevokedPushSubscription(id: String) {
        settingsLoadGeneration += 1
        revokedPushSubscriptionIds.insert(id)
        pushSubscriptionPagination.invalidateRequestsPreservingPage()
        pushSubscriptionPagination.remove { $0.id == id }
    }

    func reconcileRevokedSession(id: String) {
        settingsLoadGeneration += 1
        revokedSessionIds.insert(id)
        sessionPagination.invalidateRequestsPreservingPage()
        sessionPagination.remove { $0.id == id }
    }
}
