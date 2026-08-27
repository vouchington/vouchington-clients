import Foundation

struct MarkdownPreviewBody: Encodable {
    let markdown: String
}

public struct MarkdownPreviewResponse: Decodable, Sendable {
    public let html: String
}

public extension Endpoint {
    static func markdownPreview(markdown: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/markdown/preview", body: MarkdownPreviewBody(markdown: markdown))
    }
}
