import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

@MainActor
extension SpendingCategoriesViewModel {
    func create(spendingCategoryId: String, draft: SpendingCategoryDraft) async -> Bool {
        guard !isCreating else { return false }
        do { isCreating = true
            defer { isCreating = false }
            try await upsert(service.create(body: draft.createBody(spendingCategoryId: spendingCategoryId)))
            return true
        } catch is SpendingCategoryDraftError {
            mutationErrorMessage = .message(.extractedMySpendingCategoriesManagerPleaseEnterAvalidAmount010f1dd6)
            return false
        } catch { mutationErrorMessage = .verbatim(error.localizedDescription)
            return false
        }
    }

    func save(category: SpendingCategory, draft: SpendingCategoryDraft) async -> Bool {
        guard category.canManage, !mutatingIds.contains(category.id) else { return false }
        do { guard let body = try draft.updateBody(comparedWith: category) else { return true }
            mutatingIds.insert(category.id)
            defer { mutatingIds.remove(category.id) }
            try await upsert(service.update(id: category.id, body: body))
            return true
        } catch is SpendingCategoryDraftError {
            mutationErrorMessage = .message(.extractedMySpendingCategoriesManagerPleaseEnterAvalidAmount010f1dd6)
            return false
        } catch { mutationErrorMessage = .verbatim(error.localizedDescription)
            return false
        }
    }

    func delete(_ category: SpendingCategory) async -> Bool {
        guard category.canManage, !mutatingIds.contains(category.id),
              categories.contains(where: { $0.id == category.id }) else { return false }
        mutatingIds.insert(category.id)
        deletedIds.insert(category.id)
        let local = localUpserts.removeValue(forKey: category.id)
        categories = categories.filter { $0.id != category.id }
        defer { mutatingIds.remove(category.id) }
        do { try await service.delete(id: category.id)
            return true
        } catch {
            deletedIds.remove(category.id)
            if let local {
                localUpserts[category.id] = local
            }
            categories += [category]
            mutationErrorMessage = .verbatim(error.localizedDescription)
            return false
        }
    }

    func upsert(_ category: SpendingCategory) {
        deletedIds.remove(category.id)
        localUpserts[category.id] = category
        categories = categories.filter { $0.id != category.id } + [category]
        mutationErrorMessage = nil
    }

    func reconciled(_ server: [SpendingCategory]) -> [SpendingCategory] {
        var values = Dictionary(server.map { ($0.id, $0) }, uniquingKeysWith: { _, new in new })
        values.merge(localUpserts, uniquingKeysWith: { _, local in local })
        return values.values.filter { !deletedIds.contains($0.id) }.sorted { $0.id < $1.id }
    }

    func paginationError(_ error: Error) -> VouchaError {
        (error as? VouchaError) ?? .unexpected(error.localizedDescription)
    }
}
