import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

public extension SettingsViewModel {
    var isLoading: Bool {
        if case .loading = state {
            return true
        }
        return false
    }

    var userIdOrSlug: String? {
        identity?.id
    }

    func load() async {
        await loadLocalLLMSettings()
        guard let client else { return }
        let generation = beginSettingsLoad()
        state = .loading
        statusMessage = nil

        do {
            let identityResponse: SettingsIdentityResponse = try await client.send(.myIdentity)
            guard isCurrentSettingsLoad(generation) else { return }
            apply(identity: identityResponse.identity)
            await loadCredentialSettings()
            guard isCurrentSettingsLoad(generation) else { return }
            let profileResponse: SettingsProfileResponse = try await client.send(.myProfile)
            guard isCurrentSettingsLoad(generation) else { return }

            async let linksResponse: SettingsListResponse<VouchaModels.ProfileLink> = client.send(.myProfileLinks)
            async let apiKeysResponse: SettingsListResponse<ApiKey> = client.send(.myApiKeys())
            async let pushSubscriptionsResponse: SettingsListResponse<WebPushSubscription> = client
                .send(.myPushSubscriptions())
            async let sessionsResponse: Page<AuthSession> = client.send(.authSessions())
            async let membershipResponse: MembershipResponse? = loadOptional(.membershipMe)
            async let plansResponse: MembershipPlansResponse? = loadOptional(.membershipPlans)
            async let dataRequestResponse: UserDataRequest? = loadDataRequest(userIdOrSlug: identityResponse.identity
                .id)

            let links = try await linksResponse
            let loadedApiKeys = try await apiKeysResponse
            let loadedPushSubscriptions = try await pushSubscriptionsResponse
            let loadedSessions = try await sessionsResponse
            let loadedMembership = try await membershipResponse
            let loadedPlans = try await plansResponse
            let loadedDataRequest = try await dataRequestResponse
            guard isCurrentSettingsLoad(generation) else { return }

            profileMarkdown = profileResponse.profile.markdown
            profileLinks = links.results
            replaceApiKeyPage(loadedApiKeys)
            replacePushSubscriptionPage(loadedPushSubscriptions)
            replaceSessionPage(loadedSessions)
            membership = loadedMembership?.membership
            membershipPlans = loadedPlans?.plans ?? [:]
            membershipBenefitCatalog = loadedPlans?.benefitCatalog
            dataRequest = loadedDataRequest
            state = .loaded
        } catch {
            guard isCurrentSettingsLoad(generation) else { return }
            if error is CancellationError || Task.isCancelled {
                state = identity == nil ? .idle : .loaded
                return
            }
            state = .error((error as? VouchaError) ?? .unexpected(error.localizedDescription))
        }
    }

    func reload() async {
        await load()
    }

    internal func resetProfileLinkDraft() {
        profileLinkType = .url
        profileLinkURL = ""
        profileLinkHandle = ""
        profileLinkName = ""
        profileLinkImageId = ""
    }

    internal func reloadMembership() async throws {
        let response: MembershipResponse? = try await loadOptional(.membershipMe)
        membership = response?.membership
    }

    private func loadOptional<T: Decodable & Sendable>(_ endpoint: Endpoint) async throws -> T? {
        guard let client else { return nil }
        do {
            return try await client.send(endpoint)
        } catch VouchaError.notFound {
            return nil
        }
    }

    internal func loadDataRequest(userIdOrSlug: String) async throws -> UserDataRequest? {
        guard let client else { return nil }
        do {
            return try await client.send(.userDataRequest(idOrSlug: userIdOrSlug))
        } catch VouchaError.notFound {
            return nil
        }
    }
}
