import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

struct NativeChatDraftSendContext {
    let conversationId: String
    let text: String
    let userMessageId: String
    let createdConversation: Bool
    let providerSelection: NativeChatTitleProviderKind

    init(
        conversationId: String,
        text: String,
        userMessageId: String,
        createdConversation: Bool,
        providerSelection: NativeChatTitleProviderKind = .openAI
    ) {
        self.conversationId = conversationId
        self.text = text
        self.userMessageId = userMessageId
        self.createdConversation = createdConversation
        self.providerSelection = providerSelection
    }
}

extension NativeChatViewModel {
    func streamHostedDraftMessage(
        client: APIClient,
        context: NativeChatDraftSendContext
    ) async {
        let stream = await client.streamChatConversation(
            conversationId: context.conversationId,
            message: context.text,
            provider: context.providerSelection.hostedProviderValue
        )
        await streamHostedDraftMessage(events: stream, context: context)
    }

    func streamHostedDraftMessage<S: AsyncSequence>(
        events: S,
        context: NativeChatDraftSendContext
    ) async where S.Element == ChatStreamEvent {
        let conversationId = context.conversationId
        let userMessageId = context.userMessageId

        do {
            for try await event in events {
                await applyStreamEvent(
                    event,
                    conversationId: conversationId,
                    userMessageId: userMessageId
                )
                if case .error = event {
                    return
                }
            }
            await finishStream(
                conversationId: conversationId,
                userMessageId: userMessageId,
                createdConversation: context.createdConversation
            )
        } catch ChatSSEReaderError.incompleteStream {
            await failStream(
                conversationId: conversationId,
                userMessageId: userMessageId,
                message: .message(.nativeSwiftChatResponseInterrupted),
                createdConversation: context.createdConversation
            )
        } catch let error as VouchaError {
            await failStream(
                conversationId: conversationId,
                userMessageId: userMessageId,
                message: error.errorDescription.map(UiVerbatimText.verbatim)
                    ?? .message(.nativeSwiftChatUnableToSendMessage),
                createdConversation: context.createdConversation
            )
        } catch {
            await failStream(
                conversationId: conversationId,
                userMessageId: userMessageId,
                message: .message(.nativeSwiftChatUnableToSendMessage),
                createdConversation: context.createdConversation
            )
        }
    }
}
