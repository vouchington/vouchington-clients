import SwiftUI
import VouchaLocalization
import VouchaModels

/// A policy-bound ballot. Counts disclose participation, never weighted score or strength.
public struct VoteControls: View {
    @Environment(\.locale)
    private var nativeUiLocale
    public let election: (any ElectionCountProviding)?
    public let myVote: ElectionVoteChoice?
    public let policy: ElectionVotePolicy
    /// Whether the viewer may create or replace a ballot. Official Clear-only uses `onVote(nil)`.
    public let canCreateVote: Bool
    public let onVote: ((ElectionVoteChoice?) -> Void)?
    public let onSignedOutTap: (() -> Void)?
    public let hideDownCount: Bool

    public init(
        election: (any ElectionCountProviding)?,
        myVote: ElectionVoteChoice? = nil,
        policy: ElectionVotePolicy = .sentiment,
        canCreateVote: Bool = true,
        onVote: ((ElectionVoteChoice?) -> Void)? = nil,
        onSignedOutTap: (() -> Void)? = nil,
        hideDownCount: Bool = false
    ) {
        self.election = election
        self.myVote = myVote
        self.policy = policy
        self.canCreateVote = canCreateVote
        self.onVote = onVote
        self.onSignedOutTap = onSignedOutTap
        self.hideDownCount = hideDownCount
    }

    public var body: some View {
        Menu {
            if let onSignedOutTap {
                Button(signInLabel, action: onSignedOutTap)
            }
            if canCreateVote {
                ForEach(visibleChoices, id: \.self) { choice in
                    Button(choiceLabel(choice)) { cast(choice) }
                }
            }
            if !canCreateVote, myVote != nil, onVote != nil {
                Button(clearLabel, role: .destructive) { cast(nil) }
            }
        } label: {
            Label(myVote.map(choiceLabel) ?? votePrompt, systemImage: "hand.thumbsup")
                .frame(minWidth: 44, minHeight: 44)
        }
        .accessibilityLabel(UiMessages.string(.extractedVotesSemanticVoteVote, locale: nativeUiLocale))
        .disabled(onSignedOutTap == nil && !canCreateVote && (myVote == nil || onVote == nil))
        if let election {
            HStack(spacing: Spacing.xs) {
                Text(UiMessages.number(election.votesCountUp, locale: nativeUiLocale))
                    .foregroundStyle(Colors.positiveVote)
                if !hideDownCount {
                    Text(UiMessages.number(election.votesCountDown, locale: nativeUiLocale))
                        .foregroundStyle(Colors.negativeVote)
                }
            }
            .font(Typography.caption.monospacedDigit())
        }
    }

    private func cast(_ choice: ElectionVoteChoice?) {
        if let onVote {
            onVote(choice)
        } else {
            onSignedOutTap?()
        }
    }

    private var visibleChoices: [ElectionVoteChoice] {
        if policy == .sentiment, myVote == nil {
            return policy.choices.filter { $0 != .neutral }
        }
        return policy.choices
    }

    private var votePrompt: String {
        UiMessages.string(.extractedVotesSemanticVoteVote, locale: nativeUiLocale)
    }

    private var clearLabel: String {
        UiMessages.string(.extractedVotesSemanticVoteClear, locale: nativeUiLocale)
    }

    private var signInLabel: String {
        UiMessages.string(.nativeAuthSignIn, locale: nativeUiLocale)
    }

    private func choiceLabel(_ choice: ElectionVoteChoice) -> String {
        UiMessages.string(choice.messageKey, locale: nativeUiLocale)
    }
}

public protocol ElectionCountProviding {
    var votesCountUp: Int { get }
    var votesCountDown: Int { get }
}

extension PostElection: ElectionCountProviding {}
extension RssFeedItemElection: ElectionCountProviding {}
extension TopicElection: ElectionCountProviding {}

private extension ElectionVoteChoice {
    var messageKey: UiMessageKey {
        switch self {
        case .vouch: .extractedVotesSemanticVoteVouch
        case .like: .extractedVotesSemanticVoteLike
        case .neutral: .extractedVotesSemanticVoteNeutral
        case .dislike: .extractedVotesSemanticVoteDislike
        case .disavow: .extractedVotesSemanticVoteDisavow
        case .support: .extractedVotesSemanticVoteSupport
        case .oppose: .extractedVotesSemanticVoteOppose
        case .confirm: .extractedVotesSemanticVoteConfirm
        case .dispute: .extractedVotesSemanticVoteDispute
        case .accurate: .extractedVotesSemanticVoteAccurate
        case .inaccurate: .extractedVotesSemanticVoteInaccurate
        }
    }
}
