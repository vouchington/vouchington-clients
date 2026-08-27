import Foundation
import VouchaFeatures

extension RootView {
    func applyNativeRouteSelection(
        entry: NativeRouteCatalogEntry,
        match: NativeRouteMatch,
        destination: NativeRouteDestinationIdentifier,
        section: AppSection,
        url: URL
    ) {
        nativeRouteDispatchGeneration += 1
        selectedSection = section
        selectedSubsection = destination.verticalSubsection(for: match)
        selectedNativeRouteEntry = entry
        selectedNativeRouteMatch = match
        selectedNativeRouteQuery = nativeAppLinkRouteQuery(url)
    }

    func revealNativeSectionIfNeeded(_ section: AppSection) -> Bool {
        guard !visibleSections.contains(section) else { return true }
        sectionPreferences.setHidden(section, hidden: false)
        sectionPreferences.normalize()
        return visibleSections.contains(section)
    }
}
