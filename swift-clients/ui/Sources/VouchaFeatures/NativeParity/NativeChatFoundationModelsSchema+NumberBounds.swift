import Foundation

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema.Projection {
        func projectNumber(_ fields: [String: Any]) throws -> DynamicGenerationSchema {
            try NativeChatFoundationModelsSchema.validateKeywords(
                fields, allowing: [
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
            let (minimum, maximum) = try projectedNumberBounds(fields)
            return .init(type: Decimal.self, guides: [.minimum(minimum), .maximum(maximum)])
        }

        func projectedNumberBounds(_ fields: [String: Any]) throws -> (Decimal, Decimal) {
            let bounds = try NativeChatFoundationModelsSchema.numericBounds(fields)
            // GeneratedContent serializes Double numbers; keep guides inside its JSON-safe range.
            let safe = Decimal(9_007_199_254_740_991)
            let rawMinimum = Swift.max(bounds.minimum ?? -safe, -safe)
            let rawMaximum = Swift.min(bounds.maximum ?? safe, safe)
            guard rawMinimum <= rawMaximum else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            let minimum = try serializedBoundary(rawMinimum, isMinimum: true)
            let maximum = try serializedBoundary(rawMaximum, isMinimum: false)
            guard minimum <= maximum else { throw NativeChatFoundationModelsSchema.Failure.unsupported }
            return (minimum, maximum)
        }

        private func serializedBoundary(_ bound: Decimal, isMinimum: Bool) throws -> Decimal {
            let approximate = NSDecimalNumber(decimal: bound).doubleValue
            var serializable = try serializedDecimal(approximate)
            if isMinimum, serializable < bound {
                serializable = try serializedDecimal(approximate.nextUp)
            } else if !isMinimum, serializable > bound {
                serializable = try serializedDecimal(approximate.nextDown)
            }
            return serializable
        }

        private func serializedDecimal(_ value: Double) throws -> Decimal {
            guard value.isFinite, abs(value) <= 9_007_199_254_740_991,
                  let data = try? JSONSerialization.data(withJSONObject: [value]),
                  let json = String(data: data, encoding: .utf8),
                  let decimal = Decimal(string: String(json.dropFirst().dropLast())) else {
                throw NativeChatFoundationModelsSchema.Failure.unsupported
            }
            return decimal
        }
    }
#endif
