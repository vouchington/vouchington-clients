import Foundation
import VouchaCore
import VouchaLocalization
import VouchaModels

extension NativeCommentThreadSurface {
    var loadTaskId: String {
        [
            routeMatch?.path ?? "",
            routeMatch?.param("commentId") ?? "",
            currentUserId ?? ""
        ].joined(separator: "|")
    }

    var loadErrorMessage: String? {
        switch viewModel.threadState {
        case let .error(error): error.errorDescription
        default: nil
        }
    }

    func loadThread() async {
        if let commentId = routeMatch?.param("commentId") {
            await viewModel.loadPermalink(targetCommentId: commentId)
        } else {
            await viewModel.loadThread()
        }
    }

    func handleVote(postId: String, choice: ElectionVoteChoice?) {
        guard isSignedIn else {
            showSignIn()
            return
        }
        Swift.Task { await viewModel.vote(postId: postId, choice: choice) }
    }

    func handleSave(postId: String) {
        guard isSignedIn else {
            showSignIn()
            return
        }
        Swift.Task { await viewModel.toggleSave(postId: postId) }
    }

    func handleLock(post: Post) {
        guard isSignedIn else {
            showSignIn()
            return
        }
        Swift.Task { await viewModel.toggleLock(postId: post.id, lockedAt: post.lockedAt) }
    }

    func startReply(post: Post, quote: Bool) {
        guard isSignedIn else {
            showSignIn()
            return
        }
        composer = .init(kind: .reply(parentId: post.id), markdown: quote ? quoteMarkdown(post) : "")
    }

    func startReply(postId: String, quote: Bool) {
        guard isSignedIn else {
            showSignIn()
            return
        }
        composer = .init(kind: .reply(parentId: postId), markdown: quote ? "> \n\n" : "")
    }

    func startQuote(post: Post) {
        startReply(post: post, quote: true)
    }

    func startEdit(post: Post) {
        guard isSignedIn else {
            showSignIn()
            return
        }
        composer = .init(kind: .edit(postId: post.id), markdown: post.markdown ?? "")
    }

    func startReport(post: Post) {
        guard isSignedIn else {
            showSignIn()
            return
        }
        composer = .init(kind: .report(postId: post.id), markdown: "")
    }

    func startDelete(post: Post) {
        guard isSignedIn else {
            showSignIn()
            return
        }
        composer = .init(kind: .delete(postId: post.id), markdown: "")
    }

    func submitComposer(_ composer: NativeCommentThreadComposerState) async {
        switch composer.kind {
        case let .reply(parentId):
            await viewModel.reply(
                parentId: parentId,
                markdown: composer.markdown,
                turnstileToken: composer.turnstileToken
            )
        case let .edit(postId):
            await viewModel.edit(postId: postId, markdown: composer.markdown)
        case let .report(postId):
            await viewModel.report(
                postId: postId,
                reason: composer.reason,
                note: noteOrNil(composer.note),
                turnstileToken: composer.turnstileToken
            )
        case let .delete(postId):
            await viewModel.delete(postId: postId)
        }
        if !viewModel.emailVerificationGate.isRecoveryPresented, case .error = viewModel.mutationState {
            return
        }
        if !viewModel.emailVerificationGate.isRecoveryPresented {
            self.composer = nil
        }
    }

    func canReport(post: Post) -> Bool {
        isSignedIn && currentUserId != nil && post.createdById != currentUserId && post.deletedAt == nil
    }

    func canDistribute(_ post: Post) -> Bool {
        canDistributeToFollowers(post, currentUserId: currentUserId, isSignedIn: isSignedIn)
    }

    func authorName(for post: Post) -> UiVerbatimText {
        (post.createdBy?.username).map(UiVerbatimText.verbatim)
            ?? post.createdById.map(UiVerbatimText.verbatim)
            ?? .message(.nativeSwiftPresentationValuesDeleted)
    }

    func quoteMarkdown(_ post: Post) -> String {
        let markdown = post.markdown ?? post.title ?? ""
        let quoted = markdown.split(separator: "\n").map { "> \($0)" }.joined(separator: "\n")
        return quoted.isEmpty ? "" : "\(quoted)\n\n"
    }

    func noteOrNil(_ note: String) -> String? {
        let trimmed = note.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }

    func threadRootType(_ post: Post) -> String {
        post.postType.routeSegment
    }
}
