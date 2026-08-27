import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ReportIntegrityConfirmationSurfaceTests: XCTestCase {
    func testPenaltyActionPresentsLocalizedConfirmationAndCancelClearsIt() throws {
        let flag = try IntegrityTestSupport.reportFlag()
        let service = try ConfirmationReportIntegrityService(
            flag: flag,
            penaltyResponse: IntegrityTestSupport.reportPenalty(
                flag: IntegrityTestSupport.reportFlag(resolution: "penalized")
            )
        )
        let viewModel = ReportIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.flags = [flag]
        let surface = ReportIntegritySurface(viewModel: viewModel)

        try surface.inspect().find(button: "Investigate and penalize reporters").tap()

        XCTAssertTrue(service.penaltyCalls.isEmpty)
        let confirmingSurface = ReportIntegritySurface(
            viewModel: viewModel,
            pendingPenaltyFlag: flag
        )
        let dialog = try confirmingSurface.inspect().find(ViewType.VStack.self).confirmationDialog()
        XCTAssertEqual(try dialog.title().string(), "Confirm action")
        XCTAssertNoThrow(try dialog.message().find(text: "Investigate and penalize reporters"))
        let confirm = try dialog.actions().find(button: "Investigate and penalize reporters")

        try dialog.actions().find(button: "Cancel").tap()
        try confirm.tap()

        XCTAssertTrue(service.penaltyCalls.isEmpty)
    }

    func testPenaltyConfirmationSubmitsExactlyOnce() async throws {
        let flag = try IntegrityTestSupport.reportFlag()
        let penalized = try IntegrityTestSupport.reportFlag(resolution: "penalized")
        let service = try ConfirmationReportIntegrityService(
            flag: flag,
            penaltyResponse: IntegrityTestSupport.reportPenalty(flag: penalized)
        )
        let viewModel = ReportIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.flags = [flag]
        let surface = ReportIntegritySurface(viewModel: viewModel, pendingPenaltyFlag: flag)
        let confirm = try surface.inspect()
            .find(ViewType.VStack.self)
            .confirmationDialog()
            .actions()
            .find(button: "Investigate and penalize reporters")

        try confirm.tap()
        await waitForPenaltyCall(service)
        await Task.yield()

        XCTAssertEqual(service.penaltyCalls, [flag.id])
    }

    func testConfirmationCannotBypassAuthorizationCapabilityOrReconciliationGuards() async throws {
        let pending = try IntegrityTestSupport.reportFlag()
        let resolved = try IntegrityTestSupport.reportFlag(resolution: "penalized")

        let memberViewModel = ReportIntegrityViewModel(service: nil, viewerTier: .member)
        memberViewModel.flags = [pending]
        let memberSurface = ReportIntegritySurface(
            viewModel: memberViewModel,
            pendingPenaltyFlag: pending
        )
        XCTAssertThrowsError(try memberSurface.inspect().find(button: "Investigate and penalize reporters"))
        XCTAssertThrowsError(try memberSurface.inspect().find(ViewType.VStack.self).confirmationDialog())

        let resolvedViewModel = ReportIntegrityViewModel(service: nil, viewerTier: .administrator)
        resolvedViewModel.flags = [resolved]
        XCTAssertThrowsError(try ReportIntegritySurface(viewModel: resolvedViewModel)
            .inspect().find(button: "Investigate and penalize reporters"))

        let reconcilingService = try ConfirmationReportIntegrityService(
            flag: pending,
            penaltyResponse: IntegrityTestSupport.reportPenalty(flag: resolved)
        )
        let reconcilingViewModel = ReportIntegrityViewModel(
            service: reconcilingService,
            viewerTier: .administrator
        )
        reconcilingViewModel.flags = [pending]
        reconcilingViewModel.reconciliationRequiredFlagIds = [pending.id]
        let disabledInspection = try ReportIntegritySurface(viewModel: reconcilingViewModel).inspect()
        XCTAssertTrue(try disabledInspection
            .find(button: "Investigate and penalize reporters").isDisabled())

        let reconcilingConfirmation = ReportIntegritySurface(
            viewModel: reconcilingViewModel,
            pendingPenaltyFlag: pending
        )
        try reconcilingConfirmation.inspect().find(ViewType.VStack.self)
            .confirmationDialog()
            .actions()
            .find(button: "Investigate and penalize reporters")
            .tap()
        await Task.yield()

        XCTAssertTrue(reconcilingService.penaltyCalls.isEmpty)
    }

    private func waitForPenaltyCall(_ service: ConfirmationReportIntegrityService) async {
        for _ in 0 ..< 100 where service.penaltyCalls.isEmpty {
            await Task.yield()
        }
    }
}

@MainActor
private final class ConfirmationReportIntegrityService: ReportIntegrityServicing {
    let flagValue: ReportIntegrityFlag
    let penaltyResponse: ReportIntegrityPenaltyResponse
    var penaltyCalls: [String] = []

    init(flag: ReportIntegrityFlag, penaltyResponse: ReportIntegrityPenaltyResponse) {
        flagValue = flag
        self.penaltyResponse = penaltyResponse
    }

    func flags(
        status _: IntegrityFlagStatus?,
        after _: String?,
        limit _: Int
    ) async throws -> ReportIntegrityFlagsResponse {
        try IntegrityTestSupport.reportPage([flagValue])
    }

    func dismiss(flagId _: String) async throws -> ReportIntegrityFlag {
        flagValue
    }

    func flag(id _: String) async throws -> ReportIntegrityFlag {
        flagValue
    }

    func penalizeReporters(flagId: String) async throws -> ReportIntegrityPenaltyResponse {
        penaltyCalls.append(flagId)
        return penaltyResponse
    }
}
