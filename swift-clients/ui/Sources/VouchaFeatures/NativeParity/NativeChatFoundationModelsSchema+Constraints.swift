import Foundation

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema {
        static func isIntegerType(_ fields: [String: Any]) -> Bool {
            if fields["type"] as? String == "integer" { return true }
            return (fields["type"] as? [String])?.contains("integer") == true
        }

        static func validateSafeIntegerRange(_ fields: [String: Any]) throws {
            let safeLimit = Decimal(9_007_199_254_740_991)
            if let minimum = fields["minimum"] as? NSNumber {
                guard let value = Decimal(string: minimum.stringValue), value <= safeLimit else {
                    throw Failure.unsupported
                }
            }
            if let maximum = fields["maximum"] as? NSNumber {
                guard let value = Decimal(string: maximum.stringValue), value >= -safeLimit else {
                    throw Failure.unsupported
                }
            }
            if let minimum = fields["exclusiveMinimum"] as? NSNumber {
                guard let value = Decimal(string: minimum.stringValue), value < safeLimit else {
                    throw Failure.unsupported
                }
            }
            if let maximum = fields["exclusiveMaximum"] as? NSNumber {
                guard let value = Decimal(string: maximum.stringValue), value > -safeLimit else {
                    throw Failure.unsupported
                }
            }
        }

        static func numericBounds(_ fields: [String: Any]) throws -> (minimum: Decimal?, maximum: Decimal?) {
            guard fields["exclusiveMinimum"] == nil, fields["exclusiveMaximum"] == nil else {
                throw Failure.unsupported
            }
            if let constant = fields["const"] {
                guard fields["type"] == nil || ["integer", "number"].contains(fields["type"] as? String ?? ""),
                      fields["minimum"] == nil, fields["maximum"] == nil else { throw Failure.unsupported }
                let numberType = fields["type"] as? String == "number"
                let value = try numberType ? safeNumberConstant(constant) : safeIntegerConstant(constant)
                if let enumValues = fields["enum"] as? [Any],
                   !enumValues.contains(where: {
                       (try? (numberType ? safeNumberConstant($0) : safeIntegerConstant($0))) == value
                   }) {
                    throw Failure.unsupported
                }
                return (value, value)
            }
            guard fields["enum"] == nil else { throw Failure.unsupported }
            let minimum = try decimal(fields["minimum"])
            let maximum = try decimal(fields["maximum"])
            if let minimum, let maximum, minimum > maximum { throw Failure.unsupported }
            return (minimum, maximum)
        }

        static func projectsOnlyDeclaredProperties(_ fields: [String: Any]) -> Bool {
            guard let additional = fields["additionalProperties"] else { return true }
            if additional is Bool { return true }
            guard additional is [String: Any],
                  let properties = fields["properties"] as? [String: [String: Any]] else { return false }
            return !properties.isEmpty
        }

        static func projectableEnumValues(_ values: [Any], type: String?) -> [Any] {
            switch type {
            case "string": values.compactMap { $0 as? String }
            case "null": values.filter { $0 is NSNull }
            default: values
            }
        }

        private static func decimal(_ value: Any?) throws -> Decimal? {
            guard let value else { return nil }
            guard let number = value as? NSNumber,
                  let decimal = Decimal(string: number.stringValue) else { throw Failure.unsupported }
            return decimal
        }
    }

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema.Projection {
        mutating func projectTypeUnion(
            _ fields: [String: Any], types: [String]
        ) throws -> DynamicGenerationSchema {
            try NativeChatFoundationModelsSchema.validateKeywords(fields, allowing: ["type", "minLength"])
            return try .init(name: name(), anyOf: types.map { try project(["type": $0]) })
        }

        mutating func projectString(_ fields: [String: Any]) throws -> DynamicGenerationSchema {
            if let value = fields["const"] as? String {
                if let enumValues = fields["enum"] as? [Any],
                   !enumValues.compactMap({ $0 as? String }).contains(value) {
                    throw NativeChatFoundationModelsSchema.Failure.unsupported
                }
                return .init(name: name(), anyOf: [value])
            }
            if let enumValues = fields["enum"] {
                guard let values = enumValues as? [Any] else {
                    throw NativeChatFoundationModelsSchema.Failure.unsupported
                }
                let strings = values.compactMap { $0 as? String }
                guard !strings.isEmpty else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
                return .init(name: name(), anyOf: strings)
            }
            return .init(type: String.self)
        }

        mutating func projectReferenceOrChoices(_ fields: [String: Any]) throws -> DynamicGenerationSchema? {
            if let path = fields["$ref"] as? String {
                try NativeChatFoundationModelsSchema.validateKeywords(fields, allowing: ["$ref", "$defs"])
                guard path.hasPrefix("#/$defs/") else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
                return .init(referenceTo: String(path.dropFirst(8)))
            }
            for key in ["anyOf", "oneOf"] {
                if let choices = fields[key] as? [[String: Any]] {
                    try NativeChatFoundationModelsSchema.validateKeywords(fields, allowing: [key, "type", "$defs"])
                    if key == "oneOf", !NativeChatFoundationModelsSchema.branchesAreDisjoint(choices) {
                        throw NativeChatFoundationModelsSchema.Failure.unsupported
                    }
                    try validateChoiceSiblingTypes(choices, fields: fields)
                    return try .init(name: name(), anyOf: choices.map { try project($0) })
                }
            }
            return nil
        }

        func validateObjectBounds(_ fields: [String: Any], requiredCount: Int, propertyCount: Int) throws {
            guard (fields["minProperties"] as? Int ?? 0) <= requiredCount,
                  (fields["maxProperties"] as? Int ?? propertyCount) >= propertyCount else {
                throw NativeChatFoundationModelsSchema.Failure.unsupported
            }
        }

        func projectedObjectProperties(_ fields: [String: Any]) -> [String: [String: Any]] {
            guard fields["maxProperties"] as? Int != 0 else { return [:] }
            return fields["properties"] as? [String: [String: Any]] ?? [:]
        }

        func projectInteger(_ fields: [String: Any]) throws -> DynamicGenerationSchema {
            try NativeChatFoundationModelsSchema.validateKeywords(
                fields,
                allowing: [
                    "type",
                    "const",
                    "enum",
                    "minimum",
                    "maximum",
                    "exclusiveMinimum",
                    "exclusiveMaximum",
                    "$defs"
                ]
            )
            let (minimum, maximum) = try projectedIntegerBounds(fields)
            return .init(type: Int.self, guides: [.range(minimum ... maximum)])
        }

        func projectedIntegerBounds(_ fields: [String: Any]) throws -> (Int, Int) {
            let bounds = try NativeChatFoundationModelsSchema.numericBounds(fields)
            let safe = 9_007_199_254_740_991
            let minimum = try integerBound(bounds.minimum, clampedTo: -safe ... safe, isMinimum: true) ?? -safe
            let maximum = try integerBound(bounds.maximum, clampedTo: -safe ... safe, isMinimum: false) ?? safe
            guard minimum <= maximum else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            return (minimum, maximum)
        }

        func integerBound(
            _ value: Decimal?, clampedTo safe: ClosedRange<Int>, isMinimum: Bool
        ) throws -> Int? {
            guard let value else { return nil }
            if value < Decimal(safe.lowerBound) { return safe.lowerBound }
            if value > Decimal(safe.upperBound) { return safe.upperBound }
            let integer = NSDecimalNumber(decimal: value).int64Value
            let candidate = Decimal(integer)
            if isMinimum, candidate < value { return Int(integer + 1) }
            if !isMinimum, candidate > value { return Int(integer - 1) }
            return Int(integer)
        }

    }
#endif
