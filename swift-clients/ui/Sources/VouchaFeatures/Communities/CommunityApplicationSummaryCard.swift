import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct CommunityApplicationSummaryCard: View {
    let viewModel: CommunityDetailViewModel
    let application: CommunityApplicationSummary
    @Environment(\.locale)
    private var locale
    @State
    private var showsDetail = false

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Button { showsDetail = true } label: {
                VStack(alignment: .leading) {
                    Text(UiMessages.string(viewModel.applicationApplicantLabel(application), locale: locale))
                    Text(UiMessages.abbreviatedDateTime(application.createdAt, locale: locale))
                    Text(UiMessages.string(statusLabel, locale: locale))
                    if let preview = application.messagePreview { Text(verbatim: preview) }
                    Label(UiMessages.number(application.answerCount, locale: locale), systemImage: "text.bubble")
                }
            }
            .buttonStyle(.plain)
            if application.status == "pending" {
                CommunityApplicationReviewControls(
                    viewModel: viewModel,
                    isSignedIn: true,
                    showSignIn: {},
                    applicationId: application.id
                )
            }
        }
        .padding(Spacing.md)
        .sheet(isPresented: $showsDetail) {
            CommunityApplicationDetail(viewModel: viewModel, id: application.id)
        }
    }

    private var statusLabel: UiVerbatimText {
        switch application.status {
        case "pending": .message(.nativeSwiftCommunitiesAwaitingReview)
        case "approved": .message(.nativeSwiftModerationAppealsApproved)
        case "rejected": .message(.nativeSwiftCommunityStatusRejectedApplication)
        default: .protocolValue(application.status)
        }
    }

}

struct CommunityApplicationDetail: View {
    @Environment(\.locale)
    private var locale
    @Environment(\.dismiss)
    private var dismiss
    let viewModel: CommunityDetailViewModel
    let id: String
    var body: some View {
        ScrollView {
            if viewModel.isLoadingApplicationDetail {
                ProgressView().accessibilityIdentifier("application-detail-loading")
            } else if viewModel.applicationDetailFailed {
                VStack {
                    Text(UiMessages.string(.nativeSwiftCommunityStatusActionFailed, locale: locale))
                        .foregroundStyle(.red)
                        .accessibilityIdentifier("application-detail-error")
                    Button(UiMessages.string(.nativeCommonRetry, locale: locale)) {
                        Task { await viewModel.loadApplicationDetail(id: id) }
                    }
                }
            } else if let application = viewModel.applicationDetail, application.id == id {
                VStack(alignment: .leading) {
                    LabeledContent(
                        UiMessages.string(.nativeSwiftCommunitiesApplicationId, locale: locale),
                        value: application.id
                    )
                    if let message = application.message { Text(verbatim: message) }
                    ForEach(Array(application.answers.keys.sorted().enumerated()), id: \.element) { entry in
                        let questionId = entry.element
                        let label = viewModel.applicationQuestions.first { $0.id == questionId }?.question
                            ?? UiMessages.number(entry.offset + 1, locale: locale)
                        ForEach(
                            integrityDetails([label: application.answers[questionId] ?? .null], locale: locale),
                            id: \.self
                        ) {
                            Text(verbatim: $0)
                        }
                    }
                }
                .accessibilityIdentifier("application-detail-content")
            }
        }
        .padding()
        .safeAreaInset(edge: .top) {
            HStack {
                Spacer()
                Button(UiMessages.string(.nativeCommonDone, locale: locale)) { dismiss() }
            }
            .padding()
        }
        .task { await viewModel.loadApplicationDetail(id: id) }
    }

}
