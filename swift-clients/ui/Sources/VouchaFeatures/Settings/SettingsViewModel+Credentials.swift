import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

public extension SettingsViewModel {
    var oauthGrants: [OAuthGrant] {
        oauthGrantPagination.items
    }

    var apiKeyScopes: [CredentialScope] {
        apiKeyScopeSelection.availableScopes
    }

    var hasNoOAuthGrants: Bool {
        guard case .loaded = oauthGrantState else { return false }
        return oauthGrants.isEmpty && !oauthGrantPagination.hasMore
    }

    var canCreateApiKey: Bool {
        guard case .loaded = credentialState else { return false }
        return apiKeyScopeSelection.isValid && !apiKeyLabel.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty &&
            !apiKeyCreationInFlight && (!isApiKeyAdministrator || apiKeyLifetimeDays == 30 || apiKeyLifetimeDays == 90)
    }

    var isApiKeyAdministrator: Bool {
        identity?.roles.contains("administrator") == true
    }

    func setApiKeyLifetimeDays(_ days: Int?) {
        if let days, ![30, 90, 365].contains(days) { return }
        guard !isApiKeyAdministrator || days == 30 || days == 90 else { return }
        apiKeyLifetimeDays = days
    }

    func apiKeyStatus(_ key: ApiKey, now: Date = Date()) -> UiMessageKey {
        if key.revokedAt != nil { return .nativeApiKeysRevoked }
        if key.replacedByApiKeyId != nil { return .nativeApiKeysReplaced }
        if let expiry = key.expiresAt, expiry <= now { return .nativeApiKeysExpired }
        if isInvalidAdministratorApiKey(key) { return .nativeApiKeysAdministratorInvalid }
        return .nativeApiKeysActive
    }

    func canRotateApiKey(_ key: ApiKey, now: Date = Date()) -> Bool {
        key.revokedAt == nil && key.replacedByApiKeyId == nil &&
            key.expiresAt.map { $0 > now } != false && !apiKeyRotationInFlight.contains(key.id)
    }

    func isInvalidAdministratorApiKey(_ key: ApiKey) -> Bool {
        guard isApiKeyAdministrator else { return false }
        guard let expiry = key.expiresAt else { return true }
        return expiry.timeIntervalSince(key.createdAt) > 90 * 86_400
    }

    func setApiKeyScope(_ scope: String, selected: Bool) {
        apiKeyScopeSelection.setSelected(scope, selected: selected)
    }

    func loadMoreOAuthGrants() async {
        guard let client, let request = oauthGrantPagination.beginNextPage() else { return }
        do {
            let page: Page<OAuthGrant> = try await client.send(.myOAuthGrants(after: request.cursor))
            oauthGrantPagination.complete(
                request,
                items: page.results.filter { !revokedOAuthGrantIds.contains($0.id) },
                endCursor: page.pageInfo.endCursor,
                hasNextPage: page.pageInfo.hasNextPage
            )
        } catch {
            if error is CancellationError || Task.isCancelled {
                oauthGrantPagination.cancel(request)
            } else {
                oauthGrantPagination.fail(
                    request,
                    error: error as? VouchaError ?? .unexpected(error.localizedDescription)
                )
            }
        }
    }

    func revokeOAuthGrant(id: String) async {
        guard let client, oauthGrants.contains(where: { $0.id == id }), !isLoading else { return }
        await mutate {
            do {
                let _: EmptyResponse = try await client.send(.revokeMyOAuthGrant(id: id))
            } catch VouchaError.notFound {}
            reconcileRevokedOAuthGrant(id: id)
            statusMessage = .message(.nativeCredentialsGrantRevoked)
        }
    }
}

extension SettingsViewModel {
    func applyCredentialScopeCatalog(_ catalog: ScopeCatalogResponse) throws {
        apiKeyScopeSelection.configure(type: apiKeyType)
        apiKeyScopeSelection.replaceCatalog(catalog.scopes)
        guard !apiKeyScopeSelection.availableScopes.isEmpty else {
            throw VouchaError.unexpected(localized(.nativeCredentialsCatalogLoadFailed))
        }
    }

    func replaceOAuthGrantPage(_ page: Page<OAuthGrant>) {
        oauthGrantPagination.reset(items: page.results.filter { !revokedOAuthGrantIds.contains($0.id) })
        oauthGrantPagination.restoreContinuation(endCursor: page.pageInfo.endCursor, hasMore: page.pageInfo.hasNextPage)
    }

    func reconcileRevokedOAuthGrant(id: String) {
        oauthGrantLoadGeneration += 1
        revokedOAuthGrantIds.insert(id)
        oauthGrantPagination.invalidateRequestsPreservingPage()
        oauthGrantPagination.remove { $0.id == id }
        if case .loading = oauthGrantState { oauthGrantState = .loaded }
    }
}
