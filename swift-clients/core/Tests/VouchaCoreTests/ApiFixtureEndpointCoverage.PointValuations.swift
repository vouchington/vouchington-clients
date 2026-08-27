@testable import VouchaAPI
import VouchaModels

extension ApiFixtureEndpointCoverageTests {
    static let pointValuationFixtureEndpoints: [String: Endpoint] = [
        "native.rewards-program-topics.search.default": .rewardsProgramTopics(query: "Travel"),
        "native.point-valuations.empty": .pointValuations(),
        "native.point-valuations.page-1": .pointValuations(limit: 2),
        "native.point-valuations.page-2": .pointValuations(after: pointValuationPageCursor, limit: 2),
        "native.point-valuations.create.default": .createPointValuation(body: createPointValuationBody),
        "native.point-valuations.update.full": .updatePointValuation(
            id: firstPointValuationId,
            body: fullPointValuationUpdateBody
        ),
        "native.point-valuations.update.clear-note": .updatePointValuation(
            id: secondPointValuationId,
            body: clearPointValuationNoteBody
        ),
        "native.point-valuations.delete.default": .deletePointValuation(id: firstPointValuationId)
    ]

    static let firstPointValuationId = "00000000-0000-7000-8000-000000000721"
    static let secondPointValuationId = "00000000-0000-7000-8000-000000000722"
    static let firstRewardsProgramId = "00000000-0000-7000-8000-000000000731"
    static let pointValuationPageCursor = "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDcyMiIsInNjb3BlIjoibXktcG9pbnQtdmFsdWF0aW9uczowMDAwMDAwMC0wMDAwLTcwMDAtODAwMC0wMDAwMDAwMDA3MjA6aWQtYXNjIn0"
    static let createPointValuationBody = CreatePointValuationBody(
        rewardsProgramId: firstRewardsProgramId,
        valuePerPoint: try! ScaledMoney(amount: 35_000, currency: "usd"),
        note: "Use for flexible travel redemptions"
    )
    static let fullPointValuationUpdateBody = UpdatePointValuationBody(
        valuePerPoint: try! ScaledMoney(amount: 35_000, currency: "usd"),
        note: .value("Use for flexible travel redemptions")
    )
    static let clearPointValuationNoteBody = UpdatePointValuationBody(
        valuePerPoint: try! ScaledMoney(amount: 0, currency: "usd"),
        note: .null
    )
}
