import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

/// Privileged UTC range controls; rendered only for an authorized administrator.
struct NativeEngineeringPostgresqlRangeSection: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable var viewModel: NativeEngineeringPostgresqlViewModel

    var body: some View {
        if viewModel.isRangeAuthorized {
            VStack(alignment: .leading, spacing: Spacing.md) {
                familyControls(.classifier)
                familyControls(.request)
            }
            .confirmationDialog(
                dialogTitle,
                isPresented: Binding(
                    get: { viewModel.rangeConfirmation != nil },
                    set: { if !$0 { viewModel.dismissRangeConfirmation() } }
                ),
                titleVisibility: .visible
            ) {
                Button(dialogTitle, role: dialogRole) {
                    viewModel.requestRangeConfirmation()
                    Task { await viewModel.confirmRangeOperation() }
                }
                Button(UiMessages.string(.nativeSwiftCommonCancel, locale: nativeUiLocale), role: .cancel) {
                    viewModel.cancelRangeOperation()
                }
            } message: {
                Text(dialogMessage)
            }
        }
    }

    private func familyControls(_ family: NativePostgresqlRangeFamily) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            field(key: keys(family).fromDate, text: draftBinding(family, \.fromDate))
            field(key: keys(family).toDate, text: draftBinding(family, \.toDate))
            field(key: keys(family).rows, text: draftBinding(family, \.maxDefaultRows))
            if viewModel.rangeError == .invalidRange, viewModel.rangeErrorFamily == family {
                Text(UiMessages.string(keys(family).invalid, locale: nativeUiLocale))
                    .font(.footnote)
                    .foregroundStyle(.red)
            }
            HStack {
                Button(UiMessages.string(keys(family).create, locale: nativeUiLocale)) {
                    viewModel.prepareRange(family: family, retire: false)
                }
                Button(UiMessages.string(keys(family).retire, locale: nativeUiLocale), role: .destructive) {
                    viewModel.prepareRange(family: family, retire: true)
                }
            }
            .buttonStyle(.bordered)
            .disabled(!viewModel.canPrepareRange)
        }
    }

    private func field(key: UiMessageKey, text: Binding<String>) -> some View {
        TextField(UiMessages.string(key, locale: nativeUiLocale), text: text)
            .textFieldStyle(.roundedBorder)
    }

    private func draftBinding(
        _ family: NativePostgresqlRangeFamily,
        _ path: WritableKeyPath<NativePostgresqlRangeDraft, String>
    ) -> Binding<String> {
        Binding(
            get: { (viewModel.rangeDrafts[family] ?? NativePostgresqlRangeDraft())[keyPath: path] },
            set: {
                var draft = viewModel.rangeDrafts[family] ?? NativePostgresqlRangeDraft()
                draft[keyPath: path] = $0
                viewModel.rangeDrafts[family] = draft
            }
        )
    }
}
