import Foundation
@testable import VouchaAPI
import XCTest

final class CapturingURLProtocol: URLProtocol {
    static var responseData = Data("{}".utf8)
    static var responseStatusCode = 200
    static var lastRequestURL: URL?
    static var capturedRequestHeaders: [String: String] = [:]
    static var responseHeaders: [String: String] = ["Content-Type": "application/json"]

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        Self.lastRequestURL = request.url
        Self.capturedRequestHeaders = request.allHTTPHeaderFields ?? [:]
        let response = HTTPURLResponse(
            url: request.url!,
            statusCode: Self.responseStatusCode,
            httpVersion: "HTTP/1.1",
            headerFields: Self.responseHeaders
        )!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: Self.responseData)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}
}

func assertEndpoint(
    _ endpoint: Endpoint,
    method: HTTPMethod = .GET,
    path: String,
    body expectedBody: [String: Any]? = nil,
    file: StaticString = #filePath,
    line: UInt = #line
) {
    XCTAssertEqual(endpoint.method, method, file: file, line: line)
    XCTAssertEqual(endpoint.path, path, file: file, line: line)
    guard let expectedBody else {
        XCTAssertNil(endpoint.body, file: file, line: line)
        return
    }

    do {
        let body = try XCTUnwrap(endpoint.body, file: file, line: line)
        let encoder = JSONEncoder()
        if endpoint.bodyKeyEncodingStrategy == .convertToSnakeCase {
            encoder.keyEncodingStrategy = .convertToSnakeCase
        }
        let data = try encoder.encode(body)
        let object = try JSONSerialization.jsonObject(with: data)
        XCTAssertTrue(jsonObject(object, matches: expectedBody), file: file, line: line)
    } catch {
        XCTFail("Failed to encode endpoint body: \(error)", file: file, line: line)
    }
}

func makeVouchaDecoder() -> JSONDecoder {
    let decoder = JSONDecoder()
    decoder.keyDecodingStrategy = .convertFromSnakeCase
    let iso8601WithFractionalSeconds = Date.ISO8601FormatStyle(includingFractionalSeconds: true)
    let iso8601 = Date.ISO8601FormatStyle(includingFractionalSeconds: false)

    decoder.dateDecodingStrategy = .custom { decoder in
        let container = try decoder.singleValueContainer()
        let string = try container.decode(String.self)
        if let date = try? Date(string, strategy: iso8601WithFractionalSeconds) {
            return date
        }
        if let date = try? Date(string, strategy: iso8601) {
            return date
        }
        throw DecodingError.dataCorruptedError(in: container, debugDescription: "Cannot parse date: \(string)")
    }
    return decoder
}

private func jsonObject(_ lhs: Any, matches rhs: Any) -> Bool {
    switch (lhs, rhs) {
    case let (lhs as [String: Any], rhs as [String: Any]):
        guard lhs.count == rhs.count else { return false }
        return rhs.allSatisfy { key, value in
            guard let lhsValue = lhs[key] else { return false }
            return jsonObject(lhsValue, matches: value)
        }
    case let (lhs as String, rhs as String):
        return lhs == rhs
    case let (lhs as Int, rhs as Int):
        return lhs == rhs
    case let (lhs as Double, rhs as Double):
        return lhs == rhs
    case let (lhs as Bool, rhs as Bool):
        return lhs == rhs
    case (_ as NSNull, _ as NSNull):
        return true
    case let (lhs as [Any], rhs as [Any]):
        guard lhs.count == rhs.count else { return false }
        return zip(lhs, rhs).allSatisfy(jsonObject)
    default:
        return false
    }
}
