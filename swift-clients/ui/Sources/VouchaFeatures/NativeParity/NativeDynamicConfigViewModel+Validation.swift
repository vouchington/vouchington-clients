import Foundation
import VouchaAPI
import VouchaLocalization

extension NativeDynamicConfigViewModel {
    func validatedValue(_ draft: String, for field: DynamicConfigField) -> DynamicConfigValue? {
        setValidationError(nil, for: field.name)
        switch field.type {
        case .boolean:
            return nil
        case .string:
            return .string(draft)
        case .number:
            guard !draft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
                  let value = Double(draft), value.isFinite
            else {
                setValidationError(.message(.nativeSwiftDynamicConfigFeatureFlagsFiniteNumber), for: field.name)
                return nil
            }
            if field.integer == true, value.rounded() != value {
                setValidationError(.message(.nativeSwiftDynamicConfigFeatureFlagsWholeNumber), for: field.name)
                return nil
            }
            if let minimum = field.minValue, value < minimum {
                setValidationError(.message(
                    .nativeSwiftDynamicConfigFeatureFlagsMinimum,
                    numberParameters: ["value": minimum]
                ), for: field.name)
                return nil
            }
            if let maximum = field.maxValue, value > maximum {
                setValidationError(.message(
                    .nativeSwiftDynamicConfigFeatureFlagsMaximum,
                    numberParameters: ["value": maximum]
                ), for: field.name)
                return nil
            }
            return .number(value)
        }
    }
}
