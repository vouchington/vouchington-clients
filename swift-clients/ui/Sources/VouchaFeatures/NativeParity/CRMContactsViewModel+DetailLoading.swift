import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension CRMContactsViewModel {
    func loadContactDetail(contactId: String) async {
        guard let client else { return }
        detailErrorMessage = nil
        isLoadingDetail = true
        let revision = detailLoadRevision + 1
        detailLoadRevision = revision
        defer {
            if detailLoadRevision == revision {
                isLoadingDetail = false
            }
        }

        emailPagination.reset()
        notePagination.reset()
        guard let emailRequest = emailPagination.beginNextPage(),
              let noteRequest = notePagination.beginNextPage()
        else { return }

        do {
            async let detailResponse: CrmContactDetailResponse = client.send(.crmContact(contactId: contactId))
            async let emailResponse: Page<CrmMessage> = client.send(.crmContactEmails(contactId: contactId))
            async let noteResponse: Page<CrmNote> = client.send(.crmContactNotes(contactId: contactId))
            let detail = try await detailResponse
            let emails = try await emailResponse
            let notes = try await noteResponse

            guard detailLoadRevision == revision, selectedContactId == contactId else { return }
            applyLoadedContactDetail(detail)
            emailPagination.complete(
                emailRequest,
                items: emails.results,
                endCursor: emails.pageInfo.endCursor,
                hasNextPage: emails.pageInfo.hasNextPage
            )
            notePagination.complete(
                noteRequest,
                items: notes.results,
                endCursor: notes.pageInfo.endCursor,
                hasNextPage: notes.pageInfo.hasNextPage
            )
        } catch let error as VouchaError {
            guard detailLoadRevision == revision, selectedContactId == contactId else { return }
            emailPagination.fail(emailRequest, error: error)
            notePagination.fail(noteRequest, error: error)
            detailErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            guard detailLoadRevision == revision, selectedContactId == contactId else { return }
            let paginationError = VouchaError.api(statusCode: 0, preconditionCode: nil)
            emailPagination.fail(emailRequest, error: paginationError)
            notePagination.fail(noteRequest, error: paginationError)
            detailErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    private func applyLoadedContactDetail(_ detail: CrmContactDetailResponse) {
        selectedContact = detail.contact
        selectedSocialAccounts = detail.socialAccounts
        editForm.populate(from: detail.contact)
        linkedUserId = detail.contact.userId ?? ""
        canShowArchiveConfirmation = false
    }

    func loadEmails(contactId: String) async {
        guard let client, let request = emailPagination.beginNextPage() else { return }
        do {
            let response: Page<CrmMessage> = try await client.send(
                .crmContactEmails(contactId: contactId, after: request.cursor)
            )
            guard selectedContactId == contactId, emailPagination.isCurrent(request) else { return }
            emailPagination.complete(
                request,
                items: response.results,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch let error as VouchaError {
            guard selectedContactId == contactId else { return }
            emailPagination.fail(request, error: error)
            emailErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            guard selectedContactId == contactId else { return }
            emailPagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
            emailErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    func loadNotes(contactId: String) async {
        guard let client, let request = notePagination.beginNextPage() else { return }
        do {
            let response: Page<CrmNote> = try await client.send(
                .crmContactNotes(contactId: contactId, after: request.cursor)
            )
            guard selectedContactId == contactId, notePagination.isCurrent(request) else { return }
            notePagination.complete(
                request,
                items: response.results,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch let error as VouchaError {
            guard selectedContactId == contactId else { return }
            notePagination.fail(request, error: error)
            noteErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            guard selectedContactId == contactId else { return }
            notePagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
            noteErrorMessage = .verbatim(error.localizedDescription)
        }
    }
}
