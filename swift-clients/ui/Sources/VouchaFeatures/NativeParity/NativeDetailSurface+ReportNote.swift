import SwiftUI
import VouchaLocalization

extension NativeDetailSurface {
    var reportNoteSheet: some View {
        NavigationStack {
            Form {
                Section {
                    TextEditor(text: reportNoteBinding)
                        .frame(minHeight: 140)
                }
            }
            .navigationTitle(UiMessages.string(.nativeSwiftDetailReportUser, locale: nativeUiLocale))
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button(UiMessages.string(.nativeSwiftCommonCancel, locale: nativeUiLocale)) {
                        viewModel.clearPendingReportDraft()
                        showingReportNoteDialog = false
                    }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button(UiMessages.string(.nativeSwiftCommonContinue, locale: nativeUiLocale)) {
                        continueReportNote()
                    }
                }
            }
        }
    }

    func continueReportNote() {
        viewModel.detailPendingReportTurnstile = true
        showingReportNoteDialog = false
    }

    func presentReportTurnstileAfterNoteDismiss() {
        guard viewModel.detailPendingReportTurnstile else { return }
        viewModel.detailPendingReportTurnstile = false
        viewModel.detailShowingReportTurnstile = true
    }

    private var reportNoteBinding: Binding<String> {
        Binding(
            get: { viewModel.detailPendingReportNote },
            set: { viewModel.detailPendingReportNote = String($0.prefix(1_000)) }
        )
    }
}
