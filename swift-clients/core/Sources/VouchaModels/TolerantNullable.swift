@propertyWrapper
public struct TolerantNullable<Value: Codable & Sendable>: Codable, Sendable {
    public var wrappedValue: Value?

    public init(wrappedValue: Value?) {
        self.wrappedValue = wrappedValue
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.singleValueContainer()
        wrappedValue = container.decodeNil() ? nil : try container.decode(Value.self)
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.singleValueContainer()
        if let wrappedValue {
            try container.encode(wrappedValue)
        } else {
            try container.encodeNil()
        }
    }
}

public extension KeyedDecodingContainer {
    func decode<Value: Codable & Sendable>(
        _: TolerantNullable<Value>.Type,
        forKey key: Key
    ) throws -> TolerantNullable<Value> {
        try decodeIfPresent(TolerantNullable<Value>.self, forKey: key)
            ?? TolerantNullable(wrappedValue: nil)
    }
}
