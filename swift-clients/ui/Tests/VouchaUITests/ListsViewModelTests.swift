import Foundation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ListsViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testLoadSelectsFirstListAndLoadsItems() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (Self.listsData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-1/items"] = (Self.itemsData, 200)
        let viewModel = try ListsViewModel(client: makeClient())

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/lists",
            "/api/v1/lists/list-1/items"
        ])
        XCTAssertEqual(viewModel.selectedList?.name, "Reading Queue")
        XCTAssertEqual(viewModel.items.map(\.id), ["list-item-1", "list-item-2"])
    }

    func testFilterReloadsItemsWithMediaType() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (Self.listsData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-1/items"] = (Self.itemsData, 200)
        let viewModel = try ListsViewModel(client: makeClient())

        await viewModel.load()
        await viewModel.selectFilter(.watch)

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/lists/list-1/items")
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.last.flatMap { URLComponents(url: $0, resolvingAgainstBaseURL: false) }?
                .queryItems?
                .first { $0.name == "media_type" }?
                .value,
            "video"
        )
    }

    func testListFilterMediaTypes() {
        XCTAssertNil(ListsItemFilter.all.mediaType)
        XCTAssertEqual(ListsItemFilter.reading.mediaType, "article")
        XCTAssertEqual(ListsItemFilter.watch.mediaType, "video")
        XCTAssertEqual(ListsItemFilter.listen.mediaType, "audio")
        XCTAssertEqual(ListsItemFilter.all.id, "all")
    }

    func testLoadedListsViewRendersControlsRowsAndItems() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (Self.listsData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-1/items"] = (Self.itemsData, 200)
        let viewModel = try ListsViewModel(client: makeClient())
        await viewModel.load()

        let sut = ListsView(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(button: "Create"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Edit"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Import"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Delete"))
        XCTAssertEqual(try sut.inspect().find(text: "Reading Queue").string(), "Reading Queue")
        XCTAssertEqual(try sut.inspect().find(text: "item-1").string(), "item-1")
        XCTAssertEqual(try sut.inspect().find(text: "News item · Article").string(), "News item · Article")
    }

    func testListsViewRendersEmptyAndRouteStates() throws {
        let empty = try ListsView(viewModel: ListsViewModel(client: makeClient()))
        XCTAssertEqual(try empty.inspect().find(text: "No Lists").string(), "No Lists")

        let signedInRoute = try ListsRouteSurface(client: makeClient(), isSignedIn: true) {}
        XCTAssertEqual(try signedInRoute.inspect().find(text: "No Lists").string(), "No Lists")

        let signedOutRoute = ListsRouteSurface(client: nil, isSignedIn: false) {}
        XCTAssertEqual(try signedOutRoute.inspect().find(text: "Sign in required").string(), "Sign in required")
    }

    func testListsViewRendersEmptyItemsForSelectedList() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (Self.createdListData, 200)
        let viewModel = try ListsViewModel(client: makeClient())
        await viewModel.createList(name: "Reading")

        let sut = ListsView(viewModel: viewModel)

        XCTAssertEqual(try sut.inspect().find(text: "No List Items").string(), "No List Items")
    }

    func testSheetsRenderForms() throws {
        let create = ListsCreateSheet { _ in }
        XCTAssertNoThrow(try create.inspect().find(ViewType.TextField.self))

        let edit = try ListsEditSheet(list: Self.userList(id: "list-1", name: "Reading", description: "Saved")) {
            _,
            _ in
        }
        XCTAssertNoThrow(try edit.inspect().find(ViewType.TextField.self))
        XCTAssertNoThrow(try edit.inspect().find(ViewType.TextEditor.self))

        let importSheet = ListsImportSheet { _ in }
        XCTAssertNoThrow(try importSheet.inspect().find(ViewType.TextField.self))
    }

    func testEditSheetSavesInitialFields() async throws {
        let saved = expectation(description: "edit saved")
        var capturedName: String?
        var capturedDescription: String?
        let sut = try ListsEditSheet(list: Self.userList(id: "list-1", name: "Reading", description: "Saved")) {
            name,
            description in
            capturedName = name
            capturedDescription = description
            saved.fulfill()
        }

        try sut.inspect().find(button: "Save").tap()

        await fulfillment(of: [saved], timeout: 1)
        XCTAssertEqual(capturedName, "Reading")
        XCTAssertEqual(capturedDescription, "Saved")
    }

    func testFormShellRunsEnabledSave() async throws {
        let saved = expectation(description: "form saved")
        let sut = ListsFormShell(
            title: .nativeSwiftListsEditList,
            saveTitle: .nativeSwiftCommonSave,
            canSave: true
        ) {
            Text("Body")
        } save: {
            saved.fulfill()
        }

        try sut.inspect().find(button: "Save").tap()

        await fulfillment(of: [saved], timeout: 1)
    }

    func testCreateUpdateDeleteAndImportUseListEndpoints() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (Self.createdListData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-created"] = (Self.updatedListData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-created/items"] = (Self.itemsData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-created/import"] = (
            Data(#"{"posts":1,"items":1}"#.utf8),
            200
        )
        let viewModel = try ListsViewModel(client: makeClient())

        await viewModel.createList(name: " Reading ")
        await viewModel.updateSelectedList(name: " Renamed ", description: " Updated ")
        await viewModel.importCommunity(slug: " community ")
        await viewModel.deleteSelectedList()

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST", "PATCH", "POST", "GET", "DELETE"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/lists",
            "/api/v1/lists/list-created",
            "/api/v1/lists/list-created/import",
            "/api/v1/lists/list-created/items",
            "/api/v1/lists/list-created"
        ])
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies[0]?.contains(#""name":"Reading""#) == true)
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies[1]?.contains(#""name":"Renamed""#) == true)
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies[1]?.contains(#""description":"Updated""#) == true)
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies[2]?.contains(#""community_slug":"community""#) == true)
    }

    func testReloadFailurePreservesLoadedListsAndItems() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (Self.listsData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-1/items"] = (Self.itemsData, 200)
        let viewModel = try ListsViewModel(client: makeClient())

        await viewModel.load()

        let loadedLists = viewModel.lists
        let loadedItems = viewModel.items
        let loadedSelectedList = viewModel.selectedList

        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (Data("{}".utf8), 200)
        await viewModel.reload()

        if case .error = viewModel.state {
            XCTAssertEqual(viewModel.lists.map(\.id), loadedLists.map(\.id))
            XCTAssertEqual(viewModel.items.map(\.id), loadedItems.map(\.id))
            XCTAssertEqual(viewModel.selectedList?.id, loadedSelectedList?.id)
            switch viewModel.itemState {
            case .loaded:
                break
            default:
                XCTFail("Expected items to remain loaded after reload failure")
            }
        } else {
            XCTFail("Expected reload error")
        }
    }

    func testReloadItemFailurePreservesLoadedItems() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (Self.listsData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-1/items"] = (Self.itemsData, 200)
        let viewModel = try ListsViewModel(client: makeClient())

        await viewModel.load()

        let loadedItems = viewModel.items
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-1/items"] = (Data("{}".utf8), 500)
        await viewModel.reload()

        XCTAssertEqual(viewModel.items.map(\.id), loadedItems.map(\.id))
        if case .error = viewModel.itemState {
            XCTAssertEqual(viewModel.selectedList?.id, "list-1")
        } else {
            XCTFail("Expected item reload error")
        }
    }

    func testUpdateClearsBlankDescription() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (Self.createdListData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-created"] = (Self.clearedListData, 200)
        let viewModel = try ListsViewModel(client: makeClient())

        await viewModel.createList(name: "Reading")
        await viewModel.updateSelectedList(name: "Reading", description: "   ")

        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies[1]?.contains(#""description":null"#) == true)
        XCTAssertNil(viewModel.selectedList?.description)
    }

    func testCreateClearsStaleItemErrorForNewList() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (Self.listsData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-1/items"] = (Data("{}".utf8), 500)
        let viewModel = try ListsViewModel(client: makeClient())

        await viewModel.load()

        if case .error = viewModel.itemState {
            CannedFeedURLProtocol.handlers["/api/v1/lists"] = (Self.createdListData, 200)
            await viewModel.createList(name: "Reading")
        } else {
            XCTFail("Expected initial item error")
        }

        if case .loaded = viewModel.itemState {
            XCTAssertEqual(viewModel.selectedList?.id, "list-created")
            XCTAssertTrue(viewModel.items.isEmpty)
        } else {
            XCTFail("Expected new list to clear stale item error")
        }
    }

    func testBlankMutationsAreNoOps() async throws {
        let viewModel = try ListsViewModel(client: makeClient())

        await viewModel.createList(name: "   ")
        await viewModel.updateSelectedList(name: "   ", description: "Updated")
        await viewModel.importCommunity(slug: "   ")
        await viewModel.deleteSelectedList()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testSelectListLoadsItemsForProvidedList() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-selected/items"] = (Self.itemsData, 200)
        let viewModel = try ListsViewModel(client: makeClient())

        try await viewModel.selectList(Self.userList(id: "list-selected", name: "Selected", description: nil))

        XCTAssertEqual(viewModel.selectedList?.id, "list-selected")
        XCTAssertEqual(viewModel.items.map(\.id), ["list-item-1", "list-item-2"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), ["/api/v1/lists/list-selected/items"])
    }

    func testStaleItemResponsesDoNotReplaceCurrentSelectionItems() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/lists/list-1/items"] = [(Self.itemsData, 200, 0.15)]
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-2/items"] = (Self.list2ItemsData, 200)
        let viewModel = try ListsViewModel(client: makeClient())
        let list1 = try Self.userList(id: "list-1", name: "First", description: nil)
        let list2 = try Self.userList(id: "list-2", name: "Second", description: nil)

        let staleLoad = Task { await viewModel.selectList(list1) }
        try await Task.sleep(nanoseconds: 25_000_000)
        await viewModel.selectList(list2)
        await staleLoad.value

        XCTAssertEqual(viewModel.selectedList?.id, "list-2")
        XCTAssertEqual(viewModel.items.map(\.id), ["list-item-3"])
    }

    func testSlowUpdateDoesNotStealNewerSelection() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/lists/list-1"] = [(
            Self.listResponse(id: "list-1", name: "Renamed", description: "Updated"),
            200,
            0.15
        )]
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-2/items"] = (Self.list2ItemsData, 200)
        let viewModel = try ListsViewModel(client: makeClient())
        let list1 = try Self.userList(id: "list-1", name: "First", description: nil)
        let list2 = try Self.userList(id: "list-2", name: "Second", description: nil)
        viewModel.lists = [list1, list2]
        viewModel.selectedList = list1

        let update = Task {
            await viewModel.updateSelectedList(name: "Renamed", description: "Updated")
        }
        try await Task.sleep(nanoseconds: 25_000_000)
        await viewModel.selectList(list2)
        await update.value

        XCTAssertEqual(viewModel.selectedList?.id, "list-2")
        XCTAssertEqual(viewModel.lists.first { $0.id == "list-1" }?.name, "Renamed")
    }

    func testSlowDeleteDoesNotOverwriteNewerSelection() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/lists/list-1"] = [(Data("{}".utf8), 200, 0.15)]
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-2/items"] = (Self.list2ItemsData, 200)
        let viewModel = try ListsViewModel(client: makeClient())
        let list1 = try Self.userList(id: "list-1", name: "First", description: nil)
        let list2 = try Self.userList(id: "list-2", name: "Second", description: nil)
        viewModel.lists = [list1, list2]
        viewModel.selectedList = list1

        let delete = Task { await viewModel.deleteSelectedList() }
        try await Task.sleep(nanoseconds: 25_000_000)
        await viewModel.selectList(list2)
        await delete.value

        XCTAssertEqual(viewModel.selectedList?.id, "list-2")
        XCTAssertEqual(viewModel.lists.map(\.id), ["list-2"])
        XCTAssertEqual(viewModel.items.map(\.id), ["list-item-3"])
    }

    func testLoadAndItemErrorsSetErrorStates() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (Data("{}".utf8), 500)
        let loadErrorViewModel = try ListsViewModel(client: makeClient())

        await loadErrorViewModel.load()

        if case .error = loadErrorViewModel.state {
            XCTAssertTrue(loadErrorViewModel.lists.isEmpty)
        } else {
            XCTFail("Expected list load error")
        }

        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (Self.listsData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-1/items"] = (Data("{}".utf8), 500)
        let itemErrorViewModel = try ListsViewModel(client: makeClient())

        await itemErrorViewModel.load()

        if case .error = itemErrorViewModel.itemState {
            XCTAssertTrue(itemErrorViewModel.items.isEmpty)
        } else {
            XCTFail("Expected item load error")
        }
        XCTAssertFalse(itemErrorViewModel.isLoading)
    }

    func testDecodingFailuresSetFallbackErrorStates() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (Data("{}".utf8), 200)
        let loadErrorViewModel = try ListsViewModel(client: makeClient())

        await loadErrorViewModel.load()

        if case .error = loadErrorViewModel.state {
            XCTAssertTrue(loadErrorViewModel.lists.isEmpty)
        } else {
            XCTFail("Expected fallback list load error")
        }

        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (Self.listsData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-1/items"] = (Data("{}".utf8), 200)
        let itemErrorViewModel = try ListsViewModel(client: makeClient())

        await itemErrorViewModel.load()

        if case .error = itemErrorViewModel.itemState {
            XCTAssertTrue(itemErrorViewModel.items.isEmpty)
        } else {
            XCTFail("Expected fallback item load error")
        }

        CannedFeedURLProtocol.handlers["/api/v1/lists/list-1"] = (Data("{}".utf8), 200)
        let mutationErrorViewModel = try ListsViewModel(client: makeClient())

        await mutationErrorViewModel.load()
        await mutationErrorViewModel.updateSelectedList(name: "Renamed", description: nil)

        if case .error = mutationErrorViewModel.state {
            XCTAssertEqual(mutationErrorViewModel.selectedList?.id, "list-1")
        } else {
            XCTFail("Expected fallback mutation error")
        }
    }

    func testMutationErrorPreservesSelection() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (Self.listsData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-1/items"] = (Self.itemsData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/lists/list-1"] = (Data("{}".utf8), 500)
        let failingViewModel = try ListsViewModel(client: makeClient())

        await failingViewModel.load()
        await failingViewModel.updateSelectedList(name: "Renamed", description: "Updated")

        if case .error = failingViewModel.state {
            XCTAssertEqual(failingViewModel.selectedList?.id, "list-1")
        } else {
            XCTFail("Expected mutation error")
        }
    }

    func testDestinationViewRendersDedicatedListsSurfaceWithoutClientRequests() throws {
        let sut = try NativeRouteDestinationView(entry: entry(for: .lists), client: nil)

        XCTAssertEqual(try sut.inspect().find(text: "Sign in required").string(), "Sign in required")
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

}
