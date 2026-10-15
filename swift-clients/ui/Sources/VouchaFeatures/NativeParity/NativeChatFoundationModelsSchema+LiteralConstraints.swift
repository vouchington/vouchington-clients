import Foundation
import VouchaModels

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema {
        static func validateConstant(_ fields: [String: Any]) throws {
            if let constant = fields["const"], !(constant is String),
               !((fields["type"] == nil || fields["type"] as? String == "null") && constant is NSNull) {
                guard fields["type"] == nil || ["integer", "number"].contains(fields["type"] as? String ?? "") else {
                    throw Failure.unsupported
                }
                if fields["type"] as? String == "number" {
                    _ = try safeNumberConstant(constant)
                } else if fields["type"] == nil, (try? safeIntegerConstant(constant)) == nil {
                    _ = try safeNumberConstant(constant)
                } else {
                    _ = try safeIntegerConstant(constant)
                }
            }
        }

        static func validateStringConstraints(_ fields: [String: Any]) throws {
            var requiresFiniteDomain = ["maxLength", "pattern", "format"].contains { fields[$0] != nil }
            if let minimum = fields["minLength"] {
                guard let number = minimum as? NSNumber,
                      CFGetTypeID(number) != CFBooleanGetTypeID(),
                      let length = Int(exactly: number.doubleValue), length >= 0 else {
                    throw Failure.unsupported
                }
                requiresFiniteDomain = requiresFiniteDomain || length > 0
            }
            let declaredTypes = declaredTypes(in: fields)
            if !declaredTypes.isEmpty, !declaredTypes.contains("string") { return }
            guard requiresFiniteDomain else { return }
            guard fields["type"] == nil || fields["type"] as? String == "string" else {
                throw Failure.unsupported
            }
            let values: [String]
            if let constant = fields["const"] as? String {
                values = [constant]
            } else if let candidates = fields["enum"] as? [Any] {
                values = candidates.compactMap { $0 as? String }
            } else { throw Failure.unsupported }
            guard !values.isEmpty,
                  let data = try? JSONSerialization.data(withJSONObject: fields),
                  let schema = try? JSONDecoder().decode(DecodedJSONValue.self, from: data),
                  (try? NativeChatMCPInputSchema.validateSchema(schema)) != nil,
                  (try? values.allSatisfy({ try NativeChatMCPInputSchema.accepts(.string($0), schema: schema) })) ==
                  true
            else { throw Failure.unsupported }
        }

        static func safeIntegerConstant(_ constant: Any) throws -> Decimal {
            guard let number = constant as? NSNumber,
                  CFGetTypeID(number) != CFBooleanGetTypeID(),
                  let value = Decimal(string: number.stringValue),
                  value >= Decimal(-9_007_199_254_740_991),
                  value <= Decimal(9_007_199_254_740_991) else { throw Failure.unsupported }
            let integer = NSDecimalNumber(decimal: value).int64Value
            guard value == Decimal(integer) else { throw Failure.unsupported }
            return value
        }

        static func safeNumberConstant(_ constant: Any) throws -> Decimal {
            guard let number = constant as? NSNumber,
                  CFGetTypeID(number) != CFBooleanGetTypeID(),
                  let value = Decimal(string: number.stringValue),
                  let data = try? JSONSerialization.data(withJSONObject: [number.doubleValue]),
                  let json = String(data: data, encoding: .utf8),
                  let serialized = Decimal(string: String(json.dropFirst().dropLast())),
                  serialized == value,
                  value >= Decimal(-9_007_199_254_740_991),
                  value <= Decimal(9_007_199_254_740_991) else { throw Failure.unsupported }
            return value
        }
    }
#endif
