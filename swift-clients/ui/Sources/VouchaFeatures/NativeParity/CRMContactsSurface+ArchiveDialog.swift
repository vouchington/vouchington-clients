import SwiftUI
import VouchaLocalization

extension View {
    func crmArchiveConfirmation(viewModel: CRMContactsViewModel) -> some View {
        modifier(CRMArchiveConfirmationModifier(viewModel: viewModel))
    }
}

private struct CRMArchiveConfirmationModifier: ViewModifier {
    @Environment(\.locale)
    var nativeUiLocale
    let viewModel: CRMContactsViewModel

    func body(content: Content) -> some View {
        @Bindable
        var viewModel = viewModel

        content.confirmationDialog(
            UiMessages.string(.nativeSwiftCrmContactsArchiveContactConfirmationTitle, locale: nativeUiLocale),
            isPresented: $viewModel.canShowArchiveConfirmation,
            titleVisibility: .visible
        ) {
            Button(
                UiMessages.string(.nativeSwiftCrmContactsArchiveContact, locale: nativeUiLocale),
                role: .destructive
            ) {
                Task { await viewModel.archiveSelectedContact() }
            }
            Button(UiMessages.string(.nativeSwiftCommonCancel, locale: nativeUiLocale), role: .cancel) {
                viewModel.dismissArchiveConfirmation()
            }
        } message: {
            Text(UiMessages.string(.nativeSwiftCrmContactsThisWillArchiveTheContactAndKeepThe, locale: nativeUiLocale))
        }
    }
}
