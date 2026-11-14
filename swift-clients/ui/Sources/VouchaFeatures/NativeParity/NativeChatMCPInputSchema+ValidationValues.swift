import Foundation
import VouchaModels

extension NativeChatMCPInputSchema {
    static func checkObject(
        _ object: DecodedJSONObject, fields: DecodedJSONObject,
        root: DecodedJSONValue, depth: Int, remainingWork: inout Int, output: Bool = false
    ) throws -> Bool {
        if let minimum = integer(fields["minProperties"]), object.count < minimum { return false }
        if let maximum = integer(fields["maxProperties"]), object.count > maximum { return false }
        let properties: DecodedJSONObject = if case let .object(declared)? = fields["properties"] {
            declared
        } else { [:] }
        guard hasRequiredProperties(object, fields: fields) else { return false }
        for (name, member) in object {
            if let child = properties[name] {
                if try !check(
                    member,
                    schema: child,
                    root: root,
                    depth: depth + 1,
                    remainingWork: &remainingWork,
                    output: output
                ) {
                    return false
                }
            } else if let additional = fields["additionalProperties"] {
                if case .bool(false) = additional { return false }
                if case .object = additional,
                   try !check(
                       member,
                       schema: additional,
                       root: root,
                       depth: depth + 1,
                       remainingWork: &remainingWork,
                       output: output
                   ) {
                    return false
                }
            }
        }
        return true
    }

    static func hasRequiredProperties(
        _ object: DecodedJSONObject, fields: DecodedJSONObject
    ) -> Bool {
        guard case let .array(required)? = fields["required"] else { return true }
        for key in required {
            guard case let .string(name) = key, object[name] != nil else { return false }
        }
        return true
    }

    static func checkArray(
        _ items: [DecodedJSONValue], fields: DecodedJSONObject,
        root: DecodedJSONValue, depth: Int, remainingWork: inout Int, output: Bool = false
    ) throws -> Bool {
        if let minimum = integer(fields["minItems"]), items.count < minimum { return false }
        if let maximum = integer(fields["maxItems"]), items.count > maximum { return false }
        if case .bool(true)? = fields["uniqueItems"] {
            guard items.count <= 256 else { return false }
            for index in items.indices {
                for prior in items.indices where prior < index {
                    if try output
                        ? sameOutput(items[index], items[prior], remainingWork: &remainingWork)
                        : same(items[index], items[prior]) { return false }
                }
            }
        }
        if let itemSchema = fields["items"] {
            for item in items {
                let acceptsItem = try check(
                    item, schema: itemSchema, root: root, depth: depth + 1, remainingWork: &remainingWork,
                    output: output
                )
                if !acceptsItem {
                    return false
                }
            }
        }
        return true
    }

    static func checkString(_ text: String, fields: DecodedJSONObject) throws -> Bool {
        let length = text.unicodeScalars.count
        if let minimum = integer(fields["minLength"]), length < minimum { return false }
        if let maximum = integer(fields["maxLength"]), length > maximum { return false }
        if case let .string(pattern)? = fields["pattern"] {
            guard let regex = boundedPatternExpressions[pattern] else { throw Failure.unsupportedSchema }
            if regex.firstMatch(in: text, range: NSRange(text.startIndex..., in: text)) == nil { return false }
        }
        if case let .string(format)? = fields["format"] {
            if format == "date-time", !validDateTime(text) { return false }
            if format == "uuid", UUID(uuidString: text) == nil { return false }
            if format == "uri" {
                guard let components = URLComponents(string: text),
                      components.scheme != nil, components.string == text else { return false }
            }
        }
        return true
    }

    static func checkNumber(_ value: DecodedJSONValue, fields: DecodedJSONObject) -> Bool {
        switch value {
        case .number, .integer, .unsignedInteger, .decimal: break
        default: return true
        }
        guard let text = outputNumberText(value), let number = canonicalOutputNumber(text) else { return false }
        for keyword in ["minimum", "maximum"] {
            guard let bound = fields[keyword] else { continue }
            guard let boundText = outputNumberText(bound), let limit = canonicalOutputNumber(boundText) else {
                return false
            }
            let comparison = compareOutputNumbers(number, limit)
            if keyword == "minimum", comparison < 0 { return false }
            if keyword == "maximum", comparison > 0 { return false }
        }
        return true
    }

}
