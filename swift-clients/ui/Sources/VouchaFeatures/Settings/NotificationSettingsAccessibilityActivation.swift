import SwiftUI

#if canImport(AppKit)
    import AppKit
#elseif canImport(UIKit)
    import UIKit
#endif

public final class NotificationA11yActivator {
    public private(set) var announcementCount = 0
    private var lastActivationToken: Int?
    private let announce: (String) -> Void
    private let activationObserver: ((String) -> Void)?

    public init(
        announce: @escaping (String) -> Void = NotificationA11yActivator.postNativeAnnouncement,
        activationObserver: ((String) -> Void)? = nil
    ) {
        self.announce = announce
        self.activationObserver = activationObserver
    }

    @MainActor
    public func activate(token: Int, focus: () -> Void, announcement: String) {
        guard lastActivationToken != token else { return }
        lastActivationToken = token
        focus()
        announcementCount += 1
        announce(announcement)
        activationObserver?(announcement)
    }

    public static func postNativeAnnouncement(_ message: String) {
        DispatchQueue.main.async {
            #if canImport(AppKit)
                NSAccessibility.post(
                    element: NSApp as Any,
                    notification: .announcementRequested,
                    userInfo: [.announcement: message]
                )
            #elseif canImport(UIKit)
                UIAccessibility.post(notification: .announcement, argument: message)
            #endif
        }
    }
}
