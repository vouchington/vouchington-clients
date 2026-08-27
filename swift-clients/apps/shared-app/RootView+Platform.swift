import SwiftUI
import VouchaAuth
import VouchaFeatures

extension RootView {
    var appBody: some View {
        #if os(macOS)
            macOSBody
        #else
            iOSBody
        #endif
    }

    #if os(macOS)
        var macOSBody: some View {
            RootSplitView(
                selectedSection: $selectedSection,
                selectedSubsection: $selectedSubsection,
                selectedNativeRouteEntry: $selectedNativeRouteEntry,
                selectedNativeRouteMatch: $selectedNativeRouteMatch,
                selectedNativeRouteQuery: $selectedNativeRouteQuery,
                nativeRouteDispatchGeneration: nativeRouteDispatchGeneration,
                visibleSections: visibleSections,
                viewModelFactory: viewModelFactory,
                playbackController: podcastPlaybackController,
                onNavigateToTargetPath: { routeNativeTargetPath($0) },
                customizeNavigation: { showingCustomizeNavigation = true },
                showSignIn: { showingSignIn = true }
            )
            .accessibilityIdentifier(Self.rootShellAccessibilityIdentifier)
            .onAppear {
                repairSelectedSection()
            }
            .onChange(of: viewModelFactory.sessionManager.isSignedIn) { _, _ in
                Task {
                    await refreshFeatureFlags(retryUntilSuccess: false)
                }
                repairSelectedSection()
            }
            .onChange(of: selectedSection) { _, _ in
                repairSelectionStateForSection()
            }
            .onChange(of: sectionPreferences) { _, _ in
                repairSelectedSection()
            }
            .onChange(of: featureFlagState.effective) { _, _ in
                featureFlagsDidChange()
            }
            .onOpenURL { url in
                handleIncomingURL(url)
            }
            .sheet(isPresented: $showingCustomizeNavigation) {
                BottomBarEditView(preferences: $sectionPreferences) {
                    AppSectionPreferenceStore.save($0)
                }
            }
            .sheet(isPresented: $showingSignIn) {
                SignInView(
                    signInService: viewModelFactory.makeSignInService(),
                    nativeOAuthAuthorizationCoordinator: viewModelFactory.nativeOAuthAuthorizationCoordinator,
                    uiLocale: viewModelFactory.uiLocaleController.locale,
                    onNavigateToTargetPath: { routeNativeTargetPath($0) },
                    initialEmail: signInLinkEmail,
                    initialCode: signInLinkCode,
                    initialOAuthMFAAttemptId: currentNativeOAuthMFAAttemptId,
                    onSuccess: {
                        completeSignIn()
                    }
                )
            }
        }
    #endif

    #if !os(macOS)
        var iOSBody: some View {
            Group {
                if horizontalSizeClass == .regular {
                    RootSplitView(
                        selectedSection: $selectedSection,
                        selectedSubsection: $selectedSubsection,
                        selectedNativeRouteEntry: $selectedNativeRouteEntry,
                        selectedNativeRouteMatch: $selectedNativeRouteMatch,
                        selectedNativeRouteQuery: $selectedNativeRouteQuery,
                        nativeRouteDispatchGeneration: nativeRouteDispatchGeneration,
                        visibleSections: visibleSections,
                        viewModelFactory: viewModelFactory,
                        playbackController: podcastPlaybackController,
                        onNavigateToTargetPath: { routeNativeTargetPath($0) },
                        customizeNavigation: { showingCustomizeNavigation = true },
                        showSignIn: { showingSignIn = true }
                    )
                    .accessibilityIdentifier(Self.rootShellAccessibilityIdentifier)
                } else if let selectedSection,
                          selectedSection.requiresAuth,
                          !viewModelFactory.sessionManager.isSignedIn {
                    NavigationStack {
                        ContentUnavailableView {
                            Label(
                                viewModelFactory.uiLocaleController.string(.nativeAuthSignInToContinue),
                                systemImage: "person.crop.circle.badge.checkmark"
                            )
                        } description: {
                            Text(
                                viewModelFactory.uiLocaleController.string(
                                    .nativeAuthProtectedDestinationDescription
                                )
                            )
                        } actions: {
                            Button(viewModelFactory.uiLocaleController.string(.nativeAuthSignIn)) {
                                showingSignIn = true
                            }
                            .buttonStyle(.borderedProminent)
                        }
                    }
                    .accessibilityIdentifier(Self.rootShellAccessibilityIdentifier)
                } else {
                    TabView(selection: $selectedSection) {
                        ForEach(visibleSections) { section in
                            SectionDetailView(
                                section: section,
                                macOSSubsection: nil,
                                nativeRouteEntry: nativeRouteEntry(for: section),
                                nativeRouteMatch: nativeRouteMatch(for: section),
                                nativeRouteQuery: nativeRouteQuery(for: section),
                                nativeRouteDispatchGeneration: nativeRouteDispatchGeneration,
                                factory: viewModelFactory,
                                playbackController: podcastPlaybackController,
                                onNavigateToTargetPath: { routeNativeTargetPath($0) },
                                customizeNavigation: { showingCustomizeNavigation = true },
                                showSignIn: { showingSignIn = true }
                            )
                            .tabItem {
                                Label(
                                    section.title(using: viewModelFactory.uiLocaleController),
                                    systemImage: section.systemImage
                                )
                            }
                            .tag(Optional(section))
                        }
                    }
                    .accessibilityIdentifier(Self.rootShellAccessibilityIdentifier)
                }
            }
            .onAppear {
                repairSelectedSection()
            }
            .onChange(of: viewModelFactory.sessionManager.isSignedIn) { _, _ in
                Task {
                    await refreshFeatureFlags(retryUntilSuccess: false)
                }
                repairSelectedSection()
            }
            .onChange(of: sectionPreferences) { _, _ in
                repairSelectedSection()
            }
            .onChange(of: featureFlagState.effective) { _, _ in
                featureFlagsDidChange()
            }
            .onChange(of: selectedSection) { _, _ in
                repairSelectionStateForSection()
            }
            .onOpenURL { url in
                handleIncomingURL(url)
            }
            .sheet(isPresented: $showingCustomizeNavigation) {
                BottomBarEditView(preferences: $sectionPreferences) {
                    AppSectionPreferenceStore.save($0)
                }
            }
            .sheet(isPresented: $showingSignIn) {
                SignInView(
                    signInService: viewModelFactory.makeSignInService(),
                    nativeOAuthAuthorizationCoordinator: viewModelFactory.nativeOAuthAuthorizationCoordinator,
                    uiLocale: viewModelFactory.uiLocaleController.locale,
                    onNavigateToTargetPath: { routeNativeTargetPath($0) },
                    initialEmail: signInLinkEmail,
                    initialCode: signInLinkCode,
                    initialOAuthMFAAttemptId: currentNativeOAuthMFAAttemptId,
                    onSuccess: {
                        completeSignIn()
                    }
                )
            }
        }
    #endif
}
