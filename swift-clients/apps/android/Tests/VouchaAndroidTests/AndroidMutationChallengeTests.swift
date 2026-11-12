import Foundation
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
        let operation = Task {
            try await account.apiClient.data(for: .init(.POST, path: "/mutation"))
        }
        defer { operation.cancel()
            account.mutationChallengeBroker.cancel()
        }
        for _ in 0 ..< 1_000 {
            if account.mutationChallengeBroker.pendingID != nil { break }
            await Task.yield()
        }
        let id = try XCTUnwrap(account.mutationChallengeBroker.pendingID)
        account.mutationChallengeBroker.finish(id: id, token: "provider-proof")
        _ = try await operation.value
        let requests = AndroidAuthURLProtocol.requestSnapshot()
        XCTAssertEqual(requests.map { $0.url?.path }, ["/mutation", "/mutation"])
        XCTAssertNil(requests.first?.value(forHTTPHeaderField: "x-voucha-bot-challenge-token"))
        XCTAssertEqual(requests.last?.value(forHTTPHeaderField: "x-voucha-bot-challenge-token"), "provider-proof")
        XCTAssertNil(account.mutationChallengeBroker.pendingID)
    }
}
