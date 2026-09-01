import Foundation

/// A JSON value that preserves integer precision when decoded and re-encoded.
public enum IntegerPreservingJSONValue: Codable, Sendable {
    case null
    case bool(Bool)
    case signedInteger(Int64)
    case unsignedInteger(UInt64)
    case decimal(Decimal)
    case string(String)
    case array([IntegerPreservingJSONValue])
    case object([String: IntegerPreservingJSONValue])

    public init(from decoder: any Decoder) throws {
        let container = try decoder.singleValueContainer()
        if container.decodeNil() {
            self = .null
        } else if let value = try? container.decode(Bool.self) {
            self = .bool(value)
        } else if let value = try? container.decode(Int64.self) {
            self = .signedInteger(value)
        } else if let value = try? container.decode(UInt64.self) {
            self = .unsignedInteger(value)
        } else if let value = try? container.decode(Decimal.self) {
            self = .decimal(value)
        } else if let value = try? container.decode(String.self) {
            self = .string(value)
        } else if let value = try? container.decode([IntegerPreservingJSONValue].self) {
            self = .array(value)
        } else {
            self = try .object(container.decode([String: IntegerPreservingJSONValue].self))
        }
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.singleValueContainer()
        switch self {
        case .null:
            try container.encodeNil()
        case let .bool(value):
            try container.encode(value)
        case let .signedInteger(value):
            try container.encode(value)
        case let .unsignedInteger(value):
            try container.encode(value)
        case let .decimal(value):
            try container.encode(value)
        case let .string(value):
            try container.encode(value)
        case let .array(value):
            try container.encode(value)
        case let .object(value):
            try container.encode(value)
        }
    }
}
