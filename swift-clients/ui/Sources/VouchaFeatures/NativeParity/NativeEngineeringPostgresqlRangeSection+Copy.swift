import SwiftUI
import VouchaAPI
import VouchaLocalization

/// Typed copy for the two range families; the classifier and export-request keys are distinct.
struct NativePostgresqlRangeKeys {
    let fromDate: UiMessageKey
    let toDate: UiMessageKey
    let rows: UiMessageKey
    let invalid: UiMessageKey
    let create: UiMessageKey
    let retire: UiMessageKey
    let confirmCreate: UiMessageKey
    let confirmRetire: UiMessageKey

    static let classifier = Self(
        fromDate: UiMessageKey.engineeringClassifierRangeFromInclusiveUtc,
        toDate: UiMessageKey.engineeringClassifierRangeToExclusiveUtc,
        rows: UiMessageKey.engineeringClassifierRangeMaxDefaultRows,
        invalid: UiMessageKey.engineeringClassifierRangeInvalidRange,
        create: UiMessageKey.engineeringClassifierRangeCreate,
        retire: UiMessageKey.engineeringClassifierRangeRetire,
        confirmCreate: UiMessageKey.engineeringClassifierRangeConfirmCreate,
        confirmRetire: UiMessageKey.engineeringClassifierRangeConfirmRetire
    )

    static let request = Self(
        fromDate: UiMessageKey.engineeringRequestRangeFromInclusiveUtc,
        toDate: UiMessageKey.engineeringRequestRangeToExclusiveUtc,
        rows: UiMessageKey.engineeringRequestRangeMaxDefaultRows,
        invalid: UiMessageKey.engineeringRequestRangeInvalidRange,
        create: UiMessageKey.engineeringRequestRangeCreate,
        retire: UiMessageKey.engineeringRequestRangeRetire,
        confirmCreate: UiMessageKey.engineeringRequestRangeConfirmCreate,
        confirmRetire: UiMessageKey.engineeringRequestRangeConfirmRetire
    )
}

extension NativeEngineeringPostgresqlRangeSection {
    func keys(_ family: NativePostgresqlRangeFamily) -> NativePostgresqlRangeKeys {
        family == .classifier ? .classifier : .request
    }

    private var isRetireConfirmation: Bool {
        viewModel.rangeConfirmation?.type == .cleanupPartitions
    }

    var dialogRole: ButtonRole? {
        isRetireConfirmation ? .destructive : nil
    }

    var dialogTitle: String {
        guard let selection = viewModel.rangeConfirmation else { return "" }
        let keys = keys(selection.family)
        return UiMessages.string(isRetireConfirmation ? keys.retire : keys.create, locale: nativeUiLocale)
    }

    var dialogMessage: String {
        guard let selection = viewModel.rangeConfirmation else { return "" }
        let keys = keys(selection.family)
        return UiMessages.string(
            isRetireConfirmation ? keys.confirmRetire : keys.confirmCreate,
            parameters: [
                "fromDate": UiMessages.string(.verbatim(selection.range.fromDate), locale: nativeUiLocale),
                "toDate": UiMessages.string(.verbatim(selection.range.toDate), locale: nativeUiLocale)
            ],
            locale: nativeUiLocale
        )
    }
}
