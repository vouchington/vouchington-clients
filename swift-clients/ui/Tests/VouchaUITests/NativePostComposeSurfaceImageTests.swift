import ViewInspector
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class NativePostComposeSurfaceImageTests: XCTestCase {
    private let turnstileSiteKey = "test-site-key"

    func testImageSectionRendersPickerProgressErrorAndImageRows() throws {
        let viewModel = NativePostComposeViewModel(client: nil)
        viewModel.images = try [
            NativePostComposeImageDraft(
                id: XCTUnwrap(UUID(uuidString: "00000000-0000-0000-0000-000000000001")),
                imageId: "image-1",
                caption: "One"
            ),
            NativePostComposeImageDraft(
                id: XCTUnwrap(UUID(uuidString: "00000000-0000-0000-0000-000000000002")),
                imageId: "image-2",
                caption: "Two"
            )
        ]
        viewModel.isUploadingImages = true
        viewModel.imageUploadErrorMessage = .verbatim("Upload failed.")
        let surface = NativePostComposeSurface(client: nil, turnstileSiteKey: turnstileSiteKey)
        let sut = surface.imageSection(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(button: "Add images"))
        XCTAssertNoThrow(try sut.inspect().find(ViewType.ProgressView.self))
        XCTAssertEqual(try sut.inspect().find(text: "Upload failed.").string(), "Upload failed.")
        XCTAssertEqual(try sut.inspect().find(text: "Images").string(), "Images")
        XCTAssertEqual(try sut.inspect().find(text: "Image 1").string(), "Image 1")
        XCTAssertEqual(try sut.inspect().find(text: "Image 2").string(), "Image 2")
    }

    func testImagePickerIsDisabledWhilePublishing() throws {
        let viewModel = NativePostComposeViewModel(client: nil)
        viewModel.state = .loading
        let surface = NativePostComposeSurface(client: nil, turnstileSiteKey: turnstileSiteKey)
        let sut = surface.imageSection(viewModel: viewModel)

        XCTAssertTrue(try sut.inspect().find(button: "Add images").isDisabled())
    }

    func testImageSectionButtonsUpdateImageDrafts() throws {
        let firstId = try XCTUnwrap(UUID(uuidString: "00000000-0000-0000-0000-000000000011"))
        let secondId = try XCTUnwrap(UUID(uuidString: "00000000-0000-0000-0000-000000000012"))
        let viewModel = NativePostComposeViewModel(client: nil)
        viewModel.images = [
            NativePostComposeImageDraft(id: firstId, imageId: "image-1", caption: ""),
            NativePostComposeImageDraft(id: secondId, imageId: "image-2", caption: "")
        ]
        let surface = NativePostComposeSurface(client: nil, turnstileSiteKey: turnstileSiteKey)
        let sut = surface.imageSection(viewModel: viewModel)
        let buttons = try sut.inspect().findAll(ViewType.Button.self)

        try buttons[4].tap()
        XCTAssertEqual(viewModel.images.map(\.imageId), ["image-2", "image-1"])

        try buttons[6].tap()
        XCTAssertEqual(viewModel.images.map(\.imageId), ["image-1"])
    }

    func testImageCaptionFieldReadsLatestDraftCaptionById() throws {
        let imageId = try XCTUnwrap(UUID(uuidString: "00000000-0000-0000-0000-000000000021"))
        let viewModel = NativePostComposeViewModel(client: nil)
        viewModel.images = [
            NativePostComposeImageDraft(id: imageId, imageId: "image-1", caption: "Initial")
        ]
        let surface = NativePostComposeSurface(client: nil, turnstileSiteKey: turnstileSiteKey)
        let sut = surface.imageSection(viewModel: viewModel)
        let captionField = try sut.inspect().find(ViewType.TextField.self)

        XCTAssertEqual(try captionField.input(), "Initial")

        viewModel.updateImageCaption(id: imageId, caption: "Updated")

        XCTAssertEqual(try captionField.input(), "Updated")
    }

    func testImageCaptionDraftInitializationCapsToOneThousandCharacters() throws {
        let imageId = try XCTUnwrap(UUID(uuidString: "00000000-0000-0000-0000-000000000031"))
        let longCaption = String(repeating: "a", count: 1_200)
        let viewModel = NativePostComposeViewModel(client: nil)
        viewModel.images = [
            NativePostComposeImageDraft(id: imageId, imageId: "image-1", caption: longCaption)
        ]
        let surface = NativePostComposeSurface(client: nil, turnstileSiteKey: turnstileSiteKey)
        let sut = surface.imageSection(viewModel: viewModel)
        let captionField = try sut.inspect().find(ViewType.TextField.self)
        let expectedCaption = String(repeating: "a", count: 1_000)

        XCTAssertEqual(try captionField.input(), expectedCaption)
        XCTAssertEqual(viewModel.images.first?.caption, expectedCaption)
    }

    func testImageCaptionDraftInitializationCapsToBackendUtf16Length() throws {
        let imageId = try XCTUnwrap(UUID(uuidString: "00000000-0000-0000-0000-000000000032"))
        let emojiCaption = String(repeating: "😀", count: 600)
        let viewModel = NativePostComposeViewModel(client: nil)
        viewModel.images = [
            NativePostComposeImageDraft(id: imageId, imageId: "image-1", caption: emojiCaption)
        ]

        let caption = try XCTUnwrap(viewModel.images.first?.caption)
        XCTAssertEqual(caption.count, 500)
        XCTAssertEqual(caption.utf16.count, 1_000)
    }

    func testFullComposeSurfaceRendersImageImporterControlsAndTypeFields() throws {
        let sut = NativePostComposeSurface(client: nil, initialPostType: .link, turnstileSiteKey: turnstileSiteKey)

        XCTAssertNoThrow(try sut.inspect().find(button: "Add images"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Verify"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Save draft"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Publish"))
        XCTAssertGreaterThanOrEqual(try sut.inspect().findAll(ViewType.TextField.self).count, 2)
    }
}
