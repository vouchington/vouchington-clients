import Foundation

public enum ImageUploadStatus: String, Codable, Hashable, Sendable {
    case pending
    case processing
    case complete
    case failed
}

public struct ImageUploadTarget: Decodable, Sendable {
    public let imageId: String
    public let uploadUrl: URL
    public let contentType: String
    public let expiresAt: Date?

    public init(imageId: String, uploadUrl: URL, contentType: String, expiresAt: Date? = nil) {
        self.imageId = imageId
        self.uploadUrl = uploadUrl
        self.contentType = contentType
        self.expiresAt = expiresAt
    }

    private enum CodingKeys: String, CodingKey {
        case imageId
        case uploadUrl
        case contentType
        case expiresAt
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        imageId = try container.decode(String.self, forKey: .imageId)
        uploadUrl = try container.decode(URL.self, forKey: .uploadUrl)
        contentType = try container.decode(String.self, forKey: .contentType)
        expiresAt = try container.decodeIfPresent(Date.self, forKey: .expiresAt)
    }
}

public struct ImageUploadTargetResponse: Decodable, Sendable {
    public let upload: ImageUploadTarget
}

public struct ImageUploadCompletionImage: Decodable, Sendable {
    public let id: String
    public let uploadStatus: ImageUploadStatus
}

public struct ImageUploadCompletionResponse: Decodable, Sendable {
    public let image: ImageUploadCompletionImage
}

public struct ImageUploadState: Decodable, Sendable {
    public let id: String
    public let uploadStatus: ImageUploadStatus
    public let uploadError: String?
    public let ready: Bool
    public let blocked: Bool
}

public struct ImageUploadStateResponse: Decodable, Sendable {
    public let uploadState: ImageUploadState
}
