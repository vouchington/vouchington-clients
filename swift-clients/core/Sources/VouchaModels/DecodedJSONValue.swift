import Foundation

/// JSON values for native model and API contracts.
///
/// Codable is suitable for ordinary model objects. A keyed `Decoder` may
/// canonicalize Unicode member names, so arbitrary JSON that must preserve
/// exact names must enter through `init(exactJSONData:)` and leave through
/// `exactJSONData()`.
public enum DecodedJSONValue: Codable, Sendable {
    case null
    case bool(Bool)
    case number(Double)
    case integer(Int64)
    case unsignedInteger(UInt64)
    case decimal(Decimal)
    case string(String)
    case array([DecodedJSONValue])
    case object(DecodedJSONObject)

    public init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if container.decodeNil() {
            self = .null
        } else if let value = try? container.decode(Bool.self) {
            self = .bool(value)
        } else if let value = try? container.decode(Int64.self),
                  value > 9_007_199_254_740_992 || value < -9_007_199_254_740_992 {
            self = .integer(value)
        } else if let value = try? container.decode(UInt64.self),
                  value > 9_007_199_254_740_992 {
            self = .unsignedInteger(value)
        } else if let value = try? container.decode(Decimal.self),
                  value > Decimal(9_007_199_254_740_992) ||
                  value < Decimal(-9_007_199_254_740_992) ||
                  (try? container.decode(Double.self)).map({ Decimal($0) != value }) ?? true {
            self = .decimal(value)
        } else if let value = try? container.decode(Double.self) {
            self = .number(value)
        } else if let value = try? container.decode(String.self) {
            self = .string(value)
        } else if let value = try? container.decode([DecodedJSONValue].self) {
            self = .array(value)
        } else {
            let value = try container.decode(DecodedJSONObject.self)
            self = .object(value)
        }
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        switch self {
        case .null:
            try container.encodeNil()
        case let .bool(value):
            try container.encode(value)
        case let .number(value):
            try container.encode(value)
        case let .integer(value):
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
