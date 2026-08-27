import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct NativeSearchSurface: View {
    @Environment(\.locale) var nativeUiLocale
    @Bindable var viewModel: NativeRouteSurfaceViewModel
    let initialQuery: String?
    let onNavigateToTargetPath: (String) -> Void
    @State private var query = ""
    @State private var appliedInitialQuery = false
    @State private var searchTask: Task<Void, Never>?

    init(
        viewModel: NativeRouteSurfaceViewModel,
        initialQuery: String? = nil,
        onNavigateToTargetPath: @escaping (String) -> Void = { _ in }
    ) {
        self.viewModel = viewModel
        self.initialQuery = initialQuery
        self.onNavigateToTargetPath = onNavigateToTargetPath
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            if viewModel.destination == .fediverseSearch {
                providerFilters
            }
            HStack(spacing: Spacing.sm) {
                TextField(UiMessages.string(.nativeSwiftCommonSearch, locale: nativeUiLocale), text: $query)
                    .textFieldStyle(.roundedBorder)
                Button(UiMessages.string(.nativeSwiftCommonSearch, locale: nativeUiLocale)) { performSearch() }
                    .buttonStyle(.borderedProminent)
                    .disabled(query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || viewModel.isLoading)
            }
            results
        }
        .task {
            guard !appliedInitialQuery, let initialQuery, !initialQuery.isEmpty else { return }
            appliedInitialQuery = true
            query = initialQuery
            await viewModel.search(query: initialQuery)
        }
        .onDisappear { searchTask?.cancel() }
    }

    private var providerFilters: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: Spacing.sm) {
                ForEach(FediverseProviderFilter.allCases, id: \.self) { provider in
                    if provider == viewModel.fediverseProvider {
                        providerButton(provider).buttonStyle(.borderedProminent)
                    } else {
                        providerButton(provider).buttonStyle(.bordered)
                    }
                }
            }
        }
    }

    private func providerButton(_ provider: FediverseProviderFilter) -> some View {
        Button(UiMessages.string(provider.title, locale: nativeUiLocale)) {
            guard provider != viewModel.fediverseProvider else { return }
            viewModel.fediverseProvider = provider
            onNavigateToTargetPath(viewModel.fediverseRoute(query: query))
        }
        .controlSize(.large)
        .accessibilityIdentifier("fediverse-provider-filter-\(provider.rawValue)")
        .accessibilityAddTraits(provider == viewModel.fediverseProvider ? .isSelected : [])
    }

    @ViewBuilder private var results: some View {
        if viewModel.searchSections.isEmpty {
            NativeRowsSurface(rows: viewModel.rows, state: viewModel.state) {
                await viewModel.search(query: query)
            }
        } else {
            VStack(alignment: .leading, spacing: Spacing.md) {
                ForEach(viewModel.searchSections) { section in
                    VStack(alignment: .leading, spacing: Spacing.sm) {
                        Text(UiMessages.string(section.title, locale: nativeUiLocale)).font(Typography.headline)
                        LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                            ForEach(section.rows) { NativeSurfaceRow(row: $0) }
                        }
                    }
                }
            }
        }
    }

    private func performSearch() {
        searchTask?.cancel()
        searchTask = Task { await viewModel.search(query: query) }
    }
}
