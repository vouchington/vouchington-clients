import Foundation
@testable import VouchaAndroid

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
        let (status, identity) = Self.lock.withLock { () -> (Int, Data) in
            Self.requests.append(request)
            let status = switch path {
            case "/api/v1/auth/email-address/tokens": Self.otpStatusCode
            case "/api/v1/auth/logout": Self.logoutStatusCode
            default: 200
            }
            return (status, Self.identity)
        }
        let data: Data = switch path {
        case "/api/v1/auth/email-address/login":
            Data(#"{"mfaRequired":true,"loginAttemptId":"mfa-attempt"}"#.utf8)
        case "/api/v1/my/identity":
            identity
        default:
            Data("{}".utf8)
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
