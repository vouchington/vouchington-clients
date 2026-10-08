import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

extension CommunityDetailViewModel {
    func loadInitialCommunityAutomodActions(limit: Int) async {
        automodPagination.reset()
        state = .loading
        await loadCommunityAutomodPage(limit: limit)
        if automodPagination.hasLoadedPage {
            statusMessage = UiMessage(.nativeSwiftCommunityStatusLoadedRecentAutomodActions)
            state = .loaded
        } else {
            state = .error(UiMessage(.nativeSwiftCommunityStatusUnableToLoadRecentAutomodActions))
        }
    }

    func loadMoreCommunityAutomodActions(limit: Int = 10) async {
        await loadCommunityAutomodPage(limit: limit)
    }

    private func loadCommunityAutomodPage(limit: Int) async {
        guard let client, let request = automodPagination.beginNextPage() else { return }
        do {
            let response: CommunityAutomodRecentActionsResponse = try await client.send(
                .communityAutomodRecentActions(idOrSlug: slug, after: request.cursor, limit: limit)
            )
            let rows = response.automodActions.map { action in
                CommunityForwardRow(id: action.sourceKey, row: automodRow(action))
            }
            guard automodPagination.complete(
                request,
                items: rows,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            ) else { return }
            summary.rows = automodPagination.items.map(\.row)
        } catch let error as VouchaError {
            _ = automodPagination.fail(request, error: error)
        } catch {
            _ = automodPagination.fail(request, error: .unexpected(error.localizedDescription))
        }
    }

    private func automodRow(_ action: CommunityAutomodAction) -> NativeRouteDestinationRow {
        NativeRouteDestinationRow(
            icon: action.isFlagged ? "exclamationmark.triangle" : "checkmark.shield",
            title: .verbatim(action.authoredTitle ?? action.title),
            detail: .joined([
                .message(action.currentState.titleKey),
                action.confidenceScore.map {
                    .message(.nativeSwiftRouteSurfaceNumberValue, numberParameters: ["value": $0])
                }
            ].compactMap { $0 }),
            declaredLanguage: action.declaredLanguage,
            detectedLanguage: action.linguaRsDetectedLanguage
        )
    }
}
