import Foundation
import VouchaModels

struct NativeChatFinancialIncomeRange: Equatable {
    let minimum: Money
    /// Nil for an open-ended range (the producer sends `maximum: null`).
    let maximum: Money?
}

/// Only the member's own displayable fields. `individual_id`, `currency` and `updated_at` are never decoded.
struct NativeChatFinancialProfileFact: Equatable {
    let creditScoreRange: String?
    let statedIncomeRange: NativeChatFinancialIncomeRange?
    let totalCreditLimit: Money?
    let yearsOfCreditHistory: Double?
    let hardInquiries12m: Double?
    let cardsOpened24m: Double?
}

extension NativeChatMCPFact {
    private static let creditScoreRanges: Set = ["300-579", "580-669", "670-739", "740-799", "800-850"]

    static func makeFinancialProfile(_ value: DecodedJSONValue) -> Self? {
        guard let envelope = decodeContent(FinancialEnvelope.self, value), envelope.success else { return nil }
        guard let row = envelope.result.financialProfile else { return .financialProfile(nil) }
        let counts = [row.yearsOfCreditHistory, row.hardInquiries12m, row.cardsOpened24m]
        guard counts.allSatisfy({ $0.map { $0.isFinite && $0 >= 0 } ?? true }),
              row.creditScoreRange.map(creditScoreRanges.contains) ?? true,
              row.statedIncomeRange.map(validIncome) ?? true,
              row.totalCreditLimit.map(known) ?? true else { return nil }
        let fact = NativeChatFinancialProfileFact(
            creditScoreRange: row.creditScoreRange,
            statedIncomeRange: row.statedIncomeRange.map { .init(minimum: $0.minimum, maximum: $0.maximum) },
            totalCreditLimit: row.totalCreditLimit,
            yearsOfCreditHistory: row.yearsOfCreditHistory,
            hardInquiries12m: row.hardInquiries12m,
            cardsOpened24m: row.cardsOpened24m
        )
        return .financialProfile(fact.isEmpty ? nil : fact)
    }

    private static func validIncome(_ range: IncomeRow) -> Bool {
        known(range.minimum) && (range.maximum.map { known($0) && range.minimum.amount <= $0.amount } ?? true)
    }

    private static func known(_ money: Money) -> Bool {
        money.knownCurrencyMajorUnitDecimal != nil
    }
}

extension NativeChatFinancialProfileFact {
    var isEmpty: Bool {
        creditScoreRange == nil && statedIncomeRange == nil && totalCreditLimit == nil
            && yearsOfCreditHistory == nil && hardInquiries12m == nil && cardsOpened24m == nil
    }
}

private struct FinancialEnvelope: Decodable {
    let success: Bool
    let result: FinancialResult
}

private struct FinancialResult: Decodable {
    let financialProfile: FinancialRow?
}

private struct FinancialRow: Decodable {
    let creditScoreRange: String?
    let statedIncomeRange: IncomeRow?
    let totalCreditLimit: Money?
    let yearsOfCreditHistory: Double?
    let hardInquiries12m: Double?
    let cardsOpened24m: Double?
}

private struct IncomeRow: Decodable {
    let minimum: Money
    let maximum: Money?
}
