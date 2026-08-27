import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func loadHostnameDetailRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let rawHostname = routeMatch?.param("idOrHostname") ?? routeMatch?.path.routeLastSegment ?? ""
        let response: NativeHostnameDetailResponse = try await client.send(.hostname(idOrHostname: rawHostname))
        let hostname = response.hostname
        applyHostnameDetailResponse(response)
        detailReportTarget = hostname.blocked ? nil : .urlHostname(id: hostname.id)
        var rows = hostnameDetailRows(response: response)
        insertTopicRow(response.topic, into: &rows)
        appendRssFeedsRow(response.rssFeeds, to: &rows)
        return rows
    }

    private func applyHostnameDetailResponse(_ response: NativeHostnameDetailResponse) {
        hostnameDetailId = response.hostname.id
        hostnameDetailHostname = response.hostname
        let vote = response.electionVote?.choice ?? response.hostnameElection?.myVote
        hostnameDetailVote = vote
        hostnameDetailElection = response.hostnameElection.map {
            TopicElection(
                votesScoreNet: $0.votesScoreNet,
                votesCountUp: $0.votesCountUp,
                votesCountDown: $0.votesCountDown,
                myVote: vote
            )
        }
    }

    private func hostnameDetailRows(response: NativeHostnameDetailResponse) -> [NativeRouteDestinationRow] {
        let hostname = response.hostname
        return [
            row("globe", rawText(hostname.hostname), hostnameStatusDetail(hostname: hostname)),
            hostnameTrustVoteRow(
                hostname: hostname,
                election: response.hostnameElection,
                vote: response.electionVote?.choice
            ),
            row(
                "speaker.wave.2",
                appText(.nativeSwiftRouteSurfaceMute),
                bookmarkDetail(hostname: hostname, predicates: ["mute", "muted"])
            ),
            row(
                "nosign",
                appText(.nativeSwiftRouteSurfaceBlock),
                bookmarkDetail(hostname: hostname, predicates: ["block", "blocked"])
            ),
            row(
                "link",
                appText(.nativeSwiftRouteSurfaceTopUrls),
                response.topUrls.map {
                    appText(
                        .nativeSwiftRouteSurfaceLoadedCount,
                        numberParameters: ["count": Double($0.count)]
                    )
                } ?? appText(.nativeSwiftRouteSurfaceNoUrlsLoaded)
            )
        ]
    }

    private func insertTopicRow(_ topic: NativeTopicSummary?, into rows: inout [NativeRouteDestinationRow]) {
        if let topic {
            rows.insert(row("tag", appText(.nativeSwiftRouteSurfaceTopic), rawText(topic.name)), at: 1)
        }
    }

    private func appendRssFeedsRow(_ rssFeeds: [NativeRssFeedSummary]?, to rows: inout [NativeRouteDestinationRow]) {
        if let rssFeeds, !rssFeeds.isEmpty {
            rows.append(row(
                "newspaper",
                appText(.nativeSwiftRouteSurfaceRssFeeds),
                appText(
                    .nativeSwiftRouteSurfaceLoadedCount,
                    numberParameters: ["count": Double(rssFeeds.count)]
                )
            ))
        }
    }

    private func hostnameStatusDetail(hostname: NativeHostnameSummary) -> UiVerbatimText {
        .joined([
            hostname.blocked ? appText(.nativeSwiftRouteSurfaceBlocked) : nil,
            hostname.crawlable.map {
                appText($0 ? .nativeSwiftRouteSurfaceCrawlable : .nativeSwiftRouteSurfaceNotCrawlable)
            },
            hostname.linkRelFollow.map {
                appText($0 ? .nativeSwiftRouteSurfaceFollowable : .nativeSwiftRouteSurfaceNotFollowable)
            }
        ].compactMap { $0 })
    }

    func refreshHostnameTrustRow() {
        guard let hostname = hostnameDetailHostname else { return }
        rows = rows.map { row in
            row.id == hostnameTrustRowId(hostname.id)
                ? hostnameTrustVoteRow(hostname: hostname, election: hostnameDetailElection, vote: hostnameDetailVote)
                : row
        }
    }

    private func hostnameTrustVoteRow(
        hostname: NativeHostnameSummary,
        election: TopicElection?,
        vote: ElectionVoteChoice?
    ) -> NativeRouteDestinationRow {
        .init(
            id: hostnameTrustRowId(hostname.id),
            icon: "chevron.up.chevron.down",
            title: appText(.nativeSwiftRouteSurfaceTrustVote),
            detail: hostnameVoteDetail(hostname: hostname, election: election, vote: vote)
        )
    }

    private func hostnameTrustRowId(_ hostnameId: String) -> String {
        "hostname-trust-\(hostnameId)"
    }

    private func hostnameVoteDetail(
        hostname: NativeHostnameSummary,
        election: TopicElection?,
        vote: ElectionVoteChoice?
    ) -> UiVerbatimText {
        if let vote {
            return .verbatim(UiMessages.string(hostnameVoteChoiceKey(vote), locale: .current))
        }
        if let election {
            return .joined([
                .count(election.votesCountUp, item: "positiveVote"),
                .count(election.votesCountDown, item: "negativeVote")
            ])
        }
        return hostname.topicId.map {
            appText(.nativeSwiftRouteSurfaceTopicValue, parameters: ["value": $0])
        } ?? appText(.nativeSwiftRouteSurfaceVoteThisHostname)
    }

    private func hostnameVoteChoiceKey(_ choice: ElectionVoteChoice) -> UiMessageKey {
        UiMessageKey(rawValue: "extracted.votes.semanticVote.\(choice.rawValue)")
    }

    private func bookmarkDetail(hostname: NativeHostnameSummary, predicates: [String]) -> UiVerbatimText {
        if hostname.blocked, predicates.contains("block") {
            return appText(.nativeSwiftRouteSurfaceBlocked)
        }
        return appText(
            predicates.contains("block")
                ? .nativeSwiftRouteSurfaceAvailableToBlock
                : .nativeSwiftRouteSurfaceMuteThisHostname
        )
    }
}
