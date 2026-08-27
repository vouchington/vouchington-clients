public enum NullableValue<Wrapped: Encodable & Sendable>: Encodable, Sendable {
    case value(Wrapped)
    case null

    public func encode(to encoder: any Encoder) throws {
        switch self {
        case let .value(value):
            var container = encoder.singleValueContainer()
            try container.encode(value)
        case .null:
            var container = encoder.singleValueContainer()
            try container.encodeNil()
        }
    }
}
