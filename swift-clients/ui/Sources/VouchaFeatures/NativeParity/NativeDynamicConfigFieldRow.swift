import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct NativeDynamicConfigFieldRow: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @Bindable
    var viewModel: NativeDynamicConfigViewModel
    let namespace: DynamicConfigNamespace
    let field: DynamicConfigField

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            HStack {
                Text(field.name.split(separator: "_").joined(separator: " ")).font(Typography.headline)
                Spacer()
                Text(UiMessages.string(.protocolValue(field.type.rawValue), locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(.secondary)
            }
            Text(field.description).font(Typography.subheadline).foregroundStyle(.secondary)
            editor
            if let error = viewModel.validationErrors[field.name] {
                Text(UiMessages.string(error, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(.red)
            }
        }
        .padding(Spacing.md)
        .background(Colors.background.opacity(0.75))
        .clipShape(RoundedRectangle(cornerRadius: 8))
    }

    @ViewBuilder
    private var editor: some View {
        switch field.type {
        case .boolean:
            Toggle(UiMessages.string(
                .nativeSwiftDynamicConfigFeatureFlagsGlobalValue,
                locale: nativeUiLocale
            ), isOn: Binding(
                get: {
                    if case let .boolean(value) = field.value {
                        value
                    } else {
                        false
                    }
                },
                set: { value in Task { await viewModel.saveBoolean(field: field, value: value) } }
            ))
            .disabled(isEditingDisabled)
        case .string, .number:
            HStack {
                TextField(UiMessages.string(
                    .nativeSwiftDynamicConfigFeatureFlagsValue,
                    locale: nativeUiLocale
                ), text: Binding(
                    get: { viewModel.drafts[field.name] ?? "" },
                    set: { viewModel.drafts[field.name] = $0 }
                ))
                .textFieldStyle(.roundedBorder)
                .disabled(isEditingDisabled)
                Button(UiMessages.string(.nativeSwiftCommonSave, locale: nativeUiLocale)) {
                    Task { await viewModel.saveDraft(field: field) }
                }
                .disabled(isEditingDisabled)
            }
        }
    }

    var isEditingDisabled: Bool {
        !namespace.canUpdate || viewModel.isSaving || viewModel.isSelecting
    }

}
