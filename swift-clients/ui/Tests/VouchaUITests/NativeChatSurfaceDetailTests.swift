import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeChatSurfaceDetailTests: XCTestCase {
    func testChatDetailShowsProviderPickerForNewConversations() throws {
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil)
        viewModel.selectedConversationId = nil

        let detail = try NativeChatSurface(client: nil, routeMatch: nil)
            .chatDetail(viewModel: viewModel)
            .inspect()

        XCTAssertEqual(try detail.find(text: "New conversation").string(), "New conversation")
        XCTAssertNoThrow(try detail.find(ViewType.Picker.self))
        XCTAssertNoThrow(try detail.find(button: "Reset"))
    }
}
