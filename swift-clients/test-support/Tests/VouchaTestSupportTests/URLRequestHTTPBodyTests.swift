import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaTestSupport
import XCTest

final class URLRequestHTTPBodyTests: XCTestCase {
    func testMissingBodyIsNil() throws {
        let request = try URLRequest(url: exampleURL())
        XCTAssertNil(URLRequestHTTPBody.data(from: request))
        XCTAssertNil(URLRequestHTTPBody.utf8String(from: request))
    }

    func testHTTPBodyReturnsBytesAndRejectsInvalidUTF8() throws {
        var request = try URLRequest(url: exampleURL())
        request.httpBody = Data([0xFF])
        XCTAssertEqual(URLRequestHTTPBody.data(from: request), Data([0xFF]))
        XCTAssertNil(URLRequestHTTPBody.utf8String(from: request))
    }

    func testStreamBodyReadsPastOneBuffer() throws {
        let payload = Data(repeating: 0x61, count: 5_000)
        var dataRequest = try URLRequest(url: exampleURL())
        dataRequest.httpBodyStream = InputStream(data: payload)
        XCTAssertEqual(URLRequestHTTPBody.data(from: dataRequest), payload)

        var stringRequest = try URLRequest(url: exampleURL())
        stringRequest.httpBodyStream = InputStream(data: payload)
        XCTAssertEqual(
            URLRequestHTTPBody.utf8String(from: stringRequest),
            String(repeating: "a", count: 5_000)
        )
    }

    func testEmptyStreamBodyIsEmpty() throws {
        var request = try URLRequest(url: exampleURL())
        request.httpBodyStream = InputStream(data: Data())
        XCTAssertEqual(URLRequestHTTPBody.data(from: request), Data())
        XCTAssertEqual(URLRequestHTTPBody.utf8String(from: request), "")
    }

    private func exampleURL() throws -> URL {
        try XCTUnwrap(URL(string: "https://example.test/body"))
    }
}
