@propertyWrapper
public struct RequiredNullable<Value: Codable & Sendable>: Codable, Sendable {
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
        _: RequiredNullable<Value>.Type,
        forKey key: Key
    ) throws -> RequiredNullable<Value> {
        guard contains(key) else {
            throw DecodingError.keyNotFound(
                key,
                DecodingError.Context(
                    codingPath: codingPath,
                    debugDescription: "Required nullable key '\(key.stringValue)' is missing."
                )
            )
        }

        return try decodeIfPresent(RequiredNullable<Value>.self, forKey: key)
            ?? RequiredNullable(wrappedValue: nil)
    }
}
