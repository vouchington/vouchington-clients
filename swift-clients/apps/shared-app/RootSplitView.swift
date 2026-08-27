import SwiftUI
import VouchaFeatures
import VouchaLocalization

@MainActor
struct RootSplitView: View {
    @Environment(\.locale)
    private var locale
    @Binding
    var selectedSection: AppSection?
    @Binding
    var selectedSubsection: VerticalSubsection?
    @Binding
    var selectedNativeRouteEntry: NativeRouteCatalogEntry?
    @Binding
    var selectedNativeRouteMatch: NativeRouteMatch?
    @Binding
    var selectedNativeRouteQuery: String?
    let nativeRouteDispatchGeneration: Int
    let visibleSections: [AppSection]
    let viewModelFactory: ViewModelFactory
    let playbackController: PodcastPlaybackController
    let onNavigateToTargetPath: (String) -> Void
    let customizeNavigation: () -> Void
    let showSignIn: () -> Void

    var body: some View {
        NavigationSplitView {
            sectionList
        } content: {
            subsectionList
        } detail: {
            detailView
        }
    }

    private var sectionList: some View {
        List(visibleSections, selection: $selectedSection) { section in
            Label(UiMessages.string(section.titleKey, locale: locale), systemImage: section.systemImage)
                .tag(section)
        }
        .listStyle(.sidebar)
        .navigationTitle(UiMessages.string(.nativeSwiftNavigationVoucha, locale: locale))
        .toolbar {
            if !viewModelFactory.sessionManager.isSignedIn {
                ToolbarItem(placement: .primaryAction) {
                    Button(UiMessages.string(.nativeAuthSignIn, locale: locale), action: showSignIn)
                }
            }
            ToolbarItem(placement: .primaryAction) {
                Button(UiMessages.string(.nativeNavigationCustomize, locale: locale), action: customizeNavigation)
            }
        }
    }

    @ViewBuilder
    private var subsectionList: some View {
        if let section = selectedSection, !section.subsections.isEmpty {
            List(section.subsections, selection: $selectedSubsection) { sub in
                Label(
                    UiMessages.string(section.subsectionTitle(sub), locale: locale),
                    systemImage: sub.systemImage
                )
                .tag(Optional(sub))
            }
            .listStyle(.sidebar)
            .navigationTitle(UiMessages.string(section.titleKey, locale: locale))
        } else {
            EmptyView().frame(maxWidth: .infinity)
        }
    }

    @ViewBuilder
    private var detailView: some View {
        if let section = selectedSection, !section.requiresAuth || viewModelFactory.sessionManager.isSignedIn {
            SectionDetailView(
                section: section,
                macOSSubsection: selectedSubsection,
                nativeRouteEntry: nativeRouteEntry(for: section),
                nativeRouteMatch: nativeRouteMatch(for: section),
                nativeRouteQuery: nativeRouteQuery(for: section),
                nativeRouteDispatchGeneration: nativeRouteDispatchGeneration,
                factory: viewModelFactory,
                playbackController: playbackController,
                onNavigateToTargetPath: onNavigateToTargetPath,
                customizeNavigation: customizeNavigation,
                showSignIn: showSignIn
            )
        } else if let section = selectedSection, section.requiresAuth {
            ContentUnavailableView {
                Label(
                    UiMessages.string(.nativeAuthSignInToContinue, locale: locale),
                    systemImage: "person.crop.circle.badge.checkmark"
                )
            } description: {
                Text(UiMessages.string(.nativeAuthProtectedDestinationDescription, locale: locale))
            } actions: {
                Button(UiMessages.string(.nativeAuthSignIn, locale: locale), action: showSignIn)
                    .buttonStyle(.borderedProminent)
            }
        } else if selectedSection == nil {
            Text(UiMessages.string(.nativeNavigationSelectSection, locale: locale))
                .foregroundStyle(.secondary)
        }
    }

    private func nativeRouteEntry(for section: AppSection) -> NativeRouteCatalogEntry? {
        guard let selectedNativeRouteEntry, selectedNativeRouteEntry.nativeSection == section else {
            return nil
        }
        return selectedNativeRouteEntry
    }

    private func nativeRouteQuery(for section: AppSection) -> String? {
        guard nativeRouteEntry(for: section) != nil else { return nil }
        return selectedNativeRouteQuery
    }

    private func nativeRouteMatch(for section: AppSection) -> NativeRouteMatch? {
        guard nativeRouteEntry(for: section) != nil else { return nil }
        return selectedNativeRouteMatch
    }
}
