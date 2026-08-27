public struct ImageUploadRequest: Encodable, Sendable {
    public let contentType: String
    public let contentLength: Int

    public init(contentType: String, contentLength: Int) {
        self.contentType = contentType
        self.contentLength = contentLength
    }

    private enum CodingKeys: String, CodingKey {
        case contentType = "content_type"
        case contentLength = "content_length"
    }
}

public typealias ImageUploadResponse = ImageUploadTargetResponse
