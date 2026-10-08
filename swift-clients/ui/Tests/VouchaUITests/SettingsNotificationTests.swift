import Foundation
import Observation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class SettingsNotificationTests: NativeRouteSurfaceViewModelTestCase {
    func testEachFieldUsesOneFieldEmailPreferencesPatch() async throws {
        let cases: [(String, (NotificationSettingsViewModel) async -> Void)] = [
            (#""is_engagement_emails_enabled":false"#, { await $0.setEngagement(false) }),
            (#""news_digest_frequency":"daily""#, { await $0.setNewsDigest("daily") }),
            (#""is_moderation_emails_enabled":false"#, { await $0.setModeration(false) }),
            (#""community_digest_frequency":"none""#, { await $0.setCommunityDigest("none") }),
            (#""moderation_email_cadence":"selected_days""#, { await $0.setCadence("selected_days") }),
            (#""moderation_email_days_of_week":[1,2,3,4]"#, { await $0.setDay(4, selected: true) }),
            (#""moderation_email_time_of_day":"08:30""#, { await $0.setTime("08:30") }),
            (#""moderation_email_timezone":"UTC""#, { await $0.setTimezone("UTC") })
        ]
        for (expected, mutate) in cases {
            CannedFeedURLProtocol.handlers["/api/v1/my/email-preferences"] = (preferences(), 200)
            let model = try NotificationSettingsViewModel(
                client: makeClient(),
                timezoneResolver: { "America/Los_Angeles" }
            )
            await model.load()
            CannedFeedURLProtocol.handlers["/api/v1/my/email-preferences"] = (preferences(), 200)
            await mutate(model)
            let body = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.last.flatMap { $0 })
            XCTAssertTrue(body.contains(expected), "expected \(expected) in \(body)")
        }
    }

    func testModerationVisibilityPreservesScheduleAndFinalDayCannotBeRemoved() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/email-preferences"] = (preferences(days: [1]), 200)
        let model = try NotificationSettingsViewModel(client: makeClient())
        await model.load()
        XCTAssertTrue(model.shouldShowModerationSchedule)
        CannedFeedURLProtocol.handlers["/api/v1/my/email-preferences"] = (
            preferences(days: [1], moderation: false),
            200
        )
        await model.setModeration(false)
        XCTAssertFalse(model.shouldShowModerationSchedule)
        CannedFeedURLProtocol.handlers["/api/v1/my/email-preferences"] = (preferences(days: [1]), 200)
        await model.setModeration(true)
        XCTAssertEqual(model.current?.days, [1])
        await model.setDay(1, selected: false)
        XCTAssertEqual(model.current?.days, [1])
    }

    func testRenderedModerationScheduleAppearsAndDisappearsWithEnabledPreference() async throws {
        let path = "/api/v1/my/email-preferences"
        CannedFeedURLProtocol.handlers[path] = (preferences(cadence: "selected_days"), 200)
        let notifications = try NotificationSettingsViewModel(client: makeClient())
        await notifications.load()
        let surface = SettingsSurface(
            viewModel: SettingsViewModel(client: nil),
            focusedSection: .notifications,
            notificationSettingsViewModel: notifications
        )

        try await ViewHosting.host(surface) {
            XCTAssertEqual(try surface.inspect().findAll(ViewType.DatePicker.self).count, 1)
            XCTAssertEqual(try surface.inspect().findAll(ViewType.Toggle.self).count, 9)

            CannedFeedURLProtocol.handlers[path] = (preferences(moderation: false, cadence: "selected_days"), 200)
            try moderationToggle(in: surface).tap()
            await waitForModerationCommit(notifications, enabled: false)
            XCTAssertEqual(try surface.inspect().findAll(ViewType.DatePicker.self).count, 0)
            XCTAssertEqual(try surface.inspect().findAll(ViewType.Toggle.self).count, 2)

            CannedFeedURLProtocol.handlers[path] = (preferences(cadence: "selected_days"), 200)
            try moderationToggle(in: surface).tap()
            await waitForModerationCommit(notifications, enabled: true)
            XCTAssertEqual(try surface.inspect().findAll(ViewType.DatePicker.self).count, 1)
            XCTAssertEqual(try surface.inspect().findAll(ViewType.Toggle.self).count, 9)
        }
    }

    func testReturnedResponseCommitsOnlyTheMutatedField() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/email-preferences"] = (preferences(), 200)
        let model = try NotificationSettingsViewModel(client: makeClient())
        await model.load()
        CannedFeedURLProtocol.handlers["/api/v1/my/email-preferences"] = (preferences(news: "none"), 200)

        await model.setEngagement(false)

        XCTAssertEqual(model.current?.engagement, true)
        XCTAssertEqual(model.current?.newsDigest, "weekly")
        XCTAssertEqual(model.committed?.newsDigest, "weekly")
    }

    func testFailureRollsBackOnlyTheFailedField() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/email-preferences"] = (preferences(), 200)
        let model = try NotificationSettingsViewModel(client: makeClient())
        await model.load()
        CannedFeedURLProtocol.handlers["/api/v1/my/email-preferences"] = (Data("{}".utf8), 500)

        await model.setEngagement(false)

        XCTAssertEqual(model.current?.engagement, true)
        XCTAssertEqual(model.current?.newsDigest, "weekly")
        XCTAssertFalse(model.isPending(.engagement))
    }

    func testFocusedNotificationLoadFailureRendersRetryInsteadOfFallbackControls() throws {
        let notificationViewModel = try NotificationSettingsViewModel(client: makeClient())
        notificationViewModel.state = .error(.api(statusCode: 500, preconditionCode: nil))
        let settingsViewModel = try SettingsViewModel(client: makeClient())
        let sut = SettingsSurface(
            viewModel: settingsViewModel,
            focusedSection: .notifications,
            notificationSettingsViewModel: notificationViewModel
        )

        XCTAssertNoThrow(try sut.inspect().find(button: "Try Again"))
        XCTAssertThrowsError(try sut.inspect().find(ViewType.Toggle.self))
    }

    func testRefreshFailureWithLoadedPreferencesRendersRetryAndKeepsControls() async throws {
        let path = "/api/v1/my/email-preferences"
        CannedFeedURLProtocol.handlers[path] = (preferences(), 200)
        let notificationViewModel = try NotificationSettingsViewModel(client: makeClient())
        await notificationViewModel.load()
        CannedFeedURLProtocol.handlers[path] = (Data("{}".utf8), 500)
        await notificationViewModel.load()
        let settingsViewModel = try SettingsViewModel(client: makeClient())
        let sut = SettingsSurface(
            viewModel: settingsViewModel,
            focusedSection: .notifications,
            notificationSettingsViewModel: notificationViewModel
        )

        XCTAssertNoThrow(try sut.inspect().find(button: "Try Again"))
        XCTAssertNoThrow(try sut.inspect().find(ViewType.Toggle.self))
        XCTAssertEqual(notificationViewModel.current?.newsDigest, "weekly")
    }

    func testNewestOverlappingLoadWinsWhenEarlierLoadFinishesLast() async throws {
        let path = "/api/v1/my/email-preferences"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (preferences(news: "none"), 200, 0),
            (preferences(news: "daily"), 200, 0)
        ]
        CannedFeedURLProtocol.suspendResponse(path: path)
        let model = try NotificationSettingsViewModel(client: makeClient())

        let olderLoad = Task { await model.load() }
        addTeardownBlock {
            olderLoad.cancel()
            CannedFeedURLProtocol.releaseResponse(path: path)
            await olderLoad.value
        }
        try await waitForPendingRequests(path, minimumCount: 1)
        let newerLoad = Task { await model.load() }
        addTeardownBlock {
            newerLoad.cancel()
            CannedFeedURLProtocol.releaseResponse(path: path)
            await newerLoad.value
        }
        try await waitForPendingRequests(path, minimumCount: 2)
        CannedFeedURLProtocol.releaseNewestResponse(path: path)
        await newerLoad.value
        CannedFeedURLProtocol.releaseResponse(path: path)
        await olderLoad.value

        XCTAssertEqual(model.current?.newsDigest, "daily")
        if case .loaded = model.state {} else {
            XCTFail("Expected newest load to leave the model loaded")
        }
    }

    func testNotificationMutationFailureRendersStatusMessage() async throws {
        let path = "/api/v1/my/email-preferences"
        CannedFeedURLProtocol.handlers[path] = (preferences(), 200)
        let notificationViewModel = try NotificationSettingsViewModel(client: makeClient())
        await notificationViewModel.load()
        CannedFeedURLProtocol.handlers[path] = (Data("{}".utf8), 500)
        await notificationViewModel.setEngagement(false)
        let settingsViewModel = try SettingsViewModel(client: makeClient())
        let sut = SettingsSurface(
            viewModel: settingsViewModel,
            focusedSection: .notifications,
            notificationSettingsViewModel: notificationViewModel
        )

        XCTAssertEqual(uiEnglish(notificationViewModel.statusMessage), "An error occurred.")
        XCTAssertNoThrow(try sut.inspect().find(text: "An error occurred."))
    }

    func testFocusedNotificationRefreshReloadsNotificationSettings() async throws {
        let path = "/api/v1/my/email-preferences"
        CannedFeedURLProtocol.handlers[path] = (preferences(), 200)
        let notificationViewModel = try NotificationSettingsViewModel(client: makeClient())
        let settingsViewModel = SettingsViewModel(client: nil)
        let sut = SettingsSurface(
            viewModel: settingsViewModel,
            focusedSection: .notifications,
            notificationSettingsViewModel: notificationViewModel
        )

        await sut.reload()

        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 1)
        XCTAssertNotNil(notificationViewModel.current)
    }

    func testSameFieldDoesNotStartAnotherRequestWhilePending() async throws {
        let path = "/api/v1/my/email-preferences"
        CannedFeedURLProtocol.handlers[path] = (preferences(), 200)
        let model = try NotificationSettingsViewModel(client: makeClient())
        await model.load()
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }

        let firstRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "PATCH")
        let first = Task { await model.setEngagement(false) }
        _ = try await firstRequest.wait(timeout: .seconds(2))
        let secondFinished = expectation(description: "deduplicated engagement mutation finished")
        let second = Task { await model.setEngagement(true) }
        Task {
            await second.value
            secondFinished.fulfill()
        }
        await fulfillment(of: [secondFinished], timeout: 2)

        XCTAssertTrue(model.isPending(.engagement))
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 2)
        CannedFeedURLProtocol.releaseResponse(path: path)
        await first.value
        await second.value
    }

    func testReloadStartedDuringPendingMutationDoesNotOverwriteSuccessfulMutationWithLateGet() async throws {
        let path = "/api/v1/my/email-preferences"
        CannedFeedURLProtocol.handlers[path] = (preferences(), 200)
        let model = try NotificationSettingsViewModel(client: makeClient())
        await model.load()
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (preferences(engagement: false), 200, 0),
            (preferences(engagement: true), 200, 0)
        ]
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }

        let mutationRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "PATCH")
        let mutation = Task { await model.setEngagement(false) }
        _ = try await mutationRequest.wait(timeout: .seconds(2))
        let reloadRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let reload = Task { await model.load() }
        _ = try await reloadRequest.wait(timeout: .seconds(2))

        CannedFeedURLProtocol.releaseResponse(path: path)
        await mutation.value
        await reload.value

        XCTAssertEqual(model.current?.engagement, false)
        XCTAssertEqual(model.committed?.engagement, false)
    }

    func testConcurrentDifferentFieldsKeepTheirOwnReturnedValues() async throws {
        let path = "/api/v1/my/email-preferences"
        CannedFeedURLProtocol.handlers[path] = (preferences(), 200)
        let model = try NotificationSettingsViewModel(client: makeClient())
        await model.load()
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (preferences(engagement: false), 200, 0),
            (preferences(news: "daily"), 200, 0)
        ]
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }

        let engagementRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "PATCH")
        let engagement = Task { await model.setEngagement(false) }
        _ = try await engagementRequest.wait(timeout: .seconds(2))
        let newsRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "PATCH")
        let news = Task { await model.setNewsDigest("daily") }
        _ = try await newsRequest.wait(timeout: .seconds(2))
        CannedFeedURLProtocol.releaseResponse(path: path)
        await engagement.value
        await news.value

        XCTAssertEqual(model.current?.engagement, false)
        XCTAssertEqual(model.current?.newsDigest, "daily")
    }

    func testTimezonePersistsRecognizedDetectedZoneOnlyWhenUnset() async throws {
        let path = "/api/v1/my/email-preferences"
        CannedFeedURLProtocol.handlers[path] = (preferences(timezone: nil), 200)
        let model = try NotificationSettingsViewModel(client: makeClient(), timezoneResolver: { "America/Los_Angeles" })

        await model.load()

        XCTAssertEqual(model.current?.timezone, "America/Los_Angeles")
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 2)
        let body = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.last ?? nil)
        let payload = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(body.utf8)) as? [String: String])
        XCTAssertEqual(payload["moderation_email_timezone"], "America/Los_Angeles")
    }

    func testFailedAutomaticTimezonePatchRetriesOnLaterUnsetLoad() async throws {
        let path = "/api/v1/my/email-preferences"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (preferences(timezone: nil), 200, 0),
            (Data("{}".utf8), 500, 0),
            (preferences(timezone: nil), 200, 0),
            (preferences(timezone: "America/Los_Angeles"), 200, 0)
        ]
        let model = try NotificationSettingsViewModel(
            client: makeClient(),
            timezoneResolver: { "America/Los_Angeles" }
        )

        await model.load()
        await model.load()

        XCTAssertEqual(model.current?.timezone, "America/Los_Angeles")
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 4)
    }

    func testInvalidDetectedTimezoneDisplaysLosAngelesWithoutWriting() async throws {
        let path = "/api/v1/my/email-preferences"
        CannedFeedURLProtocol.handlers[path] = (preferences(timezone: nil), 200)
        let model = try NotificationSettingsViewModel(client: makeClient(), timezoneResolver: { "invalid/timezone" })

        await model.load()

        XCTAssertEqual(model.current?.timezone, "America/Los_Angeles")
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 1)
    }

    func testSavedTimezoneIsPreservedWithoutOverwrite() async throws {
        let path = "/api/v1/my/email-preferences"
        CannedFeedURLProtocol.handlers[path] = (preferences(timezone: "UTC"), 200)
        let model = try NotificationSettingsViewModel(client: makeClient(), timezoneResolver: { "America/Los_Angeles" })

        await model.load()

        XCTAssertEqual(model.current?.timezone, "UTC")
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 1)
    }

    func testNonemptyBackendSavedTimezoneIsPreservedWithoutLocalValidation() async throws {
        let savedTimezone = "America/Backend_Only"
        let path = "/api/v1/my/email-preferences"
        CannedFeedURLProtocol.handlers[path] = (preferences(timezone: savedTimezone), 200)
        let model = try NotificationSettingsViewModel(client: makeClient(), timezoneResolver: { "UTC" })

        await model.load()

        XCTAssertEqual(model.current?.timezone, savedTimezone)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 1)
    }

    func testStaleUnsetTimezoneLoadDoesNotOverwriteNewerExplicitTimezone() async throws {
        let path = "/api/v1/my/email-preferences"
        CannedFeedURLProtocol.handlers[path] = (preferences(timezone: "UTC"), 200)
        let model = try NotificationSettingsViewModel(client: makeClient(), timezoneResolver: { "America/Los_Angeles" })
        await model.load()

        CannedFeedURLProtocol.queuedHandlers[path] = [
            (preferences(timezone: nil), 200, 0),
            (preferences(timezone: "Europe/Paris"), 200, 0)
        ]
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }

        let staleLoadBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let staleLoad = Task { await model.load() }
        _ = try await staleLoadBarrier.wait()

        let explicitTimezoneBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "PATCH")
        let explicitTimezone = Task { await model.setTimezone("Europe/Paris") }
        _ = try await explicitTimezoneBarrier.wait()

        CannedFeedURLProtocol.releaseResponse(path: path)
        await staleLoad.value
        await explicitTimezone.value

        XCTAssertEqual(model.current?.timezone, "Europe/Paris")
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 3)
    }

    func testRenderedNotificationControlsUseISOWeekdayLabelsAndDisableOnlyPendingField() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/email-preferences"] = (
            preferences(days: Array(1 ... 7), cadence: "selected_days", timezone: "UTC"),
            200
        )
        let notificationViewModel = try NotificationSettingsViewModel(client: makeClient())
        await notificationViewModel.load()
        notificationViewModel.pendingFields = [.engagement, .timezone]
        let settingsViewModel = try SettingsViewModel(client: makeClient())
        let sut = SettingsSurface(
            viewModel: settingsViewModel,
            focusedSection: .notifications,
            notificationSettingsViewModel: notificationViewModel
        )
        .environment(\.locale, Locale(identifier: "en_US"))

        let inspection = try sut.inspect()
        let toggles = try inspection.findAll(ViewType.Toggle.self)
        XCTAssertEqual(try toggles[2].labelView().text().string(), "Mon")
        XCTAssertEqual(try toggles[8].labelView().text().string(), "Sun")
        XCTAssertTrue(toggles[0].isDisabled())
        XCTAssertFalse(toggles[1].isDisabled())
        XCTAssertTrue(try inspection.findAll(ViewType.Picker.self)[3].isDisabled())
    }

    func testAccessibilityActivationFocusesAndAnnouncesLocalizedMessageOnlyOnce() {
        var focusedCount = 0
        var announcements: [String] = []
        let activation = NotificationA11yActivator(announce: { announcements.append($0) })

        let announcement = uiEnglish(.nativeSwiftSettingsNotificationSettingsOpened)
        activation.activate(token: 1, focus: { focusedCount += 1 }, announcement: announcement)
        activation.activate(token: 1, focus: { focusedCount += 1 }, announcement: announcement)
        activation.activate(token: 2, focus: { focusedCount += 1 }, announcement: announcement)

        XCTAssertEqual(focusedCount, 2)
        XCTAssertEqual(announcements, [announcement, announcement])
        XCTAssertEqual(activation.announcementCount, 2)
    }

    private func moderationToggle(in surface: SettingsSurface) throws -> InspectableView<ViewType.Toggle> {
        try surface.inspect().find(ViewType.Toggle.self, where: {
            try $0.labelView().text().string() == "Moderation emails"
        })
    }

    private func waitForModerationCommit(_ model: NotificationSettingsViewModel, enabled: Bool) async {
        let committed = expectation(description: "moderation preference commits as \(enabled)")
        var active = true
        defer { active = false }
        func observe() {
            guard active else { return }
            if model.committed?.moderation == enabled, !model.isPending(.moderation) {
                committed.fulfill()
                return
            }
            withObservationTracking {
                _ = model.committed?.moderation
                _ = model.pendingFields
            } onChange: {
                Task { @MainActor in observe() }
            }
        }
        observe()
        await fulfillment(of: [committed], timeout: 2)
    }

    private func preferences(
        days: [Int] = [1, 2, 3],
        moderation: Bool = true,
        engagement: Bool = true,
        news: String = "weekly",
        cadence: String = "daily",
        timezone: String? = "America/Los_Angeles"
    ) -> Data {
        let timezoneJSON = timezone.map { "\"\($0)\"" } ?? "null"
        return Data("""
        {"email_preferences":{"is_engagement_emails_enabled":\(engagement),"news_digest_frequency":"\(
            news
        )","is_moderation_emails_enabled":\(
            moderation
        ),"community_digest_frequency":"weekly","moderation_email_cadence":"\(
            cadence
        )","moderation_email_days_of_week":\(
            days
        ),"moderation_email_time_of_day":"09:00","moderation_email_timezone":\(
            timezoneJSON
        )}}
        """.utf8)
    }

    private func waitForPendingRequests(_ path: String, minimumCount: Int) async throws {
        let deadline = ContinuousClock.now + .seconds(2)
        while ContinuousClock.now < deadline,
              !pendingRequestsAreReady(path, minimumCount: minimumCount) {
            await Task.yield()
        }
        _ = try XCTUnwrap(
            pendingRequestsAreReady(path, minimumCount: minimumCount) ? true : nil,
            "Timed out waiting for \(minimumCount) requests at \(path)"
        )
    }

    private func pendingRequestsAreReady(_ path: String, minimumCount: Int) -> Bool {
        CannedFeedURLProtocol.hasSuspendedResponse(path: path, minimumCount: minimumCount)
    }
}
