public struct PublicContentProvenance: Codable, Sendable, Equatable {
    public let via: String
    public let label: String

    public init(via: String, label: String) {
        self.via = via
        self.label = label
    }
}
