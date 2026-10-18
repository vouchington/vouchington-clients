import Foundation

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema.Projection {
        /// Lower bound on the serialized JSON the model must emit for required values.
        func minimumOutputBytes(
            _ fields: [String: Any], definitions: [String: [String: Any]]
        ) throws -> Int {
            try minimumOutputBytes(fields, definitions: definitions, activeReferences: [])
        }

        private func minimumOutputBytes(
            _ fields: [String: Any],
            definitions: [String: [String: Any]],
            activeReferences: Set<String>
        ) throws -> Int {
            if let path = fields["$ref"] as? String {
                guard path.hasPrefix("#/$defs/") else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
                let key = String(path.dropFirst(8))
                guard let target = definitions[key], !activeReferences.contains(key), activeReferences.count < 31 else {
                    throw NativeChatFoundationModelsSchema.Failure.unsupported
                }
                return try minimumOutputBytes(
                    target, definitions: definitions, activeReferences: activeReferences.union([key])
                )
            }
            for choice in ["anyOf", "oneOf"] {
                if let branches = fields[choice] as? [[String: Any]] {
                    return try minimumChoiceBytes(
                        branches,
                        definitions: definitions,
                        activeReferences: activeReferences
                    )
                }
            }
            if let types = fields["type"] as? [String] {
                return try minimumChoiceBytes(
                    types.map { ["type": $0] }, definitions: definitions, activeReferences: activeReferences
                )
            }
            switch fields["type"] as? String ?? NativeChatFoundationModelsSchema.inferredType(fields) {
            case "array": return try minimumArrayBytes(
                    fields,
                    definitions: definitions,
                    activeReferences: activeReferences
                )
            case "object": return try minimumObjectBytes(
                    fields,
                    definitions: definitions,
                    activeReferences: activeReferences
                )
            default: return try minimumScalarBytes(fields)
            }
        }

        private func minimumChoiceBytes(
            _ branches: [[String: Any]],
            definitions: [String: [String: Any]],
            activeReferences: Set<String>
        ) throws -> Int {
            guard !branches.isEmpty else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            return try branches.map {
                try minimumOutputBytes($0, definitions: definitions, activeReferences: activeReferences)
            }.min() ?? 1
        }

        private func minimumArrayBytes(
            _ fields: [String: Any],
            definitions: [String: [String: Any]],
            activeReferences: Set<String>
        ) throws -> Int {
            guard fields["items"] == nil || fields["items"] is [String: Any] else {
                throw NativeChatFoundationModelsSchema.Failure.unsupported
            }
            let item = fields["items"] as? [String: Any] ?? [:]
            let minimum = fields["minItems"] as? Int ?? 0
            guard (0 ... 64).contains(minimum) else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            if minimum == 0 { return 2 }
            let itemBytes = try minimumOutputBytes(
                item, definitions: definitions, activeReferences: activeReferences
            )
            guard itemBytes <= (4_096 - minimum - 1) / minimum else {
                throw NativeChatFoundationModelsSchema.Failure.unsupported
            }
            return 2 + minimum * itemBytes + minimum - 1
        }

        private func minimumObjectBytes(
            _ fields: [String: Any],
            definitions: [String: [String: Any]],
            activeReferences: Set<String>
        ) throws -> Int {
            let properties = fields["properties"] as? [String: [String: Any]] ?? [:]
            let propertiesByName = NativeChatFoundationModelsSchema.exactNameMap(properties)
            let required = Set((fields["required"] as? [String] ?? []).map { Data($0.utf8) })
            var total = 2
            for keyData in required {
                guard let property = propertiesByName[keyData],
                      let key = String(data: keyData, encoding: .utf8),
                      let serializedKey = try? JSONSerialization.data(withJSONObject: key, options: .fragmentsAllowed)
                else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
                let valueBytes = try minimumOutputBytes(
                    property, definitions: definitions, activeReferences: activeReferences
                )
                total += serializedKey.count + 1 + valueBytes + (total == 2 ? 0 : 1)
                guard total <= 4_096 else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            }
            return total
        }

        func minimumScalarBytes(_ fields: [String: Any]) throws -> Int {
            if let value = fields["const"] as? String { return try serializedBytes(value) }
            if fields["const"] is NSNull { return 4 }
            if let value = fields["const"] as? NSNumber { return try serializedBytes(value) }
            if let values = fields["enum"] as? [Any] {
                let projectable = NativeChatFoundationModelsSchema.projectableEnumValues(
                    values, type: fields["type"] as? String
                )
                guard !projectable.isEmpty else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
                return try projectable.map { try serializedBytes($0) }.min() ?? 1
            }
            if let numericBytes = try minimumNumericBytes(fields) { return numericBytes }
            switch fields["type"] as? String {
            case "string": return 2
            case "boolean", "null": return 4
            default: return 1
            }
        }

        private func minimumNumericBytes(_ fields: [String: Any]) throws -> Int? {
            if fields["type"] as? String ?? NativeChatFoundationModelsSchema.inferredType(fields) == "integer" {
                let (minimum, maximum) = try projectedIntegerBounds(fields)
                let closest = minimum > 0 ? minimum : (maximum < 0 ? maximum : 0)
                return try serializedBytes(NSNumber(value: closest))
            }
            if fields["type"] as? String == "number" {
                let (projectedMinimum, projectedMaximum) = try projectedNumberBounds(fields)
                if let widths = try serializedNumberWidths(
                    from: projectedMinimum, through: projectedMaximum
                ) {
                    return widths.minimum
                }
                let bounds = try NativeChatFoundationModelsSchema.numericBounds(fields)
                let wide = Decimal(100_000_000_000_000)
                if let minimum = bounds.minimum, minimum >= wide { return 4 }
                if let maximum = bounds.maximum, maximum <= -wide { return 5 }
            }
            return nil
        }

        func serializedNumberWidths(
            from minimum: Decimal, through maximum: Decimal
        ) throws -> (minimum: Int, maximum: Int)? {
            var value = NSDecimalNumber(decimal: minimum).doubleValue
            let upper = NSDecimalNumber(decimal: maximum).doubleValue
            var minimumBytes = Int.max
            var maximumBytes = 0
            for _ in 0 ..< 512 {
                if value > upper { return (minimumBytes, maximumBytes) }
                guard let data = try? JSONSerialization.data(withJSONObject: [value]) else {
                    throw NativeChatFoundationModelsSchema.Failure.unsupported
                }
                minimumBytes = Swift.min(minimumBytes, data.count - 2)
                maximumBytes = Swift.max(maximumBytes, data.count - 2)
                value = value.nextUp
            }
            return value > upper ? (minimumBytes, maximumBytes) : nil
        }

        func serializedBytes(_ value: Any) throws -> Int {
            guard let data = try? JSONSerialization.data(withJSONObject: value, options: .fragmentsAllowed) else {
                throw NativeChatFoundationModelsSchema.Failure.unsupported
            }
            return data.count
        }

    }
#endif
