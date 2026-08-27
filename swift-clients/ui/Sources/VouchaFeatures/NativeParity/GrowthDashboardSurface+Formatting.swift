import Foundation
import VouchaLocalization
import VouchaModels

extension GrowthDashboardSurface {
    func appText(_ key: UiMessageKey) -> UiVerbatimText {
        .message(key)
    }

    func count(_ value: Int) -> UiVerbatimText {
        .message(.nativeSwiftRouteSurfaceNumberValue, numberParameters: ["value": Double(value)])
    }

    func count(_ value: Int?) -> UiVerbatimText {
        value.map(count) ?? appText(.nativeDotnetGrowthUnavailable)
    }

    func decimal(_ value: Double) -> UiVerbatimText {
        .message(.nativeSwiftRouteSurfaceNumberValue, numberParameters: ["value": value])
    }

    func percent(_ value: Double) -> UiVerbatimText {
        .message(.nativeSwiftRouteSurfaceNumberValue, percentParameters: ["value": value])
    }

    func percent(_ value: Double?) -> UiVerbatimText {
        value.map(percent) ?? appText(.nativeDotnetGrowthUnavailable)
    }

    func money(_ value: ScaledMoneyAggregate) -> UiVerbatimText {
        money(value, locale: nativeUiLocale)
    }

    func money(_ value: ScaledMoneyAggregate, locale: Locale) -> UiVerbatimText {
        ScaledMoneyAggregateFormatter.format(value, locale: locale)
    }

    func moneyList(_ values: [ScaledMoneyAggregate]) -> UiVerbatimText {
        values.isEmpty ? count(0) : .joined(values.map(money), separator: ", ")
    }
}
