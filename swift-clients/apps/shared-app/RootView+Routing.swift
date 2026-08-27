import SwiftUI
import VouchaFeatures

extension RootView {
    var visibleSections: [AppSection] {
        let sections = sectionPreferences.visibleSections
        let isSignedIn = viewModelFactory.sessionManager.isSignedIn
        let userRoles = viewModelFactory.sessionManager.currentUserRoles
        guard isSignedIn else {
            let publicSections = sections.filter { $0.isVisible(isSignedIn: false, userRoles: []) }
            if !publicSections.isEmpty {
                return publicSections
            }
            return [
                sectionPreferences.orderedSections.first { $0.isVisible(isSignedIn: false, userRoles: []) } ?? .news
            ]
        }
        let visibleSignedInSections = sections.filter { $0.isVisible(isSignedIn: isSignedIn, userRoles: userRoles) }
        if !visibleSignedInSections.isEmpty {
            return visibleSignedInSections
        }
        return [
            sectionPreferences.orderedSections.first { $0.isVisible(isSignedIn: isSignedIn, userRoles: userRoles) }
                ?? .news
        ]
    }

    func repairSelectedSection() {
        if let selectedSection, visibleSections.contains(selectedSection) {
            return
        }
        selectedSection = visibleSections.first
    }

    func routeNativeURL(_ url: URL) {
        let pathAndQuery = urlNativeRoutePathAndQuery(url)
        let parityChecker = NativeRouteParityChecker()
        guard case let .included(destination, _, _) = parityChecker.resolution(for: pathAndQuery),
              let route = NativeRouteCatalog.matchingRoute(for: pathAndQuery),
              route.entry.destinationIdentifier == destination
        else {
            return
        }

        let isSignedIn = viewModelFactory.sessionManager.isSignedIn

        guard let section = destination.nativeSection else {
            if destination == .signIn, !isSignedIn {
                applySignInLinkQuery(from: url)
                showingSignIn = true
            }
            return
        }

        let userRoles = viewModelFactory.sessionManager.currentUserRoles
        if destination.requiresAuthentication(for: route.match), !isSignedIn {
            pendingNativeRouteURL = url
            showingSignIn = true
            return
        }

        guard section.isVisible(isSignedIn: isSignedIn, userRoles: userRoles) else {
            return
        }
        guard allowsNativeRouteFeature(
            destination: destination,
            routeMatch: route.match,
            url: url
        ) else { return }
        guard section.allowsNativeDestination(
            destination,
            isSignedIn: isSignedIn,
            userRoles: userRoles,
            featureFlags: featureFlagState.effective
        ) else {
            return
        }

        guard revealNativeSectionIfNeeded(section) else { return }

        guard sectionPreferences.orderedSections.contains(section) else {
            return
        }

        applyNativeRouteSelection(
            entry: route.entry,
            match: route.match,
            destination: destination,
            section: section,
            url: url
        )
    }

    func routeNativeTargetPath(_ targetPath: String) {
        guard let url = normalizedNativeTargetURL(targetPath) else { return }
        if isExternalWebURL(url) {
            openURL(url)
            return
        }
        routeNativeURL(url)
    }

    func completeSignIn() {
        showingSignIn = false
        signInLinkEmail = nil
        signInLinkCode = nil
        if let pendingNativeRouteURL {
            self.pendingNativeRouteURL = nil
            routeNativeURL(pendingNativeRouteURL)
            return
        }
        repairSelectedSection()
    }

    func repairSelectionStateForSection() {
        selectedSubsection = nil
        guard let selectedNativeRouteEntry,
              selectedNativeRouteEntry.nativeSection != selectedSection
        else {
            return
        }
        self.selectedNativeRouteEntry = nil
        selectedNativeRouteMatch = nil
        selectedNativeRouteQuery = nil
    }

    func urlNativeRoutePathAndQuery(_ url: URL) -> String {
        nativeAppLinkRoutePathAndQuery(url)
    }

    func normalizedNativeTargetURL(_ targetPath: String) -> URL? {
        let trimmedTargetPath = targetPath.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmedTargetPath.isEmpty else { return nil }
        if let absoluteURL = URL(string: trimmedTargetPath), absoluteURL.scheme != nil {
            return absoluteURL
        }
        let normalizedPath = trimmedTargetPath.hasPrefix("/")
            ? String(trimmedTargetPath.dropFirst())
            : trimmedTargetPath
        return URL(string: "voucha://\(normalizedPath)")
    }

    func isExternalWebURL(_ url: URL) -> Bool {
        guard let scheme = url.scheme?.lowercased() else { return false }
        return scheme == "http" || scheme == "https"
    }

    func applySignInLinkQuery(from url: URL) {
        let components = URLComponents(url: url, resolvingAgainstBaseURL: false)
        signInLinkEmail = components?.queryItems?.first { $0.name == "emailAddress" }?.value
        signInLinkCode = components?.queryItems?.first { $0.name == "otp" }?.value
    }

    func nativeRouteEntry(for section: AppSection) -> NativeRouteCatalogEntry? {
        guard let selectedNativeRouteEntry, selectedNativeRouteEntry.nativeSection == section else {
            return nil
        }
        return selectedNativeRouteEntry
    }

    func nativeRouteMatch(for section: AppSection) -> NativeRouteMatch? {
        guard nativeRouteEntry(for: section) != nil else { return nil }
        return selectedNativeRouteMatch
    }

    func nativeRouteQuery(for section: AppSection) -> String? {
        guard nativeRouteEntry(for: section) != nil else { return nil }
        return selectedNativeRouteQuery
    }
}
