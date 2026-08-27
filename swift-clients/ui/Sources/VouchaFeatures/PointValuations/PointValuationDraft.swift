import Foundation
import VouchaAPI
import VouchaModels

struct PointValuationDraft: Equatable {
    private static let scaleFactor = pow(Decimal(10), ScaledMoney.scale)

    var valuePerPointText: String
    var note: String
    var currency: String
    private let locale: Locale

    init(valuePerPoint: ScaledMoney? = nil, note: String? = nil, locale: Locale = .current) {
        valuePerPointText = valuePerPoint.map { Self.format($0, locale: locale) } ?? ""
        self.note = note ?? ""
        self.locale = locale
        currency = valuePerPoint?.currency ?? "usd"
    }

    init(valuation: PointValuation, locale: Locale = .current) {
        self.init(valuePerPoint: valuation.valuePerPoint, note: valuation.note, locale: locale)
    }

    func createBody(rewardsProgramId: String) throws -> CreatePointValuationBody {
        try .init(
            rewardsProgramId: rewardsProgramId,
            valuePerPoint: parsedValue(),
            note: note.isEmpty ? nil : note
        )
    }

    func updateBody(comparedWith valuation: PointValuation) throws -> UpdatePointValuationBody? {
        let value = try parsedValue()
        let valuePatch = value == valuation.valuePerPoint ? nil : value
        let normalizedNote = note.isEmpty ? nil : note
        let notePatch: NullableValue<String>? = if normalizedNote == valuation.note {
            nil
        } else {
            normalizedNote.map(NullableValue.value) ?? .null
        }
        guard valuePatch != nil || notePatch != nil else { return nil }
        return .init(valuePerPoint: valuePatch, note: notePatch)
    }

    static func format(_ value: ScaledMoney, locale: Locale) -> String {
        let majorUnits = value.majorUnitDecimal
        return decimalFormatter(locale: locale).string(from: NSDecimalNumber(decimal: majorUnits)) ??
            NSDecimalNumber(decimal: majorUnits).stringValue
    }

    private func parsedValue() throws -> ScaledMoney {
        let trimmed = valuePerPointText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { throw PointValuationDraftError.invalidValuePerPoint }
        let formatter = Self.decimalFormatter(locale: locale)
        guard Self.hasAtMostScaleFractionDigits(trimmed, formatter: formatter) else {
            throw PointValuationDraftError.invalidValuePerPoint
        }
        var parsedValue: AnyObject?
        var parsedRange = NSRange(location: 0, length: trimmed.utf16.count)
        try formatter.getObjectValue(&parsedValue, for: trimmed, range: &parsedRange)
        guard parsedRange == NSRange(location: 0, length: trimmed.utf16.count),
              let decimal = (parsedValue as? NSDecimalNumber)?.decimalValue,
              decimal >= 0,
              decimal <= Decimal(PointValuation.maximumValuePerPointAmount) / Self.scaleFactor
        else { throw PointValuationDraftError.invalidValuePerPoint }
        let scaled = decimal * Self.scaleFactor
        let amount = NSDecimalNumber(decimal: scaled).int64Value
        guard Decimal(amount) == scaled else {
            throw PointValuationDraftError.invalidValuePerPoint
        }
        return try ScaledMoney(
            amount: amount,
            currency: currency
        )
    }

    private static func hasAtMostScaleFractionDigits(
        _ value: String,
        formatter: NumberFormatter
    ) -> Bool {
        guard let decimalSeparator = formatter.decimalSeparator,
              let separatorRange = value.range(of: decimalSeparator)
        else { return true }
        let fractionalPart = value[separatorRange.upperBound...]
        return fractionalPart.range(of: decimalSeparator) == nil &&
            fractionalPart.count <= ScaledMoney.scale
    }

    private static func decimalFormatter(locale: Locale) -> NumberFormatter {
        let formatter = NumberFormatter()
        formatter.locale = locale
        formatter.numberStyle = .decimal
        formatter.generatesDecimalNumbers = true
        formatter.minimumFractionDigits = 0
        formatter.maximumFractionDigits = 38
        return formatter
    }
}

enum PointValuationDraftError: Error {
    case invalidValuePerPoint
}
