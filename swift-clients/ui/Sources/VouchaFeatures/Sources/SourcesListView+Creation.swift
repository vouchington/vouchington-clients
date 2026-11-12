import SwiftUI

extension SourcesListView {
    var creationSheet: SourceCreationView {
        creationSheet(isPresented: $showCreation)
    }

    func creationSheet(isPresented: Binding<Bool>) -> SourceCreationView {
        SourceCreationView(model: creationModel, navigate: { path in
            isPresented.wrappedValue = false
            navigateToSource(path)
        }, close: {
            isPresented.wrappedValue = false
        }, didDismiss: {
            let completed = creationModel
            let identity = creationIdentity()
            guard completed.consumeListCompletion() else { return }
            Task {
                guard creationModel === completed, creationIdentity() == identity else { return }
                await viewModel.reloadAfterCreation {
                    creationModel === completed && creationIdentity() == identity
                }
            }
        })
    }
}
