import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaModels

actor FinancialMCPTokenStore: MemberMCPOAuthTokenStore {
    private var tokens: MemberMCPOAuthTokens? = MemberMCPOAuthTokens(
        accessToken: "known-access", refreshToken: "known-refresh", expiresIn: 3_600,
        scope: "financial-profile:read", tokenType: "Bearer"
    )

    func load(scope _: MemberMCPOAuthTokenScope) async throws -> MemberMCPOAuthTokens? {
        tokens
    }

    func save(_ tokens: MemberMCPOAuthTokens, scope _: MemberMCPOAuthTokenScope) async throws {
        self.tokens = tokens
    }

    func clear(scope _: MemberMCPOAuthTokenScope) async throws {
        tokens = nil
    }
}

final class FinancialMCPURLProtocol: URLProtocol {
    private static let lock = NSLock()
    private static var content: DecodedJSONValue?
    private static var isError = false
    private static var lastCall = (bearer: "", method: "", name: "", arguments: [String: Any]())

    static func configure(content: DecodedJSONValue?, isError: Bool = false) {
        lock.lock()
        self.content = content
        self.isError = isError
        lastCall = ("", "", "", [:])
        lock.unlock()
    }

    static func snapshot() -> (bearer: String, method: String, name: String, arguments: [String: Any]) {
        lock.lock()
        defer { lock.unlock() }
        return lastCall
    }

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        do {
            guard let bodyData = request.httpBody ?? request.httpBodyStream.flatMap(Self.readBodyStream),
                  let body = try JSONSerialization.jsonObject(with: bodyData) as? [String: Any],
                  let params = body["params"] as? [String: Any],
                  let method = body["method"] as? String,
                  let name = params["name"] as? String,
                  let arguments = params["arguments"] as? [String: Any]
            else { throw MemberMCPClient.Failure.invalidResponse }
            Self.lock.lock()
            Self.lastCall = (
                request.value(forHTTPHeaderField: "Authorization") ?? "", method, name, arguments
            )
            let content = Self.content
            let isError = Self.isError
            Self.lock.unlock()
            let structured: Any = if let content {
                try JSONSerialization.jsonObject(with: JSONEncoder().encode(content))
            } else {
                NSNull()
            }
            let result: [String: Any] = [
                "content": [], "structuredContent": structured, "isError": isError
            ]
            let response = try JSONSerialization.data(withJSONObject: [
                "jsonrpc": "2.0", "id": body["id"] ?? "", "result": result
            ])
            let http = HTTPURLResponse(
                url: request.url!, statusCode: 200, httpVersion: "HTTP/1.1",
                headerFields: ["Content-Type": "application/json"]
            )!
            client?.urlProtocol(self, didReceive: http, cacheStoragePolicy: .notAllowed)
            client?.urlProtocol(self, didLoad: response)
            client?.urlProtocolDidFinishLoading(self)
        } catch {
            client?.urlProtocol(self, didFailWithError: error)
        }
    }

    override func stopLoading() {}

    private static func readBodyStream(_ stream: InputStream) -> Data? {
        stream.open()
        defer { stream.close() }
        var data = Data()
        var bytes = [UInt8](repeating: 0, count: 4_096)
        while stream.hasBytesAvailable {
            let count = stream.read(&bytes, maxLength: bytes.count)
            if count < 0 { return nil }
            if count == 0 { break }
            data.append(bytes, count: count)
        }
        return data
    }
}
