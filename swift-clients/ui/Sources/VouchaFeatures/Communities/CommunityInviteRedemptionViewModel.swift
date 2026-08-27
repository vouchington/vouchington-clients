import Observation
import VouchaAPI
import VouchaLocalization

@Observable
@MainActor
final class CommunityInviteRedemptionViewModel {
    private let client: APIClient?
    let code: String
    private(set) var state: CommunitySurfaceState = .idle
    private(set) var statusMessage: UiMessage?

    init(client: APIClient?, code: String) {
        self.client = client
        self.code = code
    }

    func redeem() async {
        guard let client else { return }
        state = .loading
        do {
            let _: EmptyResponse = try await client.send(.redeemCommunityInviteCode(code: code))
            statusMessage = UiMessage(.nativeSwiftCommunityStatusInviteRedeemed)
            state = .loaded
        } catch {
            state = .error(UiMessage(.nativeSwiftCommunityStatusUnableToRedeemInvite))
        }
    }
}
