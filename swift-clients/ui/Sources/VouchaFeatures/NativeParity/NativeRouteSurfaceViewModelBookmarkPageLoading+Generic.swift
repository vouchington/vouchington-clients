extension NativeRouteSurfaceViewModel {
    func bookmarkedGenericRow(
        _ entity: NativeGenericEntity,
        response: NativeGenericListResponse,
        collection: NativeBookmarkCollection,
        rank: Int
    ) -> NativeBookmarkRow {
        let entityType = collection.kind == .users ? "user" : collection.entityType
        let authoredTitle = entity.normalizedAuthoredTitle
        return NativeBookmarkRow(
            entityType: collection.entityType,
            entityId: entity.id,
            icon: collection.kind == .users
                ? "person"
                : genericBookmarkIcon(entity, response: response, fallback: collection.icon),
            title: genericBookmarkTitle(entity, entityType: entityType),
            detail: genericBookmarkDetail(entity, entityType: entityType),
            destination: .path(genericBookmarkPath(entity, entityType: entityType)),
            inverseAction: collection.inverseAction,
            rank: rank,
            declaredLanguage: authoredTitle?.declaredLanguage,
            detectedLanguage: authoredTitle?.detectedLanguage
        )
    }
}
