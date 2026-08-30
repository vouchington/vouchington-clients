import Foundation

public enum NativePostComposeCategoryType: String, CaseIterable, Identifiable, Sendable {
    case topic
    case hashtag

    public var id: Self {
        self
    }
}

public struct NativePostComposeCategoryDraft: Identifiable, Equatable, Sendable {
    public let id: UUID
    public var type: NativePostComposeCategoryType
    public var value: String

    public init(
        id: UUID = UUID(),
        type: NativePostComposeCategoryType = .topic,
        value: String = ""
    ) {
        self.id = id
        self.type = type
        self.value = value
    }
}

public extension NativePostComposeViewModel {
    func addCategory() {
        categoryDrafts.append(.init())
    }

    func updateCategoryType(id: UUID, type: NativePostComposeCategoryType) {
        guard let index = categoryDrafts.firstIndex(where: { $0.id == id }) else { return }
        categoryDrafts[index].type = type
    }

    func updateCategoryValue(id: UUID, value: String) {
        guard let index = categoryDrafts.firstIndex(where: { $0.id == id }) else { return }
        categoryDrafts[index].value = value
    }

    func removeCategory(id: UUID) {
        categoryDrafts.removeAll { $0.id == id }
    }
}
