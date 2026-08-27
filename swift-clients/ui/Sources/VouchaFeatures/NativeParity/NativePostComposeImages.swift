import Foundation

struct NativePostComposeImageDraft: Identifiable, Equatable {
    static let maxCaptionLength = 1_000

    let id: UUID
    var imageId: String
    private(set) var caption: String

    init(id: UUID = UUID(), imageId: String, caption: String = "") {
        self.id = id
        self.imageId = imageId
        self.caption = Self.sanitizedCaption(caption)
    }

    mutating func updateCaption(_ caption: String) {
        self.caption = Self.sanitizedCaption(caption)
    }

    var publishCaption: String? {
        Self.publishCaption(from: caption)
    }

    private static func sanitizedCaption(_ caption: String) -> String {
        caption.prefixByUTF16Length(maxCaptionLength)
    }

    private static func publishCaption(from caption: String) -> String? {
        let trimmed = caption.trimmingCharacters(in: .whitespacesAndNewlines).prefixByUTF16Length(maxCaptionLength)
        return trimmed.isEmpty ? nil : trimmed
    }
}
