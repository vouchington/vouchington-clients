import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ModerationAppealsSurfaceTests: XCTestCase {
    func testStaffSurfaceRendersFiltersAppealContextDraftAndActions() throws {
        let appeal = try fixtureAppeal(suspensionId: "suspension-1")
        let viewModel = ModerationAppealsViewModel(
            client: nil, isSignedIn: true, isAdministrator: false, isSiteModerator: true
        )
        viewModel.appeals = [appeal]
        viewModel.seedDrafts(from: [appeal])
        let inspection = try ModerationAppealsSurface(viewModel: viewModel).inspect()

        XCTAssertNoThrow(try inspection.find(text: "Pending"))
        XCTAssertNoThrow(try inspection.find(text: "Suspension appeal"))
        XCTAssertNoThrow(try inspection.find(text: "Case case-appeal-1"))
        XCTAssertNoThrow(try inspection.find(text: "Appellant user-1"))
        XCTAssertNoThrow(try inspection.find(text: "Target suspension-1"))
        XCTAssertNoThrow(try inspection.find(text: "Please reconsider."))
        XCTAssertNoThrow(try inspection.find(text: "AI recommendation: Reduce"))
        XCTAssertNoThrow(try inspection.find(button: "Save"))
        XCTAssertNoThrow(try inspection.find(button: "Approve"))
        XCTAssertNoThrow(try inspection.find(button: "Send"))
        XCTAssertNoThrow(try inspection.find(button: "Re-run AI"))
        XCTAssertTrue(try inspection.find(button: "Accept").isDisabled())
        XCTAssertTrue(try inspection.find(button: "Reduce").isDisabled())
    }

    func testResolutionActionsAreEnabledOnlyAfterAppealDelivery() throws {
        let unsent = try fixtureAppeal(suspensionId: nil)
        let sent = try fixtureAppeal(
            suspensionId: nil,
            sentAt: "2026-07-01T12:00:00Z"
        )
        let viewModel = ModerationAppealsViewModel(
            client: nil, isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )

        viewModel.appeals = [unsent]
        viewModel.seedDrafts(from: [unsent])
        let unsentInspection = try ModerationAppealsSurface(viewModel: viewModel).inspect()
        XCTAssertTrue(try unsentInspection.find(button: "Accept").isDisabled())
        XCTAssertTrue(try unsentInspection.find(button: "Reduce").isDisabled())
        XCTAssertTrue(try unsentInspection.find(button: "Deny").isDisabled())

        viewModel.appeals = [sent]
        viewModel.seedDrafts(from: [sent])
        let sentInspection = try ModerationAppealsSurface(viewModel: viewModel).inspect()
        XCTAssertFalse(try sentInspection.find(button: "Accept").isDisabled())
        XCTAssertFalse(try sentInspection.find(button: "Reduce").isDisabled())
        XCTAssertFalse(try sentInspection.find(button: "Deny").isDisabled())
    }

    func testMemberAndAnonymousSurfacesRenderAccessMessages() throws {
        let member = ModerationAppealsSurface(
            client: nil, isSignedIn: true, isAdministrator: false, isSiteModerator: false
        )
        let anonymous = ModerationAppealsSurface(
            client: nil, isSignedIn: false, isAdministrator: false, isSiteModerator: false
        )

        XCTAssertNoThrow(try member.inspect().find(text: "Staff access required"))
        XCTAssertNoThrow(try anonymous.inspect().find(text: "Sign in required"))
    }

    func testCardDisplaysLifecycleTimestampsAndReadOnlyStaffContext() throws {
        let appeal = try fixtureAppeal(
            suspensionId: nil,
            draftedAt: "2026-07-01T10:15:00Z",
            editedAt: "2026-07-01T10:30:00Z",
            editedById: "editor-1",
            approvedAt: "2026-07-01T11:00:00Z",
            approvedById: "approver-1",
            sentAt: "2026-07-01T12:00:00Z",
            resolvedAt: "2026-07-01T13:00:00Z",
            resolvedById: "resolver-1",
            resolutionAction: "reduce",
            internalNotes: "Staff-only history"
        )
        let viewModel = ModerationAppealsViewModel(
            client: nil, isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )
        viewModel.appeals = [appeal]
        viewModel.seedDrafts(from: [appeal])
        let inspection = try ModerationAppealsSurface(viewModel: viewModel).inspect()

        XCTAssertNoThrow(try inspection.find(text: "Drafted"))
        XCTAssertNoThrow(try inspection.find(text: "Edited"))
        XCTAssertNoThrow(try inspection.find(text: "by editor-1"))
        XCTAssertNoThrow(try inspection.find(text: "Approved"))
        XCTAssertNoThrow(try inspection.find(text: "by approver-1"))
        XCTAssertNoThrow(try inspection.find(text: "Sent"))
        XCTAssertNoThrow(try inspection.find(text: "Resolved"))
        XCTAssertNoThrow(try inspection.find(text: "by resolver-1"))
        XCTAssertNoThrow(try inspection.find(text: "Resolution: Reduce"))
        XCTAssertNoThrow(try inspection.find(text: "Internal AI response"))
        XCTAssertNoThrow(try inspection.find(text: "Internal AI context"))
        XCTAssertNoThrow(try inspection.find(text: "Internal notes"))
        XCTAssertNoThrow(try inspection.find(text: "Staff-only history"))
        XCTAssertThrowsError(try inspection.find(button: "Edit internal notes"))
        XCTAssertTrue(try inspection.find(button: "Re-run AI").isDisabled())
    }

    func testCardActionsAndEditorHaveAppealSpecificAccessibilityLabels() throws {
        let appeal = try fixtureAppeal(suspensionId: nil)
        let viewModel = ModerationAppealsViewModel(
            client: nil, isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )
        viewModel.appeals = [appeal]
        viewModel.seedDrafts(from: [appeal])
        let inspection = try ModerationAppealsSurface(viewModel: viewModel).inspect()

        for label in [
            "Public response for appeal appeal-1",
            "Save public response for appeal appeal-1",
            "Approve appeal appeal-1",
            "Send appeal appeal-1",
            "Re-run AI for appeal appeal-1",
            "Accept appeal appeal-1",
            "Reduce appeal appeal-1",
            "Deny appeal appeal-1"
        ] {
            XCTAssertNoThrow(try inspection.find(viewWithAccessibilityLabel: label))
        }
    }

    func testCardEditorMarksDraftDirtyThroughViewModelSetter() throws {
        let appeal = try fixtureAppeal(suspensionId: nil)
        let viewModel = ModerationAppealsViewModel(
            client: nil, isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )
        viewModel.appeals = [appeal]
        viewModel.seedDrafts(from: [appeal])
        let editor = try ModerationAppealsSurface(viewModel: viewModel)
            .inspect()
            .find(ViewType.TextEditor.self)

        try editor.setInput("Local edit")
        viewModel.seedDrafts(from: [appeal])

        XCTAssertEqual(viewModel.drafts[appeal.id], "Local edit")
    }

    private func fixtureAppeal(
        suspensionId: String?,
        draftedAt: String? = nil,
        editedAt: String? = nil,
        editedById: String? = nil,
        approvedAt: String? = nil,
        approvedById: String? = "admin-1",
        sentAt: String? = nil,
        resolvedAt: String? = nil,
        resolvedById: String? = nil,
        resolutionAction: String? = nil,
        internalNotes: String? = nil
    ) throws -> ModerationAppeal {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(
            ModerationAppeal.self,
            from: Data(ModerationAppealsTestSupport.appeal(
                suspensionId: suspensionId,
                internalNotes: internalNotes,
                draftedAt: draftedAt,
                editedAt: editedAt,
                editedById: editedById,
                approvedAt: approvedAt,
                approvedById: approvedById,
                sentAt: sentAt,
                resolvedAt: resolvedAt,
                resolvedById: resolvedById,
                resolutionAction: resolutionAction
            ).utf8)
        )
    }
}
