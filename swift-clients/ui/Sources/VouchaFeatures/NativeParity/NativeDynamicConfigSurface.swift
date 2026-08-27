import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

public struct NativeDynamicConfigSurface: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @State
    private var viewModel: NativeDynamicConfigViewModel

    public init(
        client: APIClient?,
        onNamespaceUpdated: @escaping @MainActor (DynamicConfigNamespace) -> Void = { _ in }
    ) {
        _viewModel = State(initialValue: NativeDynamicConfigViewModel(
            client: client,
            onNamespaceUpdated: onNamespaceUpdated
        ))
    }

    init(viewModel: NativeDynamicConfigViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    public var body: some View {
        NavigationSplitView {
            namespaceList
        } detail: {
            detail
        }
        .task { await viewModel.load() }
    }

    private var namespaceList: some View {
        List(viewModel.filteredNamespaces) { namespace in
            Button {
                Task { await viewModel.select(namespace.namespace) }
            } label: {
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    HStack {
                        Text(namespace.label)
                        Spacer()
                        if !namespace.canUpdate {
                            Text(UiMessages.string(
                                .nativeSwiftDynamicConfigFeatureFlagsReadOnly,
                                locale: nativeUiLocale
                            )).font(Typography.caption).foregroundStyle(.secondary)
                        }
                    }
                    Text(namespace.namespace)
                        .font(Typography.caption.monospaced())
                        .foregroundStyle(.secondary)
                }
            }
            .buttonStyle(.plain)
        }
        .searchable(
            text: $viewModel.query,
            prompt: UiMessages.string(.nativeSwiftDynamicConfigFeatureFlagsSearchNamespaces, locale: nativeUiLocale)
        )
        .navigationTitle(UiMessages.string(
            .nativeSwiftDynamicConfigFeatureFlagsDynamicConfig,
            locale: nativeUiLocale
        ))
        .overlay {
            if viewModel.isLoading, viewModel.namespaces.isEmpty {
                ProgressView()
            }
        }
    }

    @ViewBuilder
    private var detail: some View {
        if let namespace = viewModel.selectedNamespace {
            ScrollView {
                VStack(alignment: .leading, spacing: Spacing.lg) {
                    namespaceHeader(namespace)
                    ForEach(namespace.fields) { field in
                        NativeDynamicConfigFieldRow(viewModel: viewModel, namespace: namespace, field: field)
                    }
                    NativeDynamicConfigHistorySection(history: viewModel.history)
                }
                .padding(Spacing.md)
            }
            .navigationTitle(namespace.label)
        } else if let error = viewModel.errorMessage {
            ContentUnavailableView(
                UiMessages.string(.nativeSwiftDynamicConfigFeatureFlagsUnableToLoad, locale: nativeUiLocale),
                systemImage: "exclamationmark.triangle",
                description: Text(UiMessages.string(error, locale: nativeUiLocale))
            )
        } else {
            ContentUnavailableView(
                UiMessages.string(.nativeSwiftDynamicConfigFeatureFlagsSelectNamespace, locale: nativeUiLocale),
                systemImage: "slider.horizontal.3"
            )
        }
    }

    private func namespaceHeader(_ namespace: DynamicConfigNamespace) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            HStack {
                Text(namespace.label).font(Typography.largeTitle)
                if !namespace.canUpdate {
                    Text(UiMessages.string(
                        .nativeSwiftDynamicConfigFeatureFlagsReadOnly,
                        locale: nativeUiLocale
                    )).font(Typography.caption).foregroundStyle(.secondary)
                }
            }
            Text(namespace.description).foregroundStyle(.secondary)
            if let feedback = viewModel.feedbackMessage {
                Text(UiMessages.string(feedback, locale: nativeUiLocale)).foregroundStyle(.secondary)
            }
            if let error = viewModel.errorMessage {
                Text(UiMessages.string(error, locale: nativeUiLocale)).foregroundStyle(.red)
            }
        }
    }
}
