import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaTestSupport
import XCTest

final class AndroidAuthURLProtocol: URLProtocol {
    private static let lock = NSLock()
    private nonisolated(unsafe) static var requests: [URLRequest] = []
    private nonisolated(unsafe) static var identity = Data()
    private nonisolated(unsafe) static var otpStatusCode = 200
    private nonisolated(unsafe) static var logoutStatusCode = 200

    static func resetRequests() {
        lock.withLock {
            requests = []
            identity = Data()
            otpStatusCode = 200
            logoutStatusCode = 200
        }
    }

    static func setIdentity(_ data: Data) {
        lock.withLock { identity = data }
    }

    static func setOTPStatusCode(_ statusCode: Int) {
        lock.withLock { otpStatusCode = statusCode }
    }

    static func setLogoutStatusCode(_ statusCode: Int) {
        lock.withLock { logoutStatusCode = statusCode }
    }

    static func requestSnapshot() -> [URLRequest] {
        lock.withLock { requests }
    }

    // URLProtocol requires these class overrides for custom protocol dispatch.
    // swiftlint:disable:next static_over_final_class
    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    // swiftlint:disable:next static_over_final_class
    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        let path = request.url?.path ?? ""
        var recordedRequest = request
        recordedRequest.httpBody = URLRequestHTTPBody.data(from: request)
        let (status, identity) = Self.lock.withLock { () -> (Int, Data) in
            Self.requests.append(recordedRequest)
            let status = switch path {
            case "/api/v1/auth/email-address/tokens": Self.otpStatusCode
            case "/api/v1/auth/logout": Self.logoutStatusCode
            case "/mutation": request.value(forHTTPHeaderField: "x-voucha-bot-challenge-token") == nil ? 403 : 200
            default: 200
            }
            return (status, Self.identity)
        }
        let data: Data
        switch path {
        case "/api/v1/auth/email-address/login":
            data = Data(#"{"mfaRequired":true,"loginAttemptId":"mfa-attempt"}"#.utf8)
        case "/api/v1/my/identity":
            data = identity
        case "/mutation":
            data = Data(#"{"code":"BOT_CHALLENGE_REQUIRED","action":"bot-challenge.mutation","message":"Verify"}"#.utf8)
        case "/api/v1/session":
            do {
                data = try androidSessionRefreshFixture()
            } catch {
                client?.urlProtocol(self, didFailWithError: error)
                return
            }
        default:
            data = Data("{}".utf8)
        }
        guard let url = request.url,
              let response = HTTPURLResponse(
                  url: url,
                  statusCode: status,
                  httpVersion: "HTTP/1.1",
                  headerFields: ["Content-Type": "application/json"]
              )
        else { return }
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}
}

final class MemorySecrets: AndroidEndpointSecretStoring {
    var values: [String: String] = [:]

    func string(forKey key: String) throws -> String? {
        values[key]
    }

    func set(_ value: String, forKey key: String) throws {
        values[key] = value
    }

    func removeValue(forKey key: String) throws {
        values.removeValue(forKey: key)
    }
}

func androidSessionRefreshFixture() throws -> Data {
    let path = "api-fixtures/v1/responses/native.auth.session.refresh.rotated.json"
    let root = try ContractRoot.url(requiredPaths: [path])
    return try Data(contentsOf: ContractRoot.requiredURL(path, root: root))
}
