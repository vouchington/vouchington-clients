import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

public extension SettingsViewModel {
    func createApiKey() async {
        guard let client else { return }
        await mutate {
            let permissions = apiKeyType == .mcp ? ["mcp-tools:read", "mcp-tools:write"] : ["rss-feeds:read"]
            let response: SettingsApiKeyResponse = try await client.send(
                .createMyApiKey(label: apiKeyLabel, type: apiKeyType, permissions: permissions)
            )
            settingsLoadGeneration += 1
            revokedApiKeyIds.remove(response.apiKey.id)
            apiKeyPagination.invalidateRequestsPreservingPage()
            apiKeyPagination.replaceItems([response.apiKey] + apiKeyPagination.items)
            latestRawAPIKey = response.rawKey
            apiKeyLabel = ""
            statusMessage = .message(.nativeSwiftSettingsApiKeyCreated)
        }
    }

    func revokeApiKey(id: String) async {
        guard let client else { return }
        await mutate {
            let _: EmptyResponse = try await client.send(.revokeMyApiKey(id: id))
            reconcileRevokedApiKey(id: id)
            statusMessage = .message(.nativeSwiftSettingsApiKeyRevoked)
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
