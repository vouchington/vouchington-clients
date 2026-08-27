import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension CRMContactsViewModel {
    func generateEmailDraft() async {
        guard let client, let contactId = selectedContactId, !isDraftingEmail else { return }
        isDraftingEmail = true
        emailErrorMessage = nil
        defer { isDraftingEmail = false }
        do {
            let response: CrmEmailDraftResponse = try await client.send(
                .crmContactEmailDraft(
                    contactId: contactId,
                    prompt: emailDraftPrompt.trimmed.isEmpty ? nil : emailDraftPrompt.trimmed,
                    tone: emailDraftTone.trimmed.isEmpty ? nil : emailDraftTone.trimmed
                )
            )
            guard selectedContactId == contactId else { return }
            emailSubject = response.draft.subject
            emailBodyHtml = response.draft.bodyHtml
            emailBodyText = response.draft.bodyText
            emailDraftGeneratedAt = Date()
        } catch let error as VouchaError {
            guard selectedContactId == contactId else { return }
            emailErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            guard selectedContactId == contactId else { return }
            emailErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    func sendEmail() async {
        guard let client, let contactId = selectedContactId, !isSendingEmail else { return }
        let subject = emailSubject.trimmed
        guard !subject.isEmpty else {
            emailErrorMessage = .message(.nativeSwiftValidationSubjectRequired)
            return
        }
        guard !emailBodyHtml.trimmed.isEmpty || !emailBodyText.trimmed.isEmpty else {
            emailErrorMessage = .message(.nativeSwiftValidationBodyRequired)
            return
        }

        isSendingEmail = true
        emailErrorMessage = nil
        defer { isSendingEmail = false }

        do {
            let response: CrmMessageResponse = try await client.send(
                .sendCrmEmail(
                    contactId: contactId,
                    subject: subject,
                    bodyHtml: emailBodyHtml.trimmed.isEmpty ? nil : emailBodyHtml,
                    bodyText: emailBodyText.trimmed.isEmpty ? nil : emailBodyText,
                    emailProvider: emailProvider,
                    ctaUrl: emailCtaUrl.trimmed.isEmpty ? nil : emailCtaUrl.trimmed,
                    aiPrompt: emailDraftPrompt.trimmed.isEmpty ? nil : emailDraftPrompt.trimmed,
                    aiGeneratedAt: emailDraftGeneratedAt
                )
            )
            guard selectedContactId == contactId else { return }
            selectedEmails.insert(response.message, at: 0)
            emailSubject = ""
            emailBodyHtml = ""
            emailBodyText = ""
            emailDraftPrompt = ""
            emailDraftTone = ""
            emailDraftGeneratedAt = nil
            emailCtaUrl = ""
        } catch let error as VouchaError {
            emailErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            emailErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    func createNote() async {
        guard let client, let contactId = selectedContactId, !isCreatingNote else { return }
        let body = noteBody.trimmed
        guard !body.isEmpty else {
            noteErrorMessage = .message(.nativeSwiftValidationNoteBodyRequired)
            return
        }

        isCreatingNote = true
        noteErrorMessage = nil
        defer { isCreatingNote = false }

        do {
            let response: CrmNoteResponse = try await client.send(.createCrmNote(contactId: contactId, body: body))
            selectedNotes.insert(response.note, at: 0)
            noteBody = ""
        } catch let error as VouchaError {
            noteErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            noteErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    func deleteNote(noteId: String) async {
        guard let client, let contactId = selectedContactId else { return }
        do {
            let _: EmptyResponse = try await client.send(.deleteCrmNote(contactId: contactId, noteId: noteId))
            selectedNotes.removeAll { $0.id == noteId }
        } catch let error as VouchaError {
            noteErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            noteErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    func importContacts() async {
        guard let client, !isImporting else { return }
        let csv = importCsv.trimmed
        guard !csv.isEmpty else {
            importErrorMessage = .message(.nativeSwiftValidationCsvRequired)
            return
        }

        isImporting = true
        importErrorMessage = nil
        defer { isImporting = false }

        do {
            let response: CrmImportBatchResponse = try await client.send(
                .importCrmContacts(csv: csv),
                allowingStatusCodes: [422]
            )
            if response.valid {
                importCsv = "name,email\n"
                await reloadContacts()
            } else if let error = response.error {
                importErrorMessage = .verbatim(error)
            } else if let validation = response.validation {
                importErrorMessage = .verbatim(formatImportValidationErrors(validation))
            } else {
                importErrorMessage = .message(.nativeSwiftValidationImportValidationFailed)
            }
        } catch let error as VouchaError {
            importErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            importErrorMessage = .verbatim(error.localizedDescription)
        }
    }
}
