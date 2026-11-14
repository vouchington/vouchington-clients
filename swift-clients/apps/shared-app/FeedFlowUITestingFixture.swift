#if DEBUG
    import SwiftUI

    struct FeedFlowUITestingFixture: View {
        let viewModelFactory: ViewModelFactory
        @State
        private var restoredCanonicalIdentity = false

        var body: some View {
            Group {
                if restoredCanonicalIdentity {
                    RootView(viewModelFactory: viewModelFactory)
                } else {
                    ProgressView()
                }
            }
            .task {
                guard !restoredCanonicalIdentity else { return }
                restoredCanonicalIdentity = await viewModelFactory.sessionManager.refresh()
            }
        }
    }
#endif
