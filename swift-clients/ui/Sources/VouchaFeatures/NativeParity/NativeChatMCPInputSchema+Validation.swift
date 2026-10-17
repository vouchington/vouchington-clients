import Foundation
import VouchaModels

extension NativeChatMCPInputSchema {
    static func check(
        _ value: DecodedJSONValue, schema: DecodedJSONValue,
        root: DecodedJSONValue, depth: Int, remainingWork: inout Int, output: Bool = false
    ) throws -> Bool {
        guard remainingWork > 0, depth < 32, case let .object(fields) = schema else {
            throw Failure.unsupportedSchema
        }
        remainingWork -= 1
        guard try checkReference(
            value,
            fields: fields,
            root: root,
            depth: depth,
            remainingWork: &remainingWork,
            output: output
        ),
            try checkScalarAssertions(value, fields: fields, remainingWork: &remainingWork, output: output)
        else { return false }
        guard try checkCompositions(
            value,
            fields: fields,
            root: root,
            depth: depth,
            remainingWork: &remainingWork,
            output: output
        )
        else { return false }
        switch value {
        case let .object(object):
            return try checkObject(
                object,
                fields: fields,
                root: root,
                depth: depth,
                remainingWork: &remainingWork,
                output: output
            )
        case let .array(items):
            return try checkArray(
                items,
                fields: fields,
                root: root,
                depth: depth,
                remainingWork: &remainingWork,
                output: output
            )
        case let .string(text): return try checkString(text, fields: fields)
        default: return try output
            ? checkOutputNumber(value, fields: fields, remainingWork: &remainingWork)
            : checkNumber(value, fields: fields)
        }
    }

    static func checkReference(
        _ value: DecodedJSONValue, fields: [String: DecodedJSONValue],
        root: DecodedJSONValue, depth: Int, remainingWork: inout Int, output: Bool = false
    ) throws -> Bool {
        guard case let .string(path)? = fields["$ref"] else { return true }
        guard let name = localDefinitionName(from: path) else { throw Failure.unsupportedSchema }
        guard case let .object(rootFields) = root,
              case let .object(defs)? = rootFields["$defs"],
              let target = defs[name] else { throw Failure.unsupportedSchema }
        return try check(
            value,
            schema: target,
            root: root,
            depth: depth + 1,
            remainingWork: &remainingWork,
            output: output
        )
    }

    static func checkScalarAssertions(
        _ value: DecodedJSONValue, fields: [String: DecodedJSONValue],
        remainingWork: inout Int, output: Bool
    ) throws -> Bool {
        if let expected = fields["type"], !matchesType(value, expected: expected) { return false }
        if let constant = fields["const"],
           try !(output ? sameOutput(value, constant, remainingWork: &remainingWork) : same(value, constant)) {
            return false
        }
        if case let .array(choices)? = fields["enum"] {
            var matched = false
            for choice in choices
                where try output ? sameOutput(value, choice, remainingWork: &remainingWork) : same(value, choice) {
                matched = true
                break
            }
            if !matched { return false }
        }
        return true
    }

    static func checkCompositions(
        _ value: DecodedJSONValue, fields: [String: DecodedJSONValue],
        root: DecodedJSONValue, depth: Int, remainingWork: inout Int, output: Bool = false
    ) throws -> Bool {
        for keyword in ["allOf", "anyOf", "oneOf"] {
            guard case let .array(branches)? = fields[keyword] else { continue }
            var matched = 0
            for branch in branches {
                let acceptsBranch = try check(
                    value, schema: branch, root: root, depth: depth + 1, remainingWork: &remainingWork, output: output
                )
                if acceptsBranch {
                    matched += 1
                }
            }
            if keyword == "allOf", matched != branches.count { return false }
            if keyword == "anyOf", matched == 0 { return false }
            if keyword == "oneOf", matched != 1 { return false }
        }
        return true
    }

    static func matchesType(_ value: DecodedJSONValue, expected: DecodedJSONValue) -> Bool {
        if case let .array(types) = expected { return types.contains { matchesType(value, expected: $0) } }
        guard case let .string(name) = expected else { return false }
        switch (name, value) {
        case ("null", .null), ("boolean", .bool), ("string", .string),
             ("array", .array), ("object", .object): return true
        case ("number", .number), ("number", .integer), ("number", .unsignedInteger), ("number", .decimal),
             ("integer", .integer), ("integer", .unsignedInteger): return true
        case let ("integer", .number(number)): return number.isFinite && number.rounded() == number
        case let ("integer", .decimal(number)):
            var value = number
            var rounded = Decimal()
            NSDecimalRound(&rounded, &value, 0, .plain)
            return rounded == number
        default: return false
        }
    }

}
