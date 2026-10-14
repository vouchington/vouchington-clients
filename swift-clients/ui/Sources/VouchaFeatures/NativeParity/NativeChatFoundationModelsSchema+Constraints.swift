import Foundation

#if canImport(FoundationModels)
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
    }
#endif
