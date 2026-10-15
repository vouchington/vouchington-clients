import Foundation

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema {
        static func validateConstant(_ fields: [String: Any]) throws {
            if let constant = fields["const"], !(constant is String) { throw Failure.unsupported }
        }

        static func validateStringConstraints(_ fields: [String: Any]) throws {
            if ["minLength", "maxLength", "pattern", "format"].contains(where: { fields[$0] != nil }) {
                throw Failure.unsupported
            }
        }

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

        static func validateKeywords(_ fields: [String: Any], allowing assertions: Set<String>) throws {
            let annotations: Set = [
                "title",
                "description",
                "default",
                "examples",
                "deprecated",
                "readOnly",
                "writeOnly"
            ]
            guard Set(fields.keys).isSubset(of: assertions.union(annotations)) else {
                throw Failure.unsupported
            }
        }

        static func numericBounds(_ fields: [String: Any]) throws -> (minimum: Decimal?, maximum: Decimal?) {
            guard fields["exclusiveMinimum"] == nil, fields["exclusiveMaximum"] == nil,
                  fields["enum"] == nil, fields["const"] == nil else { throw Failure.unsupported }
            let minimum = try decimal(fields["minimum"])
            let maximum = try decimal(fields["maximum"])
            if let minimum, let maximum, minimum > maximum { throw Failure.unsupported }
            return (minimum, maximum)
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
            guard Set(fields.keys).isSubset(of: ["type", "description", "title"]) else {
                throw NativeChatFoundationModelsSchema.Failure.unsupported
            }
            return try .init(name: name(), anyOf: types.map { try project(["type": $0]) })
        }

        mutating func projectString(_ fields: [String: Any]) throws -> DynamicGenerationSchema {
            if let enumValues = fields["enum"] {
                guard let values = enumValues as? [Any] else {
                    throw NativeChatFoundationModelsSchema.Failure.unsupported
                }
                let strings = values.compactMap { $0 as? String }
                guard !strings.isEmpty else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
                return .init(name: name(), anyOf: strings)
            }
            if let value = fields["const"] as? String { return .init(name: name(), anyOf: [value]) }
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
                    if let type = fields["type"] as? String {
                        guard choices.allSatisfy({ $0["type"] as? String == type }) else {
                            throw NativeChatFoundationModelsSchema.Failure.unsupported
                        }
                    }
                    if let types = fields["type"] as? [String] {
                        guard choices.allSatisfy({ branch in
                            guard let type = branch["type"] as? String else { return false }
                            return types.contains(type)
                        }) else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
                    }
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

        func projectInteger(_ fields: [String: Any]) throws -> DynamicGenerationSchema {
            try NativeChatFoundationModelsSchema.validateKeywords(
                fields, allowing: ["type", "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "$defs"]
            )
            let bounds = try NativeChatFoundationModelsSchema.numericBounds(fields)
            let safe = 9_007_199_254_740_991
            let minimum = try integerBound(bounds.minimum, clampedTo: -safe ... safe, isMinimum: true) ?? -safe
            let maximum = try integerBound(bounds.maximum, clampedTo: -safe ... safe, isMinimum: false) ?? safe
            guard minimum <= maximum else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            return .init(type: Int.self, guides: [.range(minimum ... maximum)])
        }

        private func integerBound(
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
