import Foundation
import VouchaModels

extension DecodedJSONValue {
    func normalized(for fieldType: CommunityApplicationQuestionFieldType) -> DecodedJSONValue? {
        switch fieldType {
        case .checkbox:
            return boolValue.map(DecodedJSONValue.bool)
        case .multiSelect:
            let values = stringArrayValue
            return values.isEmpty ? nil : .array(values.map(DecodedJSONValue.string))
        case .shortText, .longText, .singleSelect:
            guard let text = stringValue?.trimmedOrNil else { return nil }
            return .string(text)
        }
    }

    var bindingText: String {
        switch self {
        case let .bool(value):
            String(value)
        case let .string(value):
            value
        case let .array(values):
            values.compactMap(\.stringValue).joined(separator: ", ")
        case .null, .number, .object:
            ""
        }
    }

    private var stringValue: String? {
        if case let .string(value) = self {
            return value
        }
        return nil
    }

    private var boolValue: Bool? {
        switch self {
        case let .bool(value):
            value
        case let .string(value):
            switch value.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() {
            case "true", "yes", "on", "1":
                true
            case "false", "no", "off", "0":
                false
            default:
                nil
            }
        default:
            nil
        }
    }

    private var stringArrayValue: [String] {
        switch self {
        case let .array(values):
            values.compactMap { $0.stringValue?.trimmedOrNil }
        case let .string(value):
            value.split(separator: ",").compactMap { String($0).trimmedOrNil }
        default:
            []
        }
    }
}
