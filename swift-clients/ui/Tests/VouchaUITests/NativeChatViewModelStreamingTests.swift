import Foundation
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class NativeChatViewModelStreamingTests: XCTestCase {

    func testChatViewModelClearsStreamErrorWhenSelectionChangesOrResets() async {
        let viewModel = NativeChatViewModel(client: nil, routeMatch: nil)
        viewModel.selectedConversationId = "conversation-1"
        viewModel.streamErrorMessage = .verbatim("Stream failed")

        await viewModel.selectConversation(id: "conversation-2")
        XCTAssertNil(viewModel.streamErrorMessage)

        viewModel.streamErrorMessage = .verbatim("Stream failed")
        await viewModel.selectConversation(id: nil)

        XCTAssertNil(viewModel.streamErrorMessage)
    }

}
