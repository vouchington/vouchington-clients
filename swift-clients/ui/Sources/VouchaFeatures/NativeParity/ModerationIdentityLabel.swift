import Foundation
import VouchaLocalization

func moderationIdentityLabel(displayName: String?, username: String?, locale: Locale) -> String {
    [displayName, username].compactMap { $0?.trimmedOrNil }.first
        ?? UiMessages.string(.nativeSwiftRouteSurfaceNotAvailable, locale: locale)
}
