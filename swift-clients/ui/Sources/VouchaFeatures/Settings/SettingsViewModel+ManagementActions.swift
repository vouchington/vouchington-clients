import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

public extension SettingsViewModel {
    func createApiKey() async {
        guard let client else { return }
        guard canCreateApiKey else {
            statusMessage = .message(.nativeCredentialsInvalidSelection)
            return
        }
        apiKeyCreationInFlight = true
        defer { apiKeyCreationInFlight = false }
        let permissions = apiKeyScopeSelection.permissions
        await mutate {
            let lifetime: ApiKeyLifetimeChoice = apiKeyLifetimeDays.map { .days($0) } ?? .unlimited
            let response: SettingsApiKeyResponse = try await client.send(
                .createMyApiKey(label: apiKeyLabel, type: apiKeyType, permissions: permissions, lifetime: lifetime)
            )
            if activeMainSettingsLoadGeneration == settingsLoadGeneration {
                createdApiKeysDuringMainLoad.append(response.apiKey)
            }
            revokedApiKeyIds.remove(response.apiKey.id)
            apiKeyPagination.invalidateRequestsPreservingPage()
            apiKeyPagination.replaceItems([response.apiKey] + apiKeyPagination.items)
            latestRawAPIKey = response.rawKey
            apiKeyLabel = ""
            apiKeyScopeSelection.clear()
            statusMessage = .message(.nativeSwiftSettingsApiKeyCreated)
        }
        if activeMainSettingsLoadGeneration == settingsLoadGeneration, case .loaded = state {
            state = .loading
        }
    }

    func dismissRawApiKey() {
        latestRawAPIKey = nil
    }

    func rotateApiKey(id: String) async {
        guard let client, let key = apiKeys.first(where: { $0.id == id }),
              canRotateApiKey(key) else { return }
        let generation = settingsLoadGeneration
        let ownerId = identity?.id
        apiKeyRotationInFlight.insert(id)
        latestRawAPIKey = nil
        statusMessage = nil
        defer {
            if generation == settingsLoadGeneration, ownerId == identity?.id {
                apiKeyRotationInFlight.remove(id)
            }
        }
        do {
            let response: SettingsApiKeyResponse = try await client.send(.rotateMyApiKey(id: id))
            guard generation == settingsLoadGeneration, ownerId == identity?.id else { return }
            latestRawAPIKey = response.rawKey
            statusMessage = .message(.nativeApiKeysRotated)
            if let page: SettingsListResponse<ApiKey> = try? await client.send(.myApiKeys()) {
                guard generation == settingsLoadGeneration, ownerId == identity?.id else { return }
                replaceApiKeyPage(page)
            } else {
                guard generation == settingsLoadGeneration, ownerId == identity?.id else { return }
                apiKeyPagination.invalidateRequestsPreservingPage()
                apiKeyPagination.replaceItems(
                    [response.apiKey] + apiKeyPagination.items.filter { $0.id != id && $0.id != response.apiKey.id }
                )
            }
        } catch {
            guard generation == settingsLoadGeneration, ownerId == identity?.id else { return }
            statusMessage = apiKeyRotationFailureMessage(error)
            if let page: SettingsListResponse<ApiKey> = try? await client.send(.myApiKeys()) {
                guard generation == settingsLoadGeneration, ownerId == identity?.id else { return }
                replaceApiKeyPage(page)
            }
        }
    }

    private func apiKeyRotationFailureMessage(_ error: Error) -> UiVerbatimText {
        guard let apiError = error as? VouchaError else { return .verbatim(error.localizedDescription) }
        switch apiError {
        case .notFound, .api(statusCode: 404, _), .apiMessage(statusCode: 404, _, _):
            return .message(.nativeApiKeysRotationNotFound)
        case .api(statusCode: 409, _), .apiMessage(statusCode: 409, _, _):
            return .message(.nativeApiKeysRotationConflict)
        default: return .verbatim(apiError.localizedDescription)
        }
    }

    func revokeApiKey(id: String) async {
        guard let client else { return }
        await mutate {
            let _: EmptyResponse = try await client.send(.revokeMyApiKey(id: id))
            reconcileRevokedApiKey(id: id)
            statusMessage = .message(.nativeSwiftSettingsApiKeyRevoked)
        }
        if activeMainSettingsLoadGeneration == settingsLoadGeneration, case .loaded = state {
            state = .loading
        }
    }

    func requestDataExport() async {
        guard let client, let userIdOrSlug else { return }
        await mutate {
            let response: UserDataRequest = try await client.send(.createUserDataRequest(idOrSlug: userIdOrSlug))
            dataRequest = response
            statusMessage = .message(.nativeSwiftSettingsDataExportRequested)
        }
    }

    func deleteAccount() async {
        guard let client, let userIdOrSlug else { return }
        guard deleteConfirmation.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() == "delete my account"
        else {
            statusMessage = .message(.nativeSwiftSettingsTypeDeleteToConfirm)
            return
        }

        await mutate {
            let response: DeleteAccountResponse = try await client.send(.deleteUser(idOrSlug: userIdOrSlug))
            statusMessage = .message(
                response.logout
                    ? .nativeSwiftSettingsAccountDeleted
                    : .nativeSwiftSettingsAccountDeletionRequested
            )
            if response.logout {
                onLogoutRequired()
            }
        }
    }

    func purchaseMembership(priceId: String) async {
        guard let client else { return }
        await mutate {
            let response: MembershipCheckoutSessionResponse = try await client.send(
                .membershipCheckout(
                    priceId: priceId,
                    successUrl: "/my/membership?checkout=success",
                    cancelUrl: "/my/membership?checkout=cancel"
                )
            )
            membershipCheckoutURL = response.checkoutSession.url
            statusMessage = .message(.nativeSwiftSettingsCheckoutSessionCreated)
        }
    }

    func openMembershipPortal() async {
        guard let client else { return }
        await mutate {
            let response: MembershipPortalSessionResponse = try await client.send(
                .membershipPortal(returnUrl: "/my/membership")
            )
            membershipPortalURL = response.portalSession.url
            statusMessage = .message(.nativeSwiftSettingsBillingPortalCreated)
        }
    }

    func cancelMembership() async {
        guard let client else { return }
        await mutate {
            let _: EmptyResponse = try await client.send(.membershipCancel)
            try await reloadMembership()
            NotificationCenter.default.post(name: .vouchaMembershipEntitlementDidChange, object: nil)
            statusMessage = .message(.nativeSwiftMembershipMembershipCancelled)
        }
    }

    func revokePushSubscription(id: String) async {
        guard let client else { return }
        await mutate {
            let _: EmptyResponse = try await client.send(.revokeMyPushSubscription(id: id))
            reconcileRevokedPushSubscription(id: id)
            statusMessage = .message(.nativeSwiftSettingsPushSubscriptionRevoked)
        }
    }
}
