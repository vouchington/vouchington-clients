import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaCore
import XCTest

final class APIClientImagePlacementLimitTests: XCTestCase {
    func testProducerPlacementConflictPreservesDomainCodeAndStatus() throws {
        let url = try XCTUnwrap(URL(string: "https://api.example.test/api/v1/posts/post-1/images"))
        let client = APIClient(
            config: AppConfig(baseURL: url),
            cookieStorage: IsolatedHTTPCookieStorage.make(),
            bootstrapSession: false
        )
        let response = try XCTUnwrap(HTTPURLResponse(url: url, statusCode: 409, httpVersion: nil, headerFields: nil))

        XCTAssertThrowsError(try client.validate(
            response: response,
            data: ApiFixtureLoader.data("native.posts.images.placement-limit.conflict")
        )) { error in
            guard case let VouchaError.apiMessage(status, code, message) = error else {
                return XCTFail("Expected coded placement conflict, got \(error)")
            }
            XCTAssertEqual(status, 409)
            XCTAssertEqual(code, "IMAGE_PLACEMENT_LIMIT_REACHED")
            XCTAssertEqual(message, "Image placement limit reached")
        }
    }
}
