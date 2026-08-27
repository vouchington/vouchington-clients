import Foundation

public enum DynamicConfigValue: Codable, Hashable, Sendable {
    case boolean(Bool)
    case number(Double)
    case string(String)

    public init(from decoder: any Decoder) throws {
        let container = try decoder.singleValueContainer()
        if let value = try? container.decode(Bool.self) {
            self = .boolean(value)
        } else if let value = try? container.decode(Double.self), value.isFinite {
            self = .number(value)
        } else if let value = try? container.decode(String.self) {
            self = .string(value)
        } else {
            throw DecodingError.typeMismatch(
                DynamicConfigValue.self,
                .init(codingPath: decoder.codingPath, debugDescription: "Expected a boolean, finite number, or string")
            )
        }
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.singleValueContainer()
        switch self {
        case let .boolean(value): try container.encode(value)
        case let .number(value): try container.encode(value)
        case let .string(value): try container.encode(value)
        }
    }

    public var displayValue: String {
        switch self {
        case let .boolean(value): String(value)
        case let .number(value): value.formatted(.number.grouping(.never))
        case let .string(value): value
        }
    }

    public var editableValue: String {
        switch self {
        case let .boolean(value): String(value)
        case let .number(value): String(value)
        case let .string(value): value
        }
    }
}

public enum DynamicConfigFieldType: String, Codable, Sendable {
    case boolean, number, string
}

public struct DynamicConfigNamespaceSummary: Codable, Identifiable, Sendable {
    public var id: String {
        namespace
    }

    public let namespace: String
    public let label: String
    public let description: String
    public let fieldCount: Int
    public let canView: Bool
    public let canUpdate: Bool
}

public struct DynamicConfigNamespacesResponse: Codable, Sendable {
    public let namespaces: [DynamicConfigNamespaceSummary]
}

public struct DynamicConfigField: Codable, Identifiable, Sendable {
    public var id: String {
        name
    }

    public let name: String
    public let type: DynamicConfigFieldType
    public let value: DynamicConfigValue
    public let defaultValue: DynamicConfigValue?
    public let description: String
    public let minValue: Double?
    public let maxValue: Double?
    public let maxValueExemption: String?
    public let integer: Bool?
}

public struct DynamicConfigNamespace: Codable, Identifiable, Sendable {
    public var id: String {
        namespace
    }

    public let namespace: String
    public let label: String
    public let description: String
    public let fieldCount: Int
    public let canView: Bool
    public let canUpdate: Bool
    public let config: [String: DynamicConfigValue]
    public let fields: [DynamicConfigField]
}

public struct DynamicConfigNamespaceResponse: Codable, Sendable {
    public let namespace: DynamicConfigNamespace
}

public struct DynamicConfigUpdateResponse: Codable, Sendable {
    public let namespace: DynamicConfigNamespace
    public let changed: Bool
}

public struct DynamicConfigHistoryActor: Codable, Sendable {
    public let id: String
    public let username: String?
}

public struct DynamicConfigHistoryChange: Codable, Sendable {
    public let previous: DynamicConfigValue
    public let next: DynamicConfigValue
}

public struct DynamicConfigHistoryEntry: Codable, Identifiable, Sendable {
    public let id: String
    public let namespace: String
    public let changedBy: DynamicConfigHistoryActor?
    public let previousFields: [String: DynamicConfigValue]
    public let nextFields: [String: DynamicConfigValue]
    public let changedFields: [String: DynamicConfigHistoryChange]
    public let createdAt: Date
}

public struct DynamicConfigHistoryResponse: Codable, Sendable {
    public let history: [DynamicConfigHistoryEntry]
}
