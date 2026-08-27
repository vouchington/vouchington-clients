import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
import VouchaCore
import VouchaModels

public enum ImageUploadServiceError: LocalizedError, Sendable {
    case timeout(imageId: String)
    case uploadFailed(statusCode: Int)
    case invalidUploadURL

    public var errorDescription: String? {
        switch self {
        case .timeout:
            "Image upload timed out."
        case let .uploadFailed(statusCode):
            statusCode == 0
                ? "Image upload failed."
                : "Image upload failed with status code \(statusCode)."
        case .invalidUploadURL:
            "Invalid image upload URL."
        }
    }
}

public actor ImageUploadService {
    private static let localUploadHosts: Set<String> = ["localhost", "127.0.0.1", "::1"]

    private let client: APIClient
    private let uploadSession: URLSession
    private let pollIntervalNanoseconds: UInt64
    private let timeoutNanoseconds: UInt64

    public init(
        client: APIClient,
        uploadProtocolClasses: [AnyClass]? = nil,
        pollInterval: TimeInterval = 2,
        timeout: TimeInterval = 60
    ) {
        self.client = client

        let configuration = URLSessionConfiguration.ephemeral
        configuration.httpShouldSetCookies = false
        configuration.httpCookieAcceptPolicy = .never
        if let uploadProtocolClasses {
            configuration.protocolClasses = uploadProtocolClasses
        }
        uploadSession = URLSession(configuration: configuration)

        pollIntervalNanoseconds = max(0, UInt64(pollInterval * 1_000_000_000))
        timeoutNanoseconds = max(0, UInt64(timeout * 1_000_000_000))
    }

    public func uploadImage(data: Data, contentType: String) async throws -> ImageUploadState {
        let request: ImageUploadTargetResponse = try await client.send(
            .requestImageUploadURL(contentType: contentType, contentLength: data.count)
        )

        try await upload(data: data, to: request.upload)

        let deadline = DispatchTime.now().uptimeNanoseconds + timeoutNanoseconds
        let completion = try await completeImageUpload(imageId: request.upload.imageId, deadline: deadline)
        return try await pollUntilTerminalState(imageId: completion.image.id, deadline: deadline)
    }

    private func upload(data: Data, to upload: ImageUploadTarget) async throws {
        try Self.validateUploadURL(upload.uploadUrl)
        var request = URLRequest(url: upload.uploadUrl)
        request.httpMethod = "PUT"
        request.setValue(upload.contentType, forHTTPHeaderField: "Content-Type")
        request.setValue("\(data.count)", forHTTPHeaderField: "Content-Length")

        let (_, response) = try await uploadSession.upload(for: request, from: data)
        guard let http = response as? HTTPURLResponse, (200 ..< 300).contains(http.statusCode) else {
            throw ImageUploadServiceError.uploadFailed(statusCode: (response as? HTTPURLResponse)?.statusCode ?? 0)
        }
    }

    public static func validateUploadURL(_ url: URL) throws {
        switch url.scheme?.lowercased() {
        case "https":
            return
        case "http":
            if let host = url.host?.lowercased(), localUploadHosts.contains(host) {
                return
            }
        default:
            break
        }
        throw ImageUploadServiceError.invalidUploadURL
    }

    private func completeImageUpload(
        imageId: String,
        deadline: UInt64
    ) async throws -> ImageUploadCompletionResponse {
        while true {
            try Task.checkCancellation()
            do {
                return try await client.send(.completeImageUpload(imageId: imageId))
            } catch {
                try await retryAfterTransientFailure(error, imageId: imageId, deadline: deadline)
            }
        }
    }

    private func pollUntilTerminalState(imageId: String, deadline: UInt64) async throws -> ImageUploadState {
        while true {
            try Task.checkCancellation()
            do {
                let response: ImageUploadStateResponse = try await client.send(.imageUploadState(imageId: imageId))
                let state = response.uploadState
                if state.ready || state.blocked || state.uploadStatus == .complete || state.uploadStatus == .failed {
                    return state
                }
            } catch {
                try await retryAfterTransientFailure(error, imageId: imageId, deadline: deadline)
                continue
            }

            try await waitBeforeRetry(imageId: imageId, deadline: deadline)
        }
    }

    private func retryAfterTransientFailure(
        _ error: Error,
        imageId: String,
        deadline: UInt64
    ) async throws {
        guard isRetryableUploadError(error) else { throw error }
        try await waitBeforeRetry(imageId: imageId, deadline: deadline)
    }

    private func waitBeforeRetry(imageId: String, deadline: UInt64) async throws {
        let now = DispatchTime.now().uptimeNanoseconds
        guard now < deadline else {
            throw ImageUploadServiceError.timeout(imageId: imageId)
        }
        let remaining = deadline - now
        let delay = pollIntervalNanoseconds == 0 ? 0 : min(pollIntervalNanoseconds, remaining)
        if delay > 0 {
            try await Task.sleep(nanoseconds: delay)
        } else {
            await Task.yield()
        }
    }

    private func isRetryableUploadError(_ error: Error) -> Bool {
        switch error {
        case is CancellationError:
            false
        case let error as VouchaError:
            switch error {
            case let .network(urlError):
                isRetryable(urlError)
            case let .api(statusCode, _),
                 let .apiMessage(statusCode, _, _):
                isRetryable(statusCode)
            case .cancelled:
                false
            default:
                false
            }
        case let error as URLError:
            isRetryable(error)
        default:
            false
        }
    }

    private func isRetryable(_ error: URLError) -> Bool {
        switch error.code {
        case .timedOut,
             .networkConnectionLost,
             .cannotFindHost,
             .cannotConnectToHost,
             .dnsLookupFailed,
             .notConnectedToInternet:
            true
        default:
            false
        }
    }

    private func isRetryable(_ statusCode: Int) -> Bool {
        statusCode == 408 || statusCode == 425 || statusCode == 429 || (500 ... 599).contains(statusCode)
    }
}
