import Foundation
import VouchaModels

extension NativeChatMCPInputSchema {
    static func inspect(_ schema: DecodedJSONValue, root: DecodedJSONValue, depth: Int, output: Bool = false) throws {
        guard depth < 32, case let .object(fields) = schema else { throw Failure.unsupportedSchema }
        for (key, item) in fields {
            try inspectKeyword(key, item: item, root: root, depth: depth, output: output)
        }
        if case .bool(true)? = fields["uniqueItems"] {
            guard let maximum = integer(fields["maxItems"]), maximum <= 256 else {
                throw Failure.unsupportedSchema
            }
        }
        if let format = fields["format"] {
            guard case let .string(name) = format,
                  ["uuid", "uri"].contains(name) || output && name == "date-time" else {
                throw Failure.unsupportedSchema
            }
        }
        if let pattern = fields["pattern"] {
            guard case let .string(source) = pattern,
                  boundedPatternSources.contains(source) else { throw Failure.unsupportedSchema }
        }
    }

    /// The bounded pattern forms in the published member MCP catalog. Unknown
    /// regexes fail closed because NSRegularExpression has no match-time budget.
    static let boundedPatternSources: Set<String> = [
        #"\S"#,
        #"^(0|[1-9][0-9]*)$"#,
        #"^([01]\d|2[0-3]):[0-5]\d$"#,
        #"^[a-z][a-z0-9_]{0,99}$"#
    ]

    static func inspectKeyword(
        _ key: String, item: DecodedJSONValue, root: DecodedJSONValue, depth: Int, output: Bool
    ) throws {
        let simple: Set = [
            "type", "const", "enum", "required", "minimum", "maximum", "minLength", "maxLength",
            "pattern", "format", "minItems", "maxItems", "uniqueItems", "minProperties", "maxProperties",
            "title", "description", "default", "examples", "deprecated", "readOnly", "writeOnly"
        ]
        if simple.contains(key) {
            try inspectSimple(key, item: item, output: output)
        } else if key == "$ref" {
            try inspectReference(item, root: root)
        } else if ["items", "additionalProperties"].contains(key) {
            if key == "additionalProperties", case .bool = item { return }
            try inspect(item, root: root, depth: depth + 1, output: output)
        } else if ["oneOf", "anyOf", "allOf"].contains(key) {
            guard case let .array(branches) = item, !branches.isEmpty else { throw Failure.unsupportedSchema }
            for branch in branches {
                try inspect(branch, root: root, depth: depth + 1, output: output)
            }
        } else if ["properties", "$defs"].contains(key) {
            guard case let .object(children) = item else { throw Failure.unsupportedSchema }
            for child in children.values {
                try inspect(child, root: root, depth: depth + 1, output: output)
            }
        } else { throw Failure.unsupportedSchema }
    }

    static func inspectReference(_ value: DecodedJSONValue, root: DecodedJSONValue) throws {
        guard case let .string(path) = value,
              let name = localDefinitionName(from: path),
              case let .object(rootFields) = root,
              case let .object(defs)? = rootFields["$defs"],
              defs[name] != nil else { throw Failure.unsupportedSchema }
    }

    static func inspectSimple(_ key: String, item: DecodedJSONValue, output: Bool = false) throws {
        if ["minimum", "maximum"].contains(key),
           output ? outputNumberText(item).flatMap(canonicalOutputNumber) == nil : decimalNumber(item) == nil {
            throw Failure.unsupportedSchema
        }
        if ["minLength", "maxLength", "minItems", "maxItems", "minProperties", "maxProperties"]
            .contains(key), integer(item) == nil { throw Failure.unsupportedSchema }
        if key == "required" {
            guard case let .array(names) = item,
                  names.allSatisfy({ if case .string = $0 { true } else { false } })
            else { throw Failure.unsupportedSchema }
        }
        if key == "enum", case .array = item {} else if key == "enum" {
            throw Failure.unsupportedSchema
        }
        if key == "uniqueItems", case .bool = item {} else if key == "uniqueItems" {
            throw Failure.unsupportedSchema
        }
        if key == "type", !validTypes(item) { throw Failure.unsupportedSchema }
    }

    static func validTypes(_ value: DecodedJSONValue) -> Bool {
        let names = Set(["object", "array", "string", "integer", "number", "boolean", "null"])
        if case let .string(name) = value { return names.contains(name) }
        if case let .array(types) = value {
            return !types.isEmpty && types.allSatisfy {
                if case let .string(name) = $0 { names.contains(name) } else { false }
            }
        }
        return false
    }

}
