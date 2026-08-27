import Foundation
import ViewInspector
import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class ProfileViewModelImageUploadTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testUploadAvatarOverridesStaleProfileImageId() async throws {
        let vm = try makeViewModel()
        let imageId = "avatar-image-1"
        let avatarURL = try makeImageFile(name: "avatar-image", extension: "jpg", data: Data("avatar-image".utf8))
        try registerImageUploadFlow(
            imageId: imageId,
            uploadURL: XCTUnwrap(URL(string: "https://upload.example.test/\(imageId)")),
            contentType: "image/jpeg",
            stateBody: #"{"upload_state":{"id":"\#(imageId)","upload_status":"complete","upload_error":null,"ready":true,"blocked":false}}"#
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(
                membershipPlan: "free",
                roles: ["user"],
                profileImageId: "old-avatar"
            ),
            200
        )

        await vm.uploadAvatar(from: avatarURL)

        XCTAssertEqual(vm.identity?.profileImageId, imageId)
        XCTAssertNil(vm.avatarUploadErrorMessage)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/images/upload-url",
            "/\(imageId)",
            "/api/v1/images/\(imageId)/completions",
            "/api/v1/images/\(imageId)/upload-state",
            "/api/v1/my/identity"
        ])
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST", "PUT", "POST", "GET", "PATCH"])

        let body = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.last.flatMap { $0 })
        XCTAssertTrue(body.contains(#""profile_image_id":"\#(imageId)""#))
    }

    func testCompleteAvatarUploadWithoutReadyStillUpdatesIdentity() async throws {
        let vm = try makeViewModel()
        let imageId = "avatar-complete-not-ready"
        let avatarURL = try makeImageFile(name: "complete-avatar", extension: "jpg", data: Data("complete-avatar".utf8))
        try registerImageUploadFlow(
            imageId: imageId,
            uploadURL: XCTUnwrap(URL(string: "https://upload.example.test/\(imageId)")),
            contentType: "image/jpeg",
            stateBody: #"{"upload_state":{"id":"\#(imageId)","upload_status":"complete","upload_error":null,"ready":false,"blocked":false}}"#
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(
                membershipPlan: "free",
                roles: ["user"],
                profileImageId: "old-avatar"
            ),
            200
        )

        await vm.uploadAvatar(from: avatarURL)

        XCTAssertEqual(vm.identity?.profileImageId, imageId)
        XCTAssertNil(vm.avatarUploadErrorMessage)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST", "PUT", "POST", "GET", "PATCH"])
    }

    func testBlockedAvatarUploadSurfacesErrorWithoutIdentityPatch() async throws {
        let vm = try makeViewModel()
        let imageId = "blocked-avatar"
        let avatarURL = try makeImageFile(name: "blocked-avatar", extension: "jpg", data: Data("blocked".utf8))
        try registerImageUploadFlow(
            imageId: imageId,
            uploadURL: XCTUnwrap(URL(string: "https://upload.example.test/\(imageId)")),
            contentType: "image/jpeg",
            stateBody: #"{"upload_state":{"id":"\#(imageId)","upload_status":"complete","upload_error":null,"ready":false,"blocked":true}}"#
        )

        await vm.uploadAvatar(from: avatarURL)

        XCTAssertEqual(vm.avatarUploadErrorMessage, .app(UiMessage(.nativeSwiftImageSelectionBlocked)))
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.map(\.path).contains("/api/v1/my/identity"))
    }

    func testUploadAvatarFromMissingFileAndRemoveFailureSurfaceErrors() async throws {
        let vm = try makeViewModel()
        let missingURL = FileManager.default.temporaryDirectory
            .appendingPathComponent("missing-avatar-\(UUID().uuidString)")
            .appendingPathExtension("jpg")

        await vm.uploadAvatar(from: missingURL)
        XCTAssertNotNil(vm.avatarUploadErrorMessage)

        vm.avatarUploadErrorMessage = nil
        await vm.removeAvatar()
        XCTAssertNotNil(vm.avatarUploadErrorMessage)
    }

    func testRemoveAvatarIsIgnoredWhileAvatarUploadIsInFlight() async throws {
        let vm = try makeViewModel()
        let avatarURL = try makeImageFile(name: "avatar-in-flight", extension: "jpg", data: Data("avatar".utf8))
        let imageId = "avatar-in-flight-1"
        try registerImageUploadFlow(
            imageId: imageId,
            uploadURL: XCTUnwrap(URL(string: "https://upload.example.test/\(imageId)")),
            contentType: "image/jpeg",
            stateBody: #"{"upload_state":{"id":"\#(imageId)","upload_status":"complete","upload_error":null,"ready":true,"blocked":false}}"#,
            uploadStateDelay: 0.2
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(
                membershipPlan: "free",
                roles: ["user"],
                profileImageId: imageId
            ),
            200
        )

        let uploadTask = Task {
            await vm.uploadAvatar(from: avatarURL)
        }
        for _ in 0 ..< 100 {
            if vm.isUploadingAvatar {
                break
            }
            await Task.yield()
        }
        XCTAssertTrue(vm.isUploadingAvatar)

        await vm.removeAvatar()
        await uploadTask.value

        XCTAssertEqual(vm.identity?.profileImageId, imageId)
        XCTAssertNil(vm.avatarUploadErrorMessage)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.filter { $0 == "PATCH" }.count, 1)
    }

    func testRemoveAvatarOverridesStaleProfileImageId() async throws {
        let vm = try makeViewModel()
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(
                membershipPlan: "free",
                roles: ["user"],
                profileImageId: "stale-avatar"
            ),
            200
        )

        await vm.removeAvatar()

        XCTAssertNil(vm.identity?.profileImageId)
        XCTAssertNil(vm.avatarUploadErrorMessage)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), ["/api/v1/my/identity"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["PATCH"])
        let body = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.first.flatMap { $0 })
        XCTAssertTrue(body.contains(#""profile_image_id":null"#))
    }

    func testProfileViewRendersAvatarActionsProgressAndErrors() throws {
        let vm = try makeViewModel()
        vm.identity = try makeIdentity(profileImageId: "image-1")
        vm.isUploadingAvatar = true
        vm.avatarUploadErrorMessage = .verbatim("Avatar failed.")
        let sut = ProfileView(viewModel: vm)

        XCTAssertGreaterThanOrEqual(try sut.inspect().findAll(ViewType.Button.self).count, 3)
        XCTAssertNoThrow(try sut.inspect().find(ViewType.ProgressView.self))
        XCTAssertEqual(try sut.inspect().find(text: "Avatar failed.").string(), "Avatar failed.")
    }

    func testProfileViewRendersBioMarkdownNatively() throws {
        let vm = try makeViewModel()
        vm.identity = try makeIdentity(profileImageId: nil)
        vm.bio = "_Native bio_"
        let sut = ProfileView(viewModel: vm)

        XCTAssertEqual(try sut.inspect().find(text: "Bio").string(), "Bio")
        XCTAssertEqual(try sut.inspect().find(text: "_Native bio_").string(), "_Native bio_")
    }

    func testEditBioViewRendersMarkdownEditor() throws {
        let sut = EditBioView(client: nil, currentBio: "**Existing**") { _ in }

        XCTAssertEqual(
            try sut.inspect().find(text: "Edit your bio. Markdown is supported.").string(),
            "Edit your bio. Markdown is supported."
        )
        XCTAssertNoThrow(try sut.inspect().find(ViewType.TextEditor.self))
    }

    private func makeViewModel() throws -> ProfileViewModel {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let cookieStorage = HTTPCookieStorage()
        let apiClient = APIClient(
            config: config,
            cookieStorage: cookieStorage,
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        let sessionManager = SessionManager(client: apiClient, cookieStorage: cookieStorage)
        return ProfileViewModel(
            client: apiClient,
            config: config,
            sessionManager: sessionManager,
            imageUploadProtocolClasses: [CannedFeedURLProtocol.self]
        )
    }

    private func registerImageUploadFlow(
        imageId: String,
        uploadURL: URL,
        contentType: String,
        stateBody: String,
        uploadStateDelay: TimeInterval = 0
    ) {
        CannedFeedURLProtocol.handlers["/api/v1/images/upload-url"] = (
            Data("""
            {"upload":{"image_id":"\(imageId)","upload_url":"\(uploadURL
                .absoluteString)","content_type":"\(contentType)","expires_at":"2026-01-01T00:00:00Z"}}
            """.utf8),
            201
        )
        CannedFeedURLProtocol.handlers[uploadURL.path] = (Data(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/images/\(imageId)/completions"] = (
            Data(#"{"image":{"id":"\#(imageId)","upload_status":"processing"}}"#.utf8),
            200
        )
        if uploadStateDelay > 0 {
            CannedFeedURLProtocol.queuedHandlers["/api/v1/images/\(imageId)/upload-state"] = [
                (Data(stateBody.utf8), 200, uploadStateDelay)
            ]
        } else {
            CannedFeedURLProtocol.handlers["/api/v1/images/\(imageId)/upload-state"] = (
                Data(stateBody.utf8),
                200
            )
        }
    }

    private func makeImageFile(name: String, extension fileExtension: String, data: Data) throws -> URL {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("\(name)-\(UUID().uuidString)")
            .appendingPathExtension(fileExtension)
        try data.write(to: url)
        return url
    }

    private func makeIdentity(profileImageId: String?, markdown: String? = nil) throws -> PrivateUser {
        let data = PrivateUserTestFixture.userData(
            membershipPlan: "free",
            verificationStatus: nil,
            roles: ["user"],
            profileImageId: profileImageId,
            markdown: markdown
        )
        return try JSONDecoder.vouchaFixtureDecoder.decode(PrivateUser.self, from: data)
    }
}
