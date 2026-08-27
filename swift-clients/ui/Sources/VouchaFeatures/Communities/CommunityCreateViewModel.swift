import Foundation
import Observation
import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class CommunityCreateViewModel {
    var name = ""
    var slug = ""
    var markdown = ""
    var turnstileToken: String?
    private(set) var createdCommunitySlug: String?
    private(set) var state: CommunitySurfaceState = .idle

    private let client: APIClient?
    private let appAttestationService: AppAttestationService?

    init(client: APIClient?, appAttestationService: AppAttestationService? = nil) {
        self.client = client
        self.appAttestationService = appAttestationService ?? AppAttestationService.makeDefault(client: client)
    }

    var canCreate: Bool {
        !name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            && !isLoading
            && (turnstileToken != nil || appAttestationService?.isSupported == true)
    }

    var isLoading: Bool {
        if case .loading = state {
            return true
        }
        return false
    }

    func create() async {
        guard !isLoading else { return }
        state = .loading
        guard let client else {
            state = .loaded
            return
        }
        if turnstileToken == nil, let endpoint = await attestedCreateEndpoint() {
            await submit(client: client, endpoint: endpoint, fallbackToTurnstile: true)
            return
        }
        guard let turnstileToken else {
            state = .requiredTurnstile
            return
        }
        await submit(client: client, endpoint: makeEndpoint(turnstileToken: turnstileToken), fallbackToTurnstile: false)
    }

    private func attestedCreateEndpoint() async -> Endpoint? {
        guard let appAttestationService, appAttestationService.isSupported else { return nil }
        guard let headers = try? await appAttestationService.assertionHeaders(actionTag: .communitiesCreate) else {
            return nil
        }
        return makeEndpoint(turnstileToken: nil).withHeaders(headers)
    }

    private func submit(client: APIClient, endpoint: Endpoint, fallbackToTurnstile: Bool) async {
        do {
            let response: CommunityResponse = try await client.send(endpoint)
            createdCommunitySlug = response.community.slug
            turnstileToken = nil
            state = .loaded
        } catch let error as VouchaError where fallbackToTurnstile && error.isRecoverableByTurnstileFallback {
            if error.isAttestationKeyRejected {
                appAttestationService?.forgetCachedKey()
            }
            guard let turnstileToken else {
                state = .requiredTurnstile
                return
            }
            await submit(
                client: client,
                endpoint: makeEndpoint(turnstileToken: turnstileToken),
                fallbackToTurnstile: false
            )
        } catch {
            turnstileToken = nil
            state = .error(UiMessage(.nativeSwiftCommunityStatusUnableToCreateCommunity))
        }
    }

    private func makeEndpoint(turnstileToken: String?) -> Endpoint {
        .createCommunity(
            name: name.trimmingCharacters(in: .whitespacesAndNewlines),
            slug: slug.trimmedOrNil,
            markdown: markdown.trimmedOrNil,
            turnstileToken: turnstileToken
        )
    }
}
