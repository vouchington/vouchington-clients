import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension CRMContactsViewModel {
    func createContact() async {
        guard let client, !isCreating else { return }
        let name = createForm.name.trimmed
        let email = createForm.email.trimmed
        guard !name.isEmpty else {
            createErrorMessage = .message(.nativeSwiftValidationNameRequired)
            return
        }
        guard !email.isEmpty else {
            createErrorMessage = .message(.nativeSwiftValidationEmailRequired)
            return
        }

        isCreating = true
        createErrorMessage = nil
        defer { isCreating = false }

        do {
            let response: CrmContactResponse = try await client.send(
                .createCrmContact(
                    name: name,
                    email: email,
                    phone: createForm.phone.trimmed.isEmpty ? nil : createForm.phone.trimmed,
                    vertical: createForm.vertical,
                    contactType: createForm.contactType,
                    source: .manual,
                    followerCount: createForm.followerCountValue(),
                    notes: createForm.notes.trimmed.isEmpty ? nil : createForm.notes.trimmed,
                    assignedToId: createForm.assignedToId.trimmed.isEmpty ? nil : createForm.assignedToId.trimmed
                )
            )
            contacts.insert(response.contact, at: 0)
            createForm.reset()
            await selectContact(id: response.contact.id)
        } catch let error as VouchaError {
            createErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            createErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    func saveSelectedContact() async {
        guard let client, let contactId = selectedContactId, !isSaving else { return }
        let name = editForm.name.trimmed
        let email = editForm.email.trimmed
        guard !name.isEmpty else {
            detailErrorMessage = .message(.nativeSwiftValidationNameRequired)
            return
        }
        guard !email.isEmpty else {
            detailErrorMessage = .message(.nativeSwiftValidationEmailRequired)
            return
        }

        isSaving = true
        detailErrorMessage = nil
        defer { isSaving = false }

        do {
            let response: CrmContactResponse = try await client.send(
                .updateCrmContact(
                    contactId: contactId,
                    name: name,
                    email: email,
                    phone: editForm.phone.trimmed.isEmpty ? nil : editForm.phone.trimmed,
                    vertical: editForm.vertical,
                    contactType: editForm.contactType,
                    followerCount: editForm.followerCountValue(),
                    notes: editForm.notes.trimmed.isEmpty ? nil : editForm.notes.trimmed,
                    assignedToId: editForm.assignedToId.trimmed.isEmpty ? nil : editForm.assignedToId.trimmed
                )
            )
            guard selectedContactId == contactId else { return }
            upsertContact(response.contact)
            selectedContact = response.contact
            editForm.populate(from: response.contact)
            linkedUserId = response.contact.userId ?? ""
        } catch let error as VouchaError {
            detailErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            detailErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    func archiveSelectedContact() async {
        guard let client, let contactId = selectedContactId, !isArchiving else { return }
        isArchiving = true
        defer { isArchiving = false }
        do {
            let _: EmptyResponse = try await client.send(.archiveCrmContact(contactId: contactId))
            contacts.removeAll { $0.id == contactId }
            selectedContact = nil
            selectedContactId = nil
            selectedSocialAccounts = []
            selectedEmails = []
            selectedNotes = []
            canShowArchiveConfirmation = false
        } catch let error as VouchaError {
            detailErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            detailErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    func linkSelectedContactToUser() async {
        guard let client, let contactId = selectedContactId, !isLinking else { return }
        let userId = linkedUserId.trimmed
        guard !userId.isEmpty else {
            detailErrorMessage = .message(.nativeSwiftValidationUserIdRequired)
            return
        }

        isLinking = true
        defer { isLinking = false }
        do {
            let response: CrmContactResponse = try await client.send(
                .linkCrmContactToUser(contactId: contactId, userId: userId)
            )
            upsertContact(response.contact)
            selectedContact = response.contact
            editForm.populate(from: response.contact)
            linkedUserId = response.contact.userId ?? ""
        } catch let error as VouchaError {
            detailErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            detailErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    func unlinkSelectedContactFromUser() async {
        guard let client, let contactId = selectedContactId, !isLinking else { return }
        isLinking = true
        defer { isLinking = false }
        do {
            let response: CrmContactResponse = try await client.send(.unlinkCrmContactFromUser(contactId: contactId))
            upsertContact(response.contact)
            selectedContact = response.contact
            editForm.populate(from: response.contact)
            linkedUserId = response.contact.userId ?? ""
        } catch let error as VouchaError {
            detailErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            detailErrorMessage = .verbatim(error.localizedDescription)
        }
    }

    func upsertContact(_ contact: CrmContact) {
        if let index = contacts.firstIndex(where: { $0.id == contact.id }) {
            contacts[index] = contact
        } else {
            contacts.insert(contact, at: 0)
        }
    }
}
