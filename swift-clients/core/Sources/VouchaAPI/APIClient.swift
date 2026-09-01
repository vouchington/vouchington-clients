import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
import VouchaCore

public actor APIClient {
    private let config: AppConfig
    let session: URLSession
    let cookieStorage: HTTPCookieStorage
    let decoder: JSONDecoder
    let encoder: JSONEncoder
    private let logger = VouchaLogger(category: "APIClient")
    public nonisolated let baseURL: URL
    private let signer: (any RequestSigning)?
    private let metadata: ClientMetadata
    let sessionBootstrapEnabled: Bool
    var sessionBootstrapTask: Task<Void, Error>?

    public init(
        config: AppConfig = .shared,
        cookieStorage: HTTPCookieStorage = .shared,
        protocolClasses: [AnyClass]? = nil,
        bootstrapSession: Bool? = nil,
        signer: (any RequestSigning)? = nil,
        metadata: ClientMetadata = ClientMetadata(platform: .macos, appVersion: "development"),
        pinningPolicy: PublicKeyPinningPolicy = .vouchaDefault
    ) {
        self.signer = signer
        self.metadata = metadata
        sessionBootstrapEnabled = bootstrapSession ?? (protocolClasses == nil)
        self.cookieStorage = cookieStorage
        self.config = config
        baseURL = config.baseURL
        let sessionConfig = URLSessionConfiguration.default
        sessionConfig.httpCookieStorage = cookieStorage
        sessionConfig.httpShouldSetCookies = true
        sessionConfig.httpCookieAcceptPolicy = .always
        if let protocolClasses {
            sessionConfig.protocolClasses = protocolClasses
        }
        let configuredSession = Self.makeSession(
            configuration: sessionConfig,
            baseURL: config.baseURL,
            pinningPolicy: pinningPolicy
        )
        session = configuredSession
        decoder = Self.makeDecoder()
        encoder = Self.makeEncoder()
    }

    public func send<T: Decodable>(_ endpoint: Endpoint) async throws -> T {
        try await send(endpoint, allowingStatusCodes: [])
    }

    func applySigningIfNeeded(_ request: URLRequest, method: String) async -> URLRequest {
        guard let signer, let url = request.url else { return request }
        guard request.value(forHTTPHeaderField: RequestSignatureHeader.challengeId) == nil else { return request }
        guard let headers = try? await signer.signingHeaders(
            method: method,
            path: url.path(percentEncoded: true),
            body: request.httpBody
        )
        else { return request }
        var signed = request
        for (key, value) in headers {
            signed.setValue(value, forHTTPHeaderField: key)
        }
        return signed
    }

    func buildRequest(_ endpoint: Endpoint) throws -> URLRequest {
        var components = URLComponents()
        components.scheme = config.baseURL.scheme
        components.host = config.baseURL.host
        components.port = config.baseURL.port
        let baseComponents = URLComponents(url: config.baseURL, resolvingAgainstBaseURL: false)
        let basePath = baseComponents?.percentEncodedPath ?? config.baseURL.path
        let normalizedBasePath = if basePath == "/" {
            ""
        } else if basePath.hasSuffix("/") {
            String(basePath.dropLast())
        } else {
            basePath
        }
        components.percentEncodedPath = normalizedBasePath + percentEncodedEndpointPath(endpoint.path)
        if !endpoint.queryItems.isEmpty {
            components.queryItems = endpoint.queryItems
        }
        guard let url = components.url else {
            throw VouchaError.api(statusCode: 0, preconditionCode: "INVALID_URL")
        }
        var request = URLRequest(url: url)
        request.httpMethod = endpoint.method.rawValue
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        if let body = endpoint.body {
            request.httpBody = try encodeBody(body, for: endpoint)
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        }
        for (field, value) in endpoint.headers {
            request.setValue(value, forHTTPHeaderField: field)
        }
        request.setValue("swift", forHTTPHeaderField: "x-voucha-client")
        request.setValue(metadata.platform.rawValue, forHTTPHeaderField: "x-voucha-platform")
        request.setValue(metadata.appVersion, forHTTPHeaderField: "x-voucha-app-version")
        request.setValue(metadata.sdkVersion, forHTTPHeaderField: "x-voucha-sdk-version")
        return request
    }

    func validate(response: URLResponse, data: Data) throws {
        guard let http = response as? HTTPURLResponse else { return }
        let status = http.statusCode
        guard status >= 200, status < 300 else {
            let payload = APIErrorPayload.decode(from: data, logger: logger)
            let code = payload?.code
            if status == 409 || status == 429, [
                "IDEMPOTENCY_KEY_REUSED",
                "CONTRIBUTION_ADMISSION_IN_PROGRESS",
                "CONTRIBUTION_QUOTA_EXCEEDED"
            ].contains(code) {
                throw ContributionAdmissionFailure(
                    statusCode: status,
                    code: code,
                    responseBody: data,
                    retryAfter: http.value(forHTTPHeaderField: "Retry-After").flatMap(TimeInterval.init)
                )
            }
            switch status {
            case 401: throw VouchaError.unauthorized
            case 403: throw VouchaError.forbidden(preconditionCode: code)
            case 404: throw VouchaError.notFound
            default:
                if status < 500, let message = payload?.displayMessage {
                    throw VouchaError.apiMessage(statusCode: status, preconditionCode: code, message: message)
                }
                throw VouchaError.api(statusCode: status, preconditionCode: code)
            }
        }
    }

}

public extension APIClient {
    func contributionStatus(action: String? = nil) async throws -> ContributionStatusResponse {
        try await send(.contributionStatus(action: action))
    }

    func send<T: Decodable>(
        _ endpoint: Endpoint,
        allowingStatusCodes allowedStatusCodes: Set<Int>
    ) async throws -> T {
        if endpoint.path != "/api/v1/session" {
            try await ensureSessionBootstrap()
        }
        let rawRequest = try buildRequest(endpoint)
        let request = await applySigningIfNeeded(rawRequest, method: endpoint.method.rawValue)
        logger.debug("→ \(endpoint.method.rawValue) \(endpoint.path)")
        let (data, response): (Data, URLResponse)
        do {
            (data, response) = try await session.data(for: request)
        } catch let error as URLError {
            throw VouchaError.network(error)
        }
        if let http = response as? HTTPURLResponse, allowedStatusCodes.contains(http.statusCode) == false {
            try validate(response: response, data: data)
        }
        if endpoint.path == "/api/v1/session" {
            try persistSessionTokensIfPresent(from: data)
        }
        let decodeData = data.isEmpty ? Data("{}".utf8) : data
        do {
            return try decoder.decode(T.self, from: decodeData)
        } catch let error as DecodingError {
            throw VouchaError.decodingFailed(error)
        }
    }
}
