import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

public struct NativeFeatureFlagOverridesSurface: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @State
    private var viewModel: NativeFeatureFlagOverridesViewModel

    public init(client: APIClient?, featureFlags: FeatureFlagState, canOverride: Bool) {
        _viewModel = State(initialValue: NativeFeatureFlagOverridesViewModel(
            client: client,
            featureFlags: featureFlags,
            canOverride: canOverride
        ))
    }

    init(viewModel: NativeFeatureFlagOverridesViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    public var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.md) {
                Text(UiMessages.string(
                    .nativeSwiftDynamicConfigFeatureFlagsDeviceOverrides,
                    locale: nativeUiLocale
                )).font(Typography.largeTitle)
                Text(UiMessages.string(
                    .nativeSwiftDynamicConfigFeatureFlagsDeviceOverridesDescription,
                    locale: nativeUiLocale
                )).foregroundStyle(.secondary)
                content
            }
            .padding(Spacing.md)
        }
        .navigationTitle(UiMessages.string(
            .nativeSwiftDynamicConfigFeatureFlagsFeatureFlags,
            locale: nativeUiLocale
        ))
        .task { await viewModel.load() }
    }

    @ViewBuilder
    private var content: some View {
        if !viewModel.canOverride {
            ContentUnavailableView(
                UiMessages.string(.nativeSwiftDynamicConfigFeatureFlagsAccessRequired, locale: nativeUiLocale),
                systemImage: "lock"
            )
        } else if viewModel.featureFlags.hasLoadedRemote {
            ForEach(viewModel.featureFlags.remote.keys.sorted(), id: \.self) { key in
                overrideRow(key)
            }
            Button(UiMessages.string(
                .nativeSwiftDynamicConfigFeatureFlagsClearOverrides,
                locale: nativeUiLocale
            )) { viewModel.clearAll() }
                .disabled(viewModel.featureFlags.localOverrides.isEmpty)
            if let error = viewModel.errorMessage {
                Text(UiMessages.string(error, locale: nativeUiLocale)).foregroundStyle(.red)
            }
        } else if viewModel.isLoading {
            ProgressView(UiMessages.string(
                .nativeSwiftDynamicConfigFeatureFlagsLoadingFeatureFlags,
                locale: nativeUiLocale
            ))
        } else {
            Text(UiMessages.string(
                viewModel.errorMessage
                    ?? .message(.nativeSwiftDynamicConfigFeatureFlagsUnavailable),
                locale: nativeUiLocale
            ))
            .foregroundStyle(.red)
            Button(UiMessages.string(.nativeSwiftDynamicConfigFeatureFlagsRetry, locale: nativeUiLocale)) {
                Task { await viewModel.load() }
            }
        }
    }

    private func overrideRow(_ key: String) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(key).font(Typography.headline)
            Text(UiMessages.string(
                .nativeSwiftDynamicConfigFeatureFlagsGlobalValueFormat,
                parameters: [
                    "value": UiMessages.string(
                        viewModel.featureFlags.remote[key] == true
                            ? .nativeSwiftDynamicConfigFeatureFlagsEnabled
                            : .nativeSwiftDynamicConfigFeatureFlagsDisabled,
                        locale: nativeUiLocale
                    )
                ],
                locale: nativeUiLocale
            ))
            .font(Typography.caption)
            .foregroundStyle(.secondary)
            Picker(UiMessages.string(
                .nativeSwiftDynamicConfigFeatureFlagsDeviceOverride,
                parameters: ["key": key],
                locale: nativeUiLocale
            ), selection: Binding(
                get: { NativeFeatureFlagOverrideChoice(viewModel.featureFlags.localOverride(for: key)) },
                set: { viewModel.apply($0, to: key) }
            )) {
                Text(UiMessages.string(
                    .nativeSwiftDynamicConfigFeatureFlagsInherited,
                    locale: nativeUiLocale
                )).tag(NativeFeatureFlagOverrideChoice.inherited)
                Text(UiMessages.string(
                    .nativeSwiftDynamicConfigFeatureFlagsEnabled,
                    locale: nativeUiLocale
                )).tag(NativeFeatureFlagOverrideChoice.enabled)
                Text(UiMessages.string(
                    .nativeSwiftDynamicConfigFeatureFlagsDisabled,
                    locale: nativeUiLocale
                )).tag(NativeFeatureFlagOverrideChoice.disabled)
            }
        }
        .padding(Spacing.md)
        .background(Colors.background.opacity(0.75))
        .clipShape(RoundedRectangle(cornerRadius: 8))
    }
}
