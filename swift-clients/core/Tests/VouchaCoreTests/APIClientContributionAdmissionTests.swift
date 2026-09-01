import Foundation
@testable import VouchaAPI
@testable import VouchaCore
import XCTest

final class APIClientContributionAdmissionTests: XCTestCase {
    override func tearDown() {
        CapturingURLProtocol.responseData = Data("{}".utf8)
        CapturingURLProtocol.responseStatusCode = 200
        CapturingURLProtocol.responseHeaders = ["Content-Type": "application/json"]
        super.tearDown()
    }

    func testAdmissionFailuresPreserveStatusCodeBodyCodeAndRetryAfter() async {
        for (status, code, retryAfter) in [
            (409, "IDEMPOTENCY_KEY_REUSED", "3"),
            (429, "CONTRIBUTION_QUOTA_EXCEEDED", "17")
        ] {
            let body = Data("{\"code\":\"\(code)\"}".utf8)
            CapturingURLProtocol.responseData = body
            CapturingURLProtocol.responseStatusCode = status
            CapturingURLProtocol.responseHeaders = ["Content-Type": "application/json", "Retry-After": retryAfter]
            let client = APIClient(
                config: AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test"),
                cookieStorage: HTTPCookieStorage(), protocolClasses: [CapturingURLProtocol.self]
            )

            do {
                let _: EmptyResponse = try await client.send(.init(.POST, path: "/admission"))
                XCTFail("Expected admission failure")
            } catch let failure as ContributionAdmissionFailure {
                XCTAssertEqual(failure.statusCode, status)
                XCTAssertEqual(failure.code, code)
                XCTAssertEqual(failure.responseBody, body)
                XCTAssertEqual(failure.retryAfter, TimeInterval(retryAfter))
            } catch { XCTFail("Unexpected failure: \(error)") }
        }
    }
}
