import SwiftUI
import UniformTypeIdentifiers
import VouchaAPI
import VouchaLocalization
import VouchaModels

public struct ImportExportView: View {
    @Environment(\.locale)
    var nativeUiLocale
    static let sourceImportContentTypes: [UTType] = [.commaSeparatedText, .xml, .vouchaOPML]

    let route: ImportExportRoute
    @State
    var viewModel: ImportExportViewModel
    @State
    var showingFileImporter = false
    @State
    var presentedError: UiVerbatimText?

    public init(route: ImportExportRoute, client: APIClient) {
        self.route = route
        _viewModel = State(initialValue: ImportExportViewModel(route: route, service: .live(client: client)))
    }

    init(route: ImportExportRoute, viewModel: ImportExportViewModel) {
        self.route = route
        _viewModel = State(initialValue: viewModel)
    }

    public var body: some View {
        Form {
            importSection
            if !viewModel.results.isEmpty || viewModel.sourceSummary != nil {
                progressSection
            }
            exportSection
        }
        .formStyle(.grouped)
        .fileImporter(
            isPresented: $showingFileImporter,
            allowedContentTypes: Self.sourceImportContentTypes,
            allowsMultipleSelection: false,
            onCompletion: handleFileSelection
        )
        .alert(
            UiMessages.string(.nativeSwiftImportExportTitle, locale: nativeUiLocale),
            isPresented: errorIsPresented
        ) {
            Button(UiMessages.string(.nativeSwiftCommonOK, locale: nativeUiLocale), role: .cancel) {
                presentedError = nil
                viewModel.errorMessage = nil
            }
        } message: {
            Text(UiMessages.string(
                presentedError ?? viewModel.errorMessage.map(UiVerbatimText.verbatim) ?? .verbatim(""),
                locale: nativeUiLocale
            ))
        }
        .onDisappear {
            viewModel.cancelOperations()
        }
    }

    private var errorIsPresented: Binding<Bool> {
        Binding(get: { presentedError != nil || viewModel.errorMessage != nil }, set: {
            if !$0 {
                presentedError = nil
                viewModel.errorMessage = nil
            }
        })
    }

}
