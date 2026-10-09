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
        guard let ownerId = identity?.id else { return }
        let invalidationGeneration = apiKeyRotationOwnerState.invalidationGeneration
        let secretOperationGeneration = beginApiKeySecretOperation()
        apiKeyCreationInFlight = true
        defer { apiKeyCreationInFlight = false }
        let permissions = apiKeyScopeSelection.permissions
        state = .loading
        statusMessage = nil
        do {
            let lifetime: ApiKeyLifetimeChoice = apiKeyLifetimeDays.map { .days($0) } ?? .unlimited
            let response: SettingsApiKeyResponse = try await client.send(
                .createMyApiKey(label: apiKeyLabel, type: apiKeyType, permissions: permissions, lifetime: lifetime)
            )
            guard invalidationGeneration == apiKeyRotationOwnerState.invalidationGeneration,
                  secretOperationGeneration == apiKeyRotationOwnerState.secretOperationGeneration else { return }
            guard identity?.id == ownerId else {
                if identity == nil, apiKeyRotationOwnerState.lastConfirmedOwnerId == ownerId {
                    apiKeyRotationOwnerState.pendingCreation = PendingApiKeyCreation(
                        ownerId: ownerId, response: response, operationGeneration: secretOperationGeneration
                    )
                }
                return
            }
            reconcileCreatedApiKey(
                response,
                preserveDuringMainLoad: activeMainSettingsLoadGeneration == settingsLoadGeneration
            )
            state = .loaded
            if activeMainSettingsLoadGeneration == settingsLoadGeneration { state = .loading }
        } catch {
            handleApiKeyCreationFailure(
                error,
                ownerId: ownerId,
                invalidationGeneration: invalidationGeneration,
                secretOperationGeneration: secretOperationGeneration
            )
        }
    }

    func dismissRawApiKey() {
        _ = beginApiKeySecretOperation()
    }

    func rotateApiKey(id: String) async {
        guard apiKeyRotationOwnerState.identityConfirmed,
              let client, let ownerId = identity?.id, let key = apiKeys.first(where: { $0.id == id }),
              canRotateApiKey(key) else { return }
        let secretOperationGeneration = beginApiKeySecretOperation()
        let context = ApiKeyRotationContext(
            ownerId: ownerId,
            loadGeneration: settingsLoadGeneration,
            invalidationGeneration: apiKeyRotationOwnerState.invalidationGeneration,
            secretOperationGeneration: secretOperationGeneration
        )
        apiKeyRotationInFlight.insert(id)
        statusMessage = nil
        defer {
            if context.invalidationGeneration == apiKeyRotationOwnerState.invalidationGeneration {
                apiKeyRotationInFlight.remove(id)
            }
        }
        do {
            let response: SettingsApiKeyResponse = try await client.send(.rotateMyApiKey(id: id))
            guard publishOrHoldApiKeyRotationSecret(
                response.rawKey,
                ownerId: context.ownerId,
                invalidationGeneration: context.invalidationGeneration,
                secretOperationGeneration: context.secretOperationGeneration
            ) else { return }
            statusMessage = .message(.nativeApiKeysRotated)
            await refreshApiKeysAfterRotation(
                response,
                originalId: id,
                context: context,
                client: client
            )
        } catch {
            await recoverApiKeysAfterRotationFailure(error, context: context)
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
        let invalidationGeneration = apiKeyRotationOwnerState.invalidationGeneration
        guard deleteConfirmation.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() == "delete my account"
        else {
            statusMessage = .message(.nativeSwiftSettingsTypeDeleteToConfirm)
            return
        }

        await mutate {
            let response: DeleteAccountResponse = try await client.send(.deleteUser(idOrSlug: userIdOrSlug))
            guard invalidationGeneration == apiKeyRotationOwnerState.invalidationGeneration else { return }
            statusMessage = .message(
                response.logout
                    ? .nativeSwiftSettingsAccountDeleted
                    : .nativeSwiftSettingsAccountDeletionRequested
            )
            if response.logout {
                invalidateApiKeyRotationOwner(ifGenerationMatches: invalidationGeneration)
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
