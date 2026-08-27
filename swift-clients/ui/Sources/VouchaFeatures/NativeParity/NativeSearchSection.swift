import Foundation
import VouchaLocalization

public struct NativeSearchSection: Identifiable, Sendable {
    public let title: UiVerbatimText
    public let rows: [NativeRouteDestinationRow]

    public var id: String {
        UiMessages.string(title, locale: .english)
    }
}
