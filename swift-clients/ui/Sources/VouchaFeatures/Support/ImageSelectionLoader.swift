import Foundation
import UniformTypeIdentifiers
import VouchaLocalization

enum ImageSelectionError: Error {
    case imageTooLarge
    case unsupportedImageFormat(pathExtension: String)

    var message: UiMessage {
        switch self {
        case .imageTooLarge:
            UiMessage(.nativeSwiftImageSelectionImageTooLarge)
        case let .unsupportedImageFormat(pathExtension):
            if pathExtension.isEmpty {
                UiMessage(.nativeSwiftImageSelectionUnsupportedImageMissingExtension)
            } else {
                UiMessage(
                    .nativeSwiftImageSelectionUnsupportedImageExtension,
                    parameters: ["extension": pathExtension]
                )
            }
        }
    }
}

enum ImageSelectionLoader {
    private static let maxImageBytes = 50 * 1_024 * 1_024
    private static let supportedContentTypes: Set<String> = [
        "image/avif",
        "image/gif",
        "image/heic",
        "image/heif",
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/tiff",
        "image/webp"
    ]

    static func load(from url: URL) async throws -> (Data, String) {
        try await Task.detached(priority: .userInitiated) {
            let scoped = url.startAccessingSecurityScopedResource()
            defer {
                if scoped {
                    url.stopAccessingSecurityScopedResource()
                }
            }
            let contentType = try mimeType(for: url)
            try validateFileSize(for: url)
            let data = try Data(contentsOf: url)
            return (data, contentType)
        }.value
    }

    private static func validateFileSize(for url: URL) throws {
        let values = try url.resourceValues(forKeys: [.fileSizeKey])
        guard let fileSize = values.fileSize else { return }
        if fileSize > maxImageBytes {
            throw ImageSelectionError.imageTooLarge
        }
    }

    private static func mimeType(for url: URL) throws -> String {
        guard
            let type = UTType(filenameExtension: url.pathExtension),
            type.conforms(to: .image),
            let contentType = type.preferredMIMEType,
            supportedContentTypes.contains(contentType.lowercased())
        else {
            throw ImageSelectionError.unsupportedImageFormat(pathExtension: url.pathExtension)
        }
        return contentType
    }
}
