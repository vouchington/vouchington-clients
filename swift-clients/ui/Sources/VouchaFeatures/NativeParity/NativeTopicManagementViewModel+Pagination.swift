import VouchaAPI
import VouchaCore
import VouchaModels

@MainActor
extension NativeTopicManagementViewModel {
    var additionalHostnames: [TopicAdditionalHostname] {
        get { additionalHostnamesPagination.items }
        set { additionalHostnamesPagination.reset(items: newValue) }
    }

    var aliases: [TopicAlias] {
        aliasesPagination.items
    }

    func loadMoreAliases() async {
        guard let client, let topicIdentifier else { return }
        guard let request = aliasesPagination.beginNextPage() else { return }
        do {
            let response: Page<TopicAlias> = try await client.send(
                .topicAliases(topicId: topicIdentifier, after: request.cursor)
            )
            aliasesPagination.complete(
                request,
                items: response.results,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch is CancellationError {
            aliasesPagination.cancel(request)
        } catch let error as VouchaError {
            if Task.isCancelled {
                aliasesPagination.cancel(request)
                return
            }
            aliasesPagination.fail(request, error: error)
        } catch {
            if Task.isCancelled {
                aliasesPagination.cancel(request)
                return
            }
            aliasesPagination.fail(request, error: .unexpected(error.localizedDescription))
        }
    }

    func loadMoreAdditionalHostnames() async {
        guard let client, let topicIdentifier else { return }
        guard let request = additionalHostnamesPagination.beginNextPage() else { return }
        do {
            let response: TopicAdditionalHostnamesResponse = try await client.send(
                .topicAdditionalHostnames(topicId: topicIdentifier, after: request.cursor)
            )
            additionalHostnamesPagination.complete(
                request,
                items: response.results,
                endCursor: response.pageInfo.endCursor,
                hasNextPage: response.pageInfo.hasNextPage
            )
        } catch is CancellationError {
            additionalHostnamesPagination.cancel(request)
        } catch let error as VouchaError {
            if Task.isCancelled {
                additionalHostnamesPagination.cancel(request)
                return
            }
            additionalHostnamesPagination.fail(request, error: error)
        } catch {
            if Task.isCancelled {
                additionalHostnamesPagination.cancel(request)
                return
            }
            additionalHostnamesPagination.fail(request, error: .unexpected(error.localizedDescription))
        }
    }
}
