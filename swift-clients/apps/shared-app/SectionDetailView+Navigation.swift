import SwiftUI
import VouchaFeatures
import VouchaLocalization

extension SectionDetailView {
    var body: some View {
        NavigationStack(path: $nativeRouteHistory) {
            Group {
                if let activeNativeRouteEntry {
                    NativeRouteDestinationView(
                        entry: activeNativeRouteEntry,
                        client: factory.apiClient,
                        routeMatch: activeNativeRouteMatch,
                        routeQuery: activeNativeRouteQuery,
                        playbackController: playbackController,
                        isSignedIn: factory.sessionManager.isSignedIn,
                        currentUserId: factory.sessionManager.currentUserId,
                        membershipPlan: factory.sessionManager.currentMembershipPlan,
                        isAdministrator: factory.sessionManager.currentUserRoles.contains("administrator"),
                        canOverrideFeatureFlags: FeatureFlagState.canManageDeviceOverrides(
                            userRoles: factory.sessionManager.currentUserRoles
                        ),
                        isSiteModerator: factory.sessionManager.currentUserRoles.contains("moderator"),
                        isCustomerSupport: false,
                        featureFlags: factory.featureFlagState,
                        nativeOAuthAuthorizationCoordinator: factory.nativeOAuthAuthorizationCoordinator,
                        a11yActivator: notificationA11yActivator,
                        notificationActivationToken: nativeRouteDispatchGeneration,
                        onNavigateToTargetPath: navigateWithinNativeSection,
                        showSignIn: showSignIn,
                        canCastPublicVotes: canCastPublicVotes,
                        hideDownCount: hideDownCount
                    )
                } else if section.isVertical {
                    verticalContent(subsection: effectiveSubsection)
                } else {
                    nonVerticalContent
                }
            }
            .navigationTitle(UiMessages.string(section.titleKey, locale: nativeUiLocale))
            .navigationDestination(for: String.self) { targetPath in
                nativeRouteDestination(for: targetPath)
            }
            // swiftformat:disable indent
            #if !os(macOS)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                verticalPickerToolbarItem
                customizeToolbarItem
                signInToolbarItem
            }
            #else
            .toolbar {
                customizeToolbarItem
                signInToolbarItem
            }
            #endif
            // swiftformat:enable indent
        }
        .onChange(of: nativeRouteDispatchGeneration) { _, _ in nativeRouteHistory = [] }
        .onChange(of: section) { _, _ in nativeRouteHistory = [] }
        .onChange(of: activeNativeRouteIdentity) { _, _ in nativeRouteHistory = [] }
    }

    func navigateWithinNativeSection(_ targetPath: String) {
        guard let target = NativeRouteCatalog.matchingRoute(for: targetPath),
              target.entry.destinationIdentifier == .engineeringAgents,
              target.entry.nativeSection == section
        else {
            onNavigateToTargetPath(targetPath)
            return
        }
        nativeRouteHistory.append(targetPath)
    }

    @ViewBuilder
    func nativeRouteDestination(for targetPath: String) -> some View {
        if let target = NativeRouteCatalog.matchingRoute(for: targetPath) {
            NativeRouteDestinationView(
                entry: target.entry,
                client: factory.apiClient,
                routeMatch: target.match,
                routeQuery: nil,
                playbackController: playbackController,
                isSignedIn: factory.sessionManager.isSignedIn,
                currentUserId: factory.sessionManager.currentUserId,
                membershipPlan: factory.sessionManager.currentMembershipPlan,
                isAdministrator: factory.sessionManager.currentUserRoles.contains("administrator"),
                canOverrideFeatureFlags: FeatureFlagState.canManageDeviceOverrides(
                    userRoles: factory.sessionManager.currentUserRoles
                ),
                isSiteModerator: factory.sessionManager.currentUserRoles.contains("moderator"),
                isCustomerSupport: false,
                featureFlags: factory.featureFlagState,
                a11yActivator: notificationA11yActivator,
                notificationActivationToken: nativeRouteDispatchGeneration,
                onNavigateToTargetPath: navigateWithinNativeSection,
                showSignIn: showSignIn,
                canCastPublicVotes: canCastPublicVotes,
                hideDownCount: hideDownCount
            )
        }
    }

    #if !os(macOS)
        @ToolbarContentBuilder
        var verticalPickerToolbarItem: some ToolbarContent {
            if section.isVertical, !section.subsections.isEmpty {
                ToolbarItem(placement: .principal) {
                    Picker(selection: Binding(
                        get: { effectiveSubsection },
                        set: { selectIOSSubsection($0) }
                    )) {
                        ForEach(section.subsections) { sub in
                            Text(UiMessages.string(section.subsectionTitle(sub), locale: nativeUiLocale)).tag(sub)
                        }
                    } label: {
                        EmptyView()
                    }
                    .pickerStyle(.segmented)
                    .fixedSize()
                }
            }
        }
    #endif

    func selectIOSSubsection(_ subsection: VerticalSubsection) {
        didClearNativeRoute = true
        iOSSubsection = subsection
    }

    @ToolbarContentBuilder
    var customizeToolbarItem: some ToolbarContent {
        ToolbarItem(placement: .primaryAction) {
            Button(factory.uiLocaleController.string(.nativeNavigationCustomize)) {
                customizeNavigation()
            }
        }
    }

    @ToolbarContentBuilder
    var signInToolbarItem: some ToolbarContent {
        if !factory.sessionManager.isSignedIn {
            ToolbarItem(placement: .primaryAction) {
                Button(factory.uiLocaleController.string(.nativeAuthSignIn)) {
                    showSignIn()
                }
            }
        }
    }

    /// The active sub-option: macOS uses column 2 selection, iOS uses the toolbar picker.
    var effectiveSubsection: VerticalSubsection {
        let fallback = activeNativeRouteEntry?.destinationIdentifier?.verticalSubsection
            ?? signedOutDefaultSubsection
            ?? section.subsections.first
            ?? .feed(.your)
        #if os(macOS)
            return normalizeSignedOutSubsection(macOSSubsection ?? fallback)
        #else
            return normalizeSignedOutSubsection(iOSSubsection ?? fallback)
        #endif
    }

    private var activeNativeRouteEntry: NativeRouteCatalogEntry? {
        didClearNativeRoute ? nil : nativeRouteEntry
    }

    private var activeNativeRouteMatch: NativeRouteMatch? {
        didClearNativeRoute ? nil : nativeRouteMatch
    }

    private var activeNativeRouteQuery: String? {
        didClearNativeRoute ? nil : nativeRouteQuery
    }

    private var activeNativeRouteIdentity: String? {
        guard let activeNativeRouteEntry else { return nil }
        return [activeNativeRouteEntry.representativePath, activeNativeRouteMatch?.path, activeNativeRouteQuery]
            .compactMap { $0 }
            .joined(separator: "|")
    }

    private var signedOutDefaultSubsection: VerticalSubsection? {
        guard !factory.sessionManager.isSignedIn, section.isVertical else { return nil }
        return .feed(.all)
    }

    private func normalizeSignedOutSubsection(_ subsection: VerticalSubsection) -> VerticalSubsection {
        guard !factory.sessionManager.isSignedIn else { return subsection }
        switch subsection {
        case .feed(.your): return .feed(.all)
        case .sources(.your): return .sources(.all)
        default: return subsection
        }
    }
}
