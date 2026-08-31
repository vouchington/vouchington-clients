import Foundation
import UniformTypeIdentifiers
import VouchaAPI
import VouchaCore
import VouchaModels

struct NativeTopicImageUploadService {
    private static let maximumUploadBytes = 50 * 1_024 * 1_024
    let client: APIClient?
    let session: URLSession
    let maxUploadStatePolls: Int
    let uploadStatePollDelayNanoseconds: UInt64

    init(
        client: APIClient?,
        session: URLSession = .shared,
        maxUploadStatePolls: Int = 15,
        uploadStatePollDelayNanoseconds: UInt64 = 2_000_000_000
    ) {
        self.client = client
        self.session = session
        self.maxUploadStatePolls = maxUploadStatePolls
        self.uploadStatePollDelayNanoseconds = uploadStatePollDelayNanoseconds
    }

    func uploadImage(at url: URL) async throws -> String {
        guard let client else {
            throw VouchaError.api(statusCode: 0, preconditionCode: nil)
        }

        let isSecurityScoped = url.startAccessingSecurityScopedResource()
        defer {
            if isSecurityScoped {
                url.stopAccessingSecurityScopedResource()
            }
        }

        guard let fileSize = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize else {
            throw VouchaError.api(statusCode: 0, preconditionCode: "IMAGE_UPLOAD_FILE_SIZE_UNAVAILABLE")
        }
        guard fileSize <= Self.maximumUploadBytes else {
            throw VouchaError.api(statusCode: 0, preconditionCode: "IMAGE_TOO_LARGE")
        }
        let contentType = UTType(filenameExtension: url.pathExtension)?.preferredMIMEType ?? "application/octet-stream"
        let uploadEnvelope: ImageUploadResponse = try await client.send(
            .imageUploadUrl(contentType: contentType, contentLength: fileSize)
        )
        let upload = uploadEnvelope.upload
        let uploadURL = upload.uploadUrl
        try ImageUploadService.validateUploadURL(uploadURL)

        var request = URLRequest(url: uploadURL)
        request.httpMethod = "PUT"
        request.setValue(upload.contentType, forHTTPHeaderField: "Content-Type")

        let (_, response) = try await session.upload(for: request, fromFile: url)
        let status = (response as? HTTPURLResponse)?.statusCode ?? 0
        guard (200 ..< 300).contains(status) else {
            throw VouchaError.api(statusCode: status, preconditionCode: nil)
        }

        let completed: ImageUploadCompletionResponse = try await client
            .send(.completeImageUpload(imageId: upload.imageId))
        return try await waitForReadyImage(imageId: completed.image.id, client: client)
    }

    private func waitForReadyImage(imageId: String, client: APIClient) async throws -> String {
        for attempt in 0 ..< maxUploadStatePolls {
            let envelope: ImageUploadStateResponse = try await client.send(.imageUploadState(imageId: imageId))
            let state = envelope.uploadState
            if state.ready {
                return state.id
            }
            if state.blocked {
                throw VouchaError.api(statusCode: 0, preconditionCode: "IMAGE_UPLOAD_BLOCKED")
            }
            if state.uploadStatus == .failed {
                throw VouchaError.api(statusCode: 0, preconditionCode: "IMAGE_UPLOAD_FAILED")
            }
            if attempt + 1 < maxUploadStatePolls {
                try await Task.sleep(nanoseconds: uploadStatePollDelayNanoseconds)
            }
        }
        throw VouchaError.api(statusCode: 0, preconditionCode: "IMAGE_UPLOAD_TIMEOUT")
    }
}
