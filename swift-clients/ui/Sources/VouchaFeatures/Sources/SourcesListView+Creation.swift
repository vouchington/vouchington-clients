import SwiftUI

extension SourcesListView {
    var creationSheet: SourceCreationView {
        SourceCreationView(model: creationModel, navigate: { path in
            showCreation = false
            navigateToSource(path)
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
