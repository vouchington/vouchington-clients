import Foundation
import VouchaAPI
import VouchaModels

struct SpendingCategoryDraft: Equatable {
    var amountText: String
    var currency: String
    var frequency: SpendingFrequency
    var note: String
    private var locale: Locale
    private var amountTextLocale: Locale
    private var amountTextWasEdited = false

    init(
        amount: Money? = nil,
        currency: String = "usd",
        frequency: SpendingFrequency = .monthly,
        note: String? = nil,
        locale: Locale = .current
    ) {
        amountText = amount.map { Self.format($0, locale: locale) } ?? ""
        self.currency = amount?.currency ?? currency
        self.frequency = frequency
        self.note = note ?? ""
        self.locale = locale
        amountTextLocale = locale
    }

    init(category: SpendingCategory, locale: Locale = .current) {
        self.init(
            amount: category.amount,
            frequency: category.spendingFrequency,
            note: category.note,
            locale: locale
        )
    }

    func createBody(spendingCategoryId: String) throws -> CreateSpendingCategoryBody {
        try .init(
            spendingCategoryId: spendingCategoryId,
            amount: parsedAmount(),
            spendingFrequency: frequency,
            note: normalizedNote
        )
    }

    func updateBody(comparedWith category: SpendingCategory) throws -> UpdateSpendingCategoryBody? {
        let amount = try parsedAmount()
        let notePatch: NullableValue<String>? = normalizedNote == category.note ? nil : normalizedNote
            .map(NullableValue.value) ?? .null
        let amountPatch = amount == category.amount ? nil : amount
        let frequencyPatch = frequency == category.spendingFrequency ? nil : frequency
        guard amountPatch != nil || frequencyPatch != nil || notePatch != nil else { return nil }
        return .init(amount: amountPatch, spendingFrequency: frequencyPatch, note: notePatch)
    }

    static func format(_ value: Money, locale: Locale) -> String {
        guard let exponent = Currency.minorUnitExponent(for: value.currency) else { return "" }
        let majorUnits = value.majorUnitDecimal(minorUnitExponent: exponent)
        return format(majorUnits, locale: locale)
    }

    mutating func applyLocale(_ locale: Locale) {
        guard self.locale != locale else { return }
        self.locale = locale
        guard !amountTextWasEdited,
              let amount = Self.parseDecimal(amountText, locale: amountTextLocale)
        else { return }
        amountText = Self.format(amount, locale: locale)
        amountTextLocale = locale
    }

    mutating func updateAmountText(_ value: String) {
        amountTextWasEdited = true
        amountTextLocale = locale
        amountText = value
    }

    private var normalizedNote: String? {
        note.isEmpty ? nil : note
    }

    private func parsedAmount() throws -> Money {
        guard let exponent = Currency.minorUnitExponent(for: currency),
              let decimal = Self.parseDecimal(amountText, locale: amountTextLocale),
              decimal >= 0
        else { throw SpendingCategoryDraftError.invalidAmount }
        let scaleFactor = Self.powerOfTen(exponent)
        guard decimal <= Decimal(Money.maximumAmount) / scaleFactor else {
            throw SpendingCategoryDraftError.invalidAmount
        }
        let minorUnits = decimal * scaleFactor
        let amount = NSDecimalNumber(decimal: minorUnits).int64Value
        guard Decimal(amount) == minorUnits else {
            throw SpendingCategoryDraftError.invalidAmount
        }
        do {
            return try Money(amount: amount, currency: currency)
        } catch {
            throw SpendingCategoryDraftError.invalidAmount
        }
    }

    private static func parseDecimal(_ amountText: String, locale: Locale) -> Decimal? {
        let trimmed = amountText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        let formatter = decimalFormatter(locale: locale)
        guard hasCanonicalDecimalSyntax(trimmed, formatter: formatter) else { return nil }
        var object: AnyObject?
        var range = NSRange(location: 0, length: trimmed.utf16.count)
        do {
            try formatter.getObjectValue(&object, for: trimmed, range: &range)
        } catch {
            return nil
        }
        guard range == NSRange(location: 0, length: trimmed.utf16.count) else { return nil }
        return (object as? NSDecimalNumber)?.decimalValue
    }

    private static func hasCanonicalDecimalSyntax(_ value: String, formatter: NumberFormatter) -> Bool {
        var digits = value
        if let decimalSeparator = formatter.decimalSeparator {
            guard value.components(separatedBy: decimalSeparator).count <= 2 else { return false }
            digits = digits.replacingOccurrences(of: decimalSeparator, with: "")
        }
        if let groupingSeparator = formatter.groupingSeparator {
            let groupingSeparators = Self.whitespaceGroupingSeparators.contains(groupingSeparator) ?
                Self.whitespaceGroupingSeparators : [groupingSeparator]
            for separator in groupingSeparators {
                digits = digits.replacingOccurrences(of: separator, with: "")
            }
        }
        return !digits.isEmpty && digits.unicodeScalars.allSatisfy(CharacterSet.decimalDigits.contains)
    }

    private static let whitespaceGroupingSeparators = [" ", "\u{00A0}", "\u{202F}"]

    private static func format(_ value: Decimal, locale: Locale) -> String {
        decimalFormatter(locale: locale)
            .string(from: NSDecimalNumber(decimal: value)) ?? NSDecimalNumber(decimal: value).stringValue
    }

    private static func powerOfTen(_ exponent: Int) -> Decimal {
        (0 ..< exponent).reduce(1) { value, _ in value * 10 }
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

enum SpendingCategoryDraftError: Error { case invalidAmount }
