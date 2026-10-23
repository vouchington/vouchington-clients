public enum EngineeringPartitionDropRefusal: String, Codable, CaseIterable, Sendable {
    case checkPending = "check-pending"
    case inFlight = "in-flight"
    case insideRetention = "inside-retention"
    case legalHold = "legal-hold"
    case noDeclaredRetention = "no-declared-retention"
}

public struct EngineeringPartitionInfo: Codable, Identifiable, Sendable, Hashable {
    public let name: String
    public let sizeBytes: Int
    public let dropRefusal: EngineeringPartitionDropRefusal?

    public var id: String {
        name
    }

    private enum CodingKeys: String, CodingKey {
        case name
        case sizeBytes
        case dropRefusal
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        name = try container.decode(String.self, forKey: .name)
        sizeBytes = try container.decode(Int.self, forKey: .sizeBytes)
        dropRefusal = try container.decode(EngineeringPartitionDropRefusal?.self, forKey: .dropRefusal)
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(name, forKey: .name)
        try container.encode(sizeBytes, forKey: .sizeBytes)
        try container.encode(dropRefusal, forKey: .dropRefusal)
    }
}
