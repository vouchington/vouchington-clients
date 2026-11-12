extension SectionDetailView {
    func sourceCreationIdentity() -> String? {
        factory.sessionManager.currentUserId.map { "\($0)|\(ObjectIdentifier(factory.apiClient))" }
    }
}
