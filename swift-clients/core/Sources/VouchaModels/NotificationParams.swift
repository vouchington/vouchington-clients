import Foundation

/// A free-form notification param: the server sends strings, numbers and booleans.
public enum NotificationParamValue: Codable, Sendable, Equatable {
    case string(String)
    case number(Double)
    case bool(Bool)

    public init(from decoder: any Decoder) throws {
        let container = try decoder.singleValueContainer()
        if let flag = try? container.decode(Bool.self) {
            self = .bool(flag)
        } else if let number = try? container.decode(Double.self) {
            self = .number(number)
        } else {
            self = try .string(container.decode(String.self))
        }
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.singleValueContainer()
        switch self {
        case let .string(value): try container.encode(value)
        case let .number(value): try container.encode(value)
        case let .bool(value): try container.encode(value)
        }
    }
}

/// Notification params keyed as sent on the wire. The shared decoder camel-cases dictionary keys,
/// so lookups accept the snake_case wire name and its camelCase form.
public struct NotificationParams: Codable, Sendable, Equatable {
    public let values: [String: NotificationParamValue]

    public init(_ values: [String: NotificationParamValue] = [:]) {
        self.values = values
    }

    public init(from decoder: any Decoder) throws {
        values = try decoder.singleValueContainer().decode([String: NotificationParamValue].self)
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.singleValueContainer()
        try container.encode(values)
    }

    public subscript(name: String) -> NotificationParamValue? {
        values[name] ?? values[Self.camelCased(name)]
    }

    /// A numeric param, or 0 when absent or not a number (mirrors `Number(params.x ?? 0)`).
    public func number(_ name: String) -> Double {
        switch self[name] {
        case let .number(value): value
        case let .string(value): Double(value) ?? 0
        case let .bool(flag): flag ? 1 : 0
        case nil: 0
        }
    }

    public func string(_ name: String) -> String? {
        if case let .string(value) = self[name] { value } else { nil }
    }

    public func bool(_ name: String) -> Bool {
        if case .bool(true) = self[name] { true } else { false }
    }

    private static func camelCased(_ name: String) -> String {
        let parts = name.split(separator: "_").map(String.init)
        guard let first = parts.first else { return name }
        return first + parts.dropFirst().map(\.capitalized).joined()
    }
}
