import Foundation
import Observation
@testable import VouchaAndroid
import VouchaAPI
import VouchaCore
import XCTest

@MainActor
final class AndroidMutationChallengeTests: XCTestCase {
    func testAccountClientPresentsBrokerAndReplaysProviderToken() async throws {
        AndroidAuthURLProtocol.resetRequests()
        defer { AndroidAuthURLProtocol.resetRequests() }
        let account = try AndroidAccount(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.ai")), turnstileSiteKey: "test"),
            cookieStorage: AndroidSessionCookieStorage(secrets: MemorySecrets()),
            protocolClasses: [AndroidAuthURLProtocol.self]
        )
        await account.installMutationChallengeHandler()
        let presented = expectation(description: "Native challenge presented")
        withObservationTracking { _ = account.mutationChallengeBroker.pendingID } onChange: {
            presented.fulfill()
        }
        let completed = expectation(description: "Challenged mutation completed")
        let operation = Task {
            do {
                _ = try await account.apiClient.data(for: .init(.POST, path: "/mutation"))
            } catch { XCTFail("Unexpected mutation failure: \(error)") }
            completed.fulfill()
        }
        defer { operation.cancel()
            account.mutationChallengeBroker.cancel()
        }
        await fulfillment(of: [presented], timeout: 2)
        let id = try XCTUnwrap(account.mutationChallengeBroker.pendingID)
        account.mutationChallengeBroker.finish(id: id, token: "provider-proof")
        await fulfillment(of: [completed], timeout: 2)
        let requests = AndroidAuthURLProtocol.requestSnapshot()
        XCTAssertEqual(requests.map { $0.url?.path }, ["/mutation", "/mutation"])
        XCTAssertNil(requests.first?.value(forHTTPHeaderField: "x-voucha-bot-challenge-token"))
        XCTAssertEqual(requests.last?.value(forHTTPHeaderField: "x-voucha-bot-challenge-token"), "provider-proof")
        XCTAssertNil(account.mutationChallengeBroker.pendingID)
    }
}
