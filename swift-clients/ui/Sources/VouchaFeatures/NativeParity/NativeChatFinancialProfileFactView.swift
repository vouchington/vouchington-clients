import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativeChatFinancialProfileFactView: View {
    let profile: NativeChatFinancialProfileFact?
    let locale: Locale

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            if let profile {
                if let range = profile.creditScoreRange {
                    row(.nativeMcpFactsFinancialProfileCreditScoreRange, .protocolValue(range))
                }
                if let income = profile.statedIncomeRange {
                    row(
                        .nativeMcpFactsFinancialProfileStatedIncomeRange,
                        income.maximum.map { .joined([money(income.minimum), money($0)], separator: " – ") }
                            ?? .joined([money(income.minimum), .protocolValue("+")], separator: "")
                    )
                }
                if let limit = profile.totalCreditLimit {
                    row(.nativeMcpFactsFinancialProfileTotalCreditLimit, money(limit))
                }
                count(.nativeMcpFactsFinancialProfileYearsOfCreditHistory, profile.yearsOfCreditHistory)
                count(.nativeMcpFactsFinancialProfileHardInquiries12m, profile.hardInquiries12m)
                count(.nativeMcpFactsFinancialProfileCardsOpened24m, profile.cardsOpened24m)
            } else {
                Text(UiMessages.string(.nativeMcpFactsFinancialProfileEmpty, locale: locale))
            }
        }
        .accessibilityIdentifier("native-chat-financial-profile-fact")
    }

    @ViewBuilder
    private func count(_ key: UiMessageKey, _ value: Double?) -> some View {
        if let value {
            row(key, .message(.nativeSwiftRouteSurfaceNumberValue, numberParameters: ["value": value]))
        }
    }

    private func row(_ key: UiMessageKey, _ value: UiVerbatimText) -> some View {
        NativeChatFactRow(label: key, value: value, locale: locale)
    }

    private func money(_ amount: Money) -> UiVerbatimText {
        .message(
            .nativeSwiftRouteSurfaceNumberValue,
            currencyParameters: [
                "value": .init(amount.knownCurrencyMajorUnitDecimal ?? 0, code: amount.currency.uppercased())
            ]
        )
    }
}
