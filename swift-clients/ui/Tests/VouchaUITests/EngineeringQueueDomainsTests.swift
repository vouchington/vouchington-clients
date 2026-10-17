import Foundation
import SwiftUI
import ViewInspector
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class EngineeringQueueDomainsTests: NativeRouteSurfaceViewModelTestCase {
    func testBackgroundQueueUsesDomainCountsAndHidesWholeQueuePause() async throws {
        stubLoad()
        let model = try NativeEngineeringQueuesViewModel(client: makeClient())
        await model.load()
        XCTAssertFalse(model.domains.isEmpty)
        let surface = try NativeEngineeringQueuesSurface(entry: entry(for: .engineeringQueues), client: nil)
        let rendered = try surface.loadedContent(model).inspect()
        XCTAssertNoThrow(try rendered.find(text: "Background domains"))
        for domain in model.domains {
            XCTAssertNoThrow(try rendered.find(text: domain.domain))
        }
        let queueOnly = try NativeEngineeringQueuesSurface(entry: entry(for: .engineeringQueues), client: nil)
        model.domains = []
        XCTAssertThrowsError(try queueOnly.loadedContent(model).inspect().find(button: "Pause"))
        await model.pauseQueue(name: "background")
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/mq/queues/background/pause" })
    }

    func testDomainActionsUseScopedRoutesAndRefreshCounts() async throws {
        stubLoad()
        let model = try NativeEngineeringQueuesViewModel(client: makeClient())
        await model.load()
        let domain = try XCTUnwrap(model.domains.first?.domain)
        for action in ["pause", "resume", "retry-failed"] {
            CannedFeedURLProtocol.handlers["/api/v1/mq/queues/background/domains/\(domain)/\(action)"] = (
                ApiFixtureLoader.data("web.admin.mq.queues.domains.\(action).default"), 200
            )
        }
        await model.pauseDomain(domain, reason: "Provider outage")
        await model.resumeDomain(domain)
        await model.retryFailedDomain(domain)
        for action in ["pause", "resume", "retry-failed"] {
            XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
                $0.path == "/api/v1/mq/queues/background/domains/\(domain)/\(action)"
            })
        }
        XCTAssertNil(model.actionErrorMessage)
        XCTAssertTrue(model.actionLoadingKeys.isEmpty)
        CannedFeedURLProtocol.handlers["/api/v1/mq/queues/background/domains/\(domain)/pause"] = (
            Data(#"{"error":"unavailable"}"#.utf8), 500
        )
        await model.pauseDomain(domain)
        XCTAssertNotNil(model.actionErrorMessage)
        XCTAssertFalse(model.domains.isEmpty)
    }

    func testDomainRowsRenderCappedCountsPauseReasonAndButtons() throws {
        let model = try NativeEngineeringQueuesViewModel(client: makeClient())
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        var fixture = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("web.admin.mq.queues.domains.default")
        ) as? [String: Any])
        var domains = try XCTUnwrap(fixture["domains"] as? [[String: Any]])
        let pausedIndex = try XCTUnwrap(domains.firstIndex { $0["paused"] as? Bool == true })
        let literalReason = "native.common.retry <paused> {reason}"
        domains[pausedIndex]["reason"] = literalReason
        fixture["domains"] = domains
        model.domains = try decoder.decode(
            EngineeringQueueDomainsResponse.self,
            from: JSONSerialization.data(withJSONObject: fixture)
        ).domains
        let inspection = try NativeEngineeringQueueDomainsSection(viewModel: model).inspect()
        XCTAssertNoThrow(try inspection.find(text: "Paused: \(literalReason)"))
        XCTAssertNoThrow(try inspection.find(button: "Pause"))
        XCTAssertNoThrow(try inspection.find(button: "Resume"))
        XCTAssertNoThrow(try inspection.find(button: "Retry failed"))
        for domain in model.domains {
            for value in [domain.waiting, domain.active, domain.delayed, domain.failed, domain.completed] {
                XCTAssertNoThrow(try inspection.find(text: UiMessages.queueDomainCount(
                    value.count,
                    complete: value.complete,
                    locale: Locale(identifier: "en")
                )))
            }
            if let reason = domain.reason, domain.paused {
                XCTAssertNoThrow(try inspection.find(text: "Paused: \(reason)"))
            }
        }
        try model.actionLoadingKeys.insert("domain|\(XCTUnwrap(model.domains.first?.domain))")
        let disabled = try NativeEngineeringQueueDomainsSection(viewModel: model).inspect()
            .findAll(ViewType.Button.self)
        XCTAssertTrue(try disabled.contains { try $0.isDisabled() })
    }

    func testConcurrentDomainMutationRefreshesAfterAnAlreadyCapturedLoad() async throws {
        stubLoad()
        let model = try NativeEngineeringQueuesViewModel(client: makeClient())
        await model.load()
        let firstDomain = try XCTUnwrap(model.domains.first?.domain)
        let secondDomain = try XCTUnwrap(model.domains.dropFirst().first?.domain)
        for domain in [firstDomain, secondDomain] {
            CannedFeedURLProtocol.handlers["/api/v1/mq/queues/background/domains/\(domain)/pause"] = (
                ApiFixtureLoader.data("web.admin.mq.queues.domains.pause.default"), 200
            )
        }
        let domainsPath = "/api/v1/mq/queues/background/domains"
        let capturedStaleLoad = expectation(description: "First mutation reload captured")
        CannedFeedURLProtocol.suspendResponse(path: domainsPath) { capturedStaleLoad.fulfill() }
        let firstMutation = Task { await model.pauseDomain(firstDomain) }
        await fulfillment(of: [capturedStaleLoad], timeout: 2)

        var response = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("web.admin.mq.queues.domains.default")
        ) as? [String: Any])
        var domains = try XCTUnwrap(response["domains"] as? [[String: Any]])
        let index = try XCTUnwrap(domains.firstIndex { $0["domain"] as? String == secondDomain })
        domains[index]["waiting"] = ["count": 99, "complete": true]
        response["domains"] = domains
        CannedFeedURLProtocol.handlers[domainsPath] = try (JSONSerialization.data(withJSONObject: response), 200)
        await model.pauseDomain(secondDomain)
        CannedFeedURLProtocol.releaseResponse(path: domainsPath)
        await firstMutation.value

        XCTAssertEqual(model.domains.first { $0.domain == secondDomain }?.waiting.count, 99)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == domainsPath }.count, 3)
        XCTAssertTrue(model.actionLoadingKeys.isEmpty)
    }

    func testDomainCountFormattingKeepsLowerBoundMarkerAfterLocalizedNumber() {
        XCTAssertEqual(UiMessages.queueDomainCount(1_234, complete: false, locale: Locale(identifier: "en")), "1,234+")
        XCTAssertEqual(UiMessages.queueDomainCount(1_234, complete: true, locale: Locale(identifier: "en")), "1,234")
        XCTAssertEqual(
            UiMessages.queueDomainCount(1_234, complete: false, locale: Locale(identifier: "fr")),
            "1\u{202F}234+"
        )
    }

    func testCanceledReloadDoesNotDrainQueuedWorkAndAllowsNextExplicitLoad() async throws {
        stubLoad()
        let model = try NativeEngineeringQueuesViewModel(client: makeClient())
        let domainsPath = "/api/v1/mq/queues/background/domains"
        let capturedLoad = expectation(description: "Canceled load captured")
        CannedFeedURLProtocol.suspendResponse(path: domainsPath) { capturedLoad.fulfill() }
        let load = Task { await model.load() }
        await fulfillment(of: [capturedLoad], timeout: 2)
        await model.load()
        load.cancel()
        CannedFeedURLProtocol.releaseResponse(path: domainsPath)
        await load.value
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == domainsPath }.count, 1)

        await model.load()

        XCTAssertFalse(model.domains.isEmpty)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == domainsPath }.count, 2)
    }

    private func stubLoad() {
        CannedFeedURLProtocol.handlers["/api/v1/mq/stats"] = (
            ApiFixtureLoader.data("web.admin.mq.stats.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/mq/queues"] = (Data("""
        {"queues":[{"name":"background","waiting":10,"active":2,"delayed":3,"failed":4,"completed":20,"paused":false}],"total":1}
        """.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/mq/scheduled-jobs"] = (Data(#"{"jobs":[]}"#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/mq/backfills"] = (Data(#"{"backfills":[]}"#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/mq/queues/background/domains"] = (
            ApiFixtureLoader.data("web.admin.mq.queues.domains.default"), 200
        )
    }
}
