import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension CRMContactsSurface {
    func notesSection(viewModel: CRMContactsViewModel) -> some View {
        @Bindable
        var viewModel = viewModel

        return Section(header: Text(verbatim: UiMessages.string(
            .nativeSwiftCrmContactsNotes,
            locale: nativeUiLocale
        ))) {
            TextEditor(text: $viewModel.noteBody)
                .frame(minHeight: 120)
                .overlay(
                    RoundedRectangle(cornerRadius: 8, style: .continuous)
                        .strokeBorder(.quaternary, lineWidth: 1)
                )
            Button {
                Task { await viewModel.createNote() }
            } label: {
                Label(
                    UiMessages.string(.nativeSwiftCrmContactsAddNote, locale: nativeUiLocale),
                    systemImage: "note.text.badge.plus"
                )
            }
            .buttonStyle(.borderedProminent)
            .disabled(viewModel.isCreatingNote)
            if let message = viewModel.noteErrorMessage {
                Text(UiMessages.string(message, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.negativeVote)
            }
            noteRows(viewModel: viewModel)
            HybridPaginationControl(
                hasMore: viewModel.notePagination.hasMore,
                isLoading: viewModel.notePagination.isLoading,
                hasError: viewModel.notePagination.lastError != nil
            ) {
                await viewModel.loadMoreNotes()
            }
        }
    }

    private func noteRows(viewModel: CRMContactsViewModel) -> some View {
        ForEach(viewModel.selectedNotes) { note in
            VStack(alignment: .leading, spacing: Spacing.xs) {
                HStack {
                    Text(note.body)
                    Spacer(minLength: 0)
                    Button(role: .destructive) {
                        Task { await viewModel.deleteNote(noteId: note.id) }
                    } label: {
                        Image(systemName: "trash")
                    }
                    .buttonStyle(.borderless)
                }
                Text(UiMessages.date(
                    note.createdAt,
                    date: .abbreviated,
                    time: .shortened,
                    locale: nativeUiLocale,
                    timeZone: .current
                ))
                .font(Typography.caption)
                .foregroundStyle(Colors.secondaryLabel)
            }
        }
    }
}
