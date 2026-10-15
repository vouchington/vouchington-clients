import Foundation

#if canImport(FoundationModels)
    import FoundationModels

    @available(macOS 26.4, iOS 26.4, *)
    extension NativeChatFoundationModelsSchema {
        static func validateConstant(_ fields: [String: Any]) throws {
            if let constant = fields["const"], !(constant is String),
               !((fields["type"] == nil || fields["type"] as? String == "null") && constant is NSNull) {
                guard fields["type"] as? String == "integer" else { throw Failure.unsupported }
                _ = try safeIntegerConstant(constant)
            }
        }

        static func validateStringConstraints(_ fields: [String: Any]) throws {
            if let minimum = fields["minLength"] {
                guard let number = minimum as? NSNumber,
                      CFGetTypeID(number) != CFBooleanGetTypeID(),
                      let length = Int(exactly: number.doubleValue), length >= 0 else {
                    throw Failure.unsupported
                }
                if length > 0 {
                    guard fields["type"] == nil || fields["type"] as? String == "string" else {
                        throw Failure.unsupported
                    }
                    let values: [String]
                    if let constant = fields["const"] as? String {
                        values = [constant]
                    } else if let candidates = fields["enum"] as? [String] {
                        values = candidates
                    } else { throw Failure.unsupported }
                    guard !values.isEmpty, values.allSatisfy({ $0.unicodeScalars.count >= length }) else {
                        throw Failure.unsupported
                    }
                }
            }
            if ["maxLength", "pattern", "format"].contains(where: { fields[$0] != nil }) {
                throw Failure.unsupported
            }
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
    }
#endif
