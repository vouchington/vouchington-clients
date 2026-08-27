import Foundation
import VouchaAPI
import VouchaModels

struct PaymentCardDraft: Equatable {
    var openedOn: LocalDate?
    var closedOn: LocalDate?
    var receivedSignUpBonusOn: LocalDate?
    var creditLimitText: String
    var creditLimitCurrency: String
    var isAuthorizedUser: Bool
    var authorizedUserOfId: String?
    var note: String
    private let creditLimitLocale: Locale

    init(card: PaymentCard, locale: Locale = .current) {
        openedOn = card.openedOn
        closedOn = card.closedOn
        receivedSignUpBonusOn = card.receivedSignUpBonusOn
        creditLimitText = card.creditLimit.map { Self.formatCreditLimit($0, locale: locale) } ?? ""
        isAuthorizedUser = card.isAuthorizedUser
        authorizedUserOfId = card.authorizedUserOfId
        note = card.note ?? ""
        creditLimitLocale = locale
        creditLimitCurrency = card.creditLimit?.currency ?? "usd"
    }

    func updateBody(comparedWith card: PaymentCard) throws -> UpdatePaymentCardBody? {
        let creditLimit = try parsedCreditLimit()
        let normalizedParent = isAuthorizedUser ? authorizedUserOfId : nil
        let body = UpdatePaymentCardBody(
            openedOn: patch(openedOn, comparedWith: card.openedOn),
            closedOn: patch(closedOn, comparedWith: card.closedOn),
            receivedSignUpBonusOn: patch(receivedSignUpBonusOn, comparedWith: card.receivedSignUpBonusOn),
            creditLimit: patch(creditLimit, comparedWith: card.creditLimit),
            isAuthorizedUser: isAuthorizedUser == card.isAuthorizedUser ? nil : isAuthorizedUser,
            authorizedUserOfId: patch(normalizedParent, comparedWith: card.authorizedUserOfId),
            note: patch(note.isEmpty ? nil : note, comparedWith: card.note)
        )
        return body.hasChanges ? body : nil
    }

    mutating func synchronizeAuthorizedUserParent(
        from previousParentId: String?, to currentParentId: String?
    ) {
        guard authorizedUserOfId == previousParentId else { return }
        authorizedUserOfId = currentParentId
    }

    private func parsedCreditLimit() throws -> Money? {
        let trimmed = creditLimitText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        let formatter = Self.decimalFormatter(locale: creditLimitLocale)
        var parsedValue: AnyObject?
        var parsedRange = NSRange(location: 0, length: trimmed.utf16.count)
        try formatter.getObjectValue(&parsedValue, for: trimmed, range: &parsedRange)
        guard parsedRange == NSRange(location: 0, length: trimmed.utf16.count),
              let decimal = (parsedValue as? NSDecimalNumber)?.decimalValue,
              decimal >= 0
        else { throw PaymentCardDraftError.invalidCreditLimit }
        guard let exponent = Currency.minorUnitExponent(for: creditLimitCurrency) else {
            throw PaymentCardDraftError.invalidCreditLimit
        }
        let minorUnits = decimal * Self.powerOfTen(exponent)
        let amount = NSDecimalNumber(decimal: minorUnits).int64Value
        guard Decimal(amount) == minorUnits,
              minorUnits <= Decimal(Money.maximumAmount)
        else { throw PaymentCardDraftError.invalidCreditLimit }
        return try Money(
            amount: amount,
            currency: creditLimitCurrency
        )
    }

    static func formatCreditLimit(_ value: Money, locale: Locale) -> String {
        let formatter = decimalFormatter(locale: locale)
        guard let exponent = Currency.minorUnitExponent(for: value.currency) else {
            return ""
        }
        let majorUnits = value.majorUnitDecimal(minorUnitExponent: exponent)
        return formatter.string(from: NSDecimalNumber(decimal: majorUnits)) ??
            NSDecimalNumber(decimal: majorUnits).stringValue
    }

    private static func powerOfTen(_ exponent: Int) -> Decimal {
        (0 ..< exponent).reduce(1) { value, _ in value * 10 }
    }

    private static func decimalFormatter(locale: Locale) -> NumberFormatter {
        let formatter = NumberFormatter()
        formatter.locale = locale
        formatter.numberStyle = .decimal
        formatter.generatesDecimalNumbers = true
        formatter.maximumFractionDigits = 38
        return formatter
    }

    private func patch<T: Encodable & Equatable & Sendable>(
        _ value: T?, comparedWith original: T?
    ) -> NullableValue<T>? {
        guard value != original else { return nil }
        return value.map(NullableValue.value) ?? .null
    }
}

enum PaymentCardDraftError: Error {
    case invalidCreditLimit
}

private extension UpdatePaymentCardBody {
    var hasChanges: Bool {
        openedOn != nil || closedOn != nil || receivedSignUpBonusOn != nil || creditLimit != nil ||
            isAuthorizedUser != nil || authorizedUserOfId != nil || note != nil
    }
}
