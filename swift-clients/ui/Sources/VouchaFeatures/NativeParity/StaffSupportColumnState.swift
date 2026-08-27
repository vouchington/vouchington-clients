import Observation
import SwiftUI

@Observable
@MainActor
final class StaffSupportColumnState {
    var visibility: NavigationSplitViewVisibility = .all

    func update(for horizontalSizeClass: UserInterfaceSizeClass?, hasDetailSelection: Bool) {
        if horizontalSizeClass == .compact, hasDetailSelection {
            visibility = .detailOnly
        } else {
            visibility = .all
        }
    }
}
