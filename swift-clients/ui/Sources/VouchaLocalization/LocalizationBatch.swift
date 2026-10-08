import Foundation

public struct LocalizationBatch: Decodable, Sendable, Equatable {
    public let contract: String
    public let revision: String
    public let ttlSeconds: Int
    public let messages: [String: LocalizationLeaf]

    public func flattenedValues() -> [String: String] {
        messages.reduce(into: [String: String]()) { result, pair in
            result.merge(pair.value.flattened(id: pair.key), uniquingKeysWith: { _, new in new })
        }
    }
}

public enum LocalizationLeaf: Decodable, Sendable, Equatable {
    case text(String)
    case plural(valueParameter: String, forms: [String: String])
    case selectPlural(valueParameter: String, selectParameter: String, cases: [String: [String: String]])

    public init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if let text = try? container.decode(String.self) {
            self = .text(text)
            return
        }
        let object = try container.decode(Object.self)
        switch object.kind {
        case "plural":
            self = .plural(
                valueParameter: object.valueParameter ?? "count",
                forms: object.forms ?? [:]
            )
        case "select-plural":
            self = .selectPlural(
                valueParameter: object.valueParameter ?? "count",
                selectParameter: object.selectParameter ?? "select",
                cases: object.cases ?? [:]
            )
        default:
            throw DecodingError.dataCorruptedError(
                in: container,
                debugDescription: "Unknown localization leaf kind \(object.kind)"
            )
        }
    }

    public func flattened(id: String) -> [String: String] {
        switch self {
        case let .text(value):
            [id: value]
        case let .plural(_, forms):
            Dictionary(uniqueKeysWithValues: forms.map { ("\(id).__plural.\($0.key)", $0.value) })
        case let .selectPlural(_, _, cases):
            cases.reduce(into: [String: String]()) { result, pair in
                for (category, value) in pair.value {
                    result["\(id).__select.\(pair.key).\(category)"] = value
                }
            }
        }
    }

    private struct Object: Decodable {
        let kind: String
        let valueParameter: String?
        let selectParameter: String?
        let forms: [String: String]?
        let cases: [String: [String: String]]?
    }
}
