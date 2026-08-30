import Foundation

public struct CanonicalHashtagSlug: Equatable, Sendable {
    public let value: String

    public init?(rawValue: String) {
        let trimmed = rawValue.trimmingCharacters(in: .whitespacesAndNewlines)
        guard trimmed.utf16.count <= 255 else { return nil }
        let withoutHash = trimmed.first == "#" ? String(trimmed.dropFirst()) : trimmed
        let substituted = withoutHash.replacingOccurrences(of: ".", with: "-")
            .replacingOccurrences(of: "_", with: "-")
            .lowercased()
        guard substituted.first != "-", substituted.last != "-" else { return nil }
        let segments = substituted.split(separator: "-", omittingEmptySubsequences: true)
        guard !segments.isEmpty,
              segments.allSatisfy({ $0.allSatisfy { $0.isASCII && ($0.isLetter || $0.isNumber) } })
        else { return nil }
        let normalized = segments.joined(separator: "-")
        guard normalized.count <= 255 else { return nil }
        value = normalized
    }
}
