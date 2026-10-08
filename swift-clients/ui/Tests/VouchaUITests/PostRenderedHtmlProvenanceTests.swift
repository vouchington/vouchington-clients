import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class PostRenderedHtmlProvenanceTests: XCTestCase {
    func testRenderingAndHydrationRetainTrustedProvenance() {
        let provenance = PublicContentProvenance(
            via: "api",
            app: PublicProvenanceApp(kind: "hostname", hostname: "example.test")
        )
        let post = makePost(provenance: provenance)

        let rendered = post.withRenderedHtml("<p>Example</p>")
        XCTAssertEqual(rendered.html, "<p>Example</p>")
        XCTAssertEqual(rendered.provenance, provenance)

        let hydrated = post.hydrated(
            renderedHtml: "<p>Hydrated</p>",
            metrics: nil,
            election: nil,
            voteChoice: nil
        )
        XCTAssertEqual(hydrated.html, "<p>Hydrated</p>")
        XCTAssertEqual(hydrated.provenance, provenance)
    }

    func testCommentMutationReconstructionRetainsProvenance() {
        let provenance = PublicContentProvenance(
            via: "mcp",
            app: PublicProvenanceApp(kind: "verified", clientName: "Example client")
        )
        let viewModel = NativeCommentThreadViewModel(client: nil, rootPostId: "post-1")
        let existing = makePost(provenance: provenance)
        let mutationWithoutProvenance = makePost(provenance: nil)

        let merged = viewModel.mergedMutationPost(mutationWithoutProvenance, preserving: existing)
        XCTAssertEqual(merged.provenance, provenance)

        let anonymousApp = PublicContentProvenance(via: "api", app: nil)
        let updated = viewModel.mergedMutationPost(makePost(provenance: anonymousApp), preserving: existing)
        XCTAssertEqual(updated.provenance, anonymousApp)

        let newReply = viewModel.newReplyMutationPost(existing)
        XCTAssertEqual(newReply.provenance, provenance)
    }

    private func makePost(provenance: PublicContentProvenance?) -> Post {
        Post(
            id: "post-1",
            slug: nil,
            postType: .discussion,
            title: "Example",
            markdown: "Example",
            html: nil,
            parentId: nil,
            rootId: nil,
            createdById: "user-1",
            createdAt: Date(timeIntervalSince1970: 0),
            provenance: provenance,
            broadcast: nil,
            privacy: .public,
            isAnonymous: false,
            communityId: nil,
            clearanceStatus: nil
        )
    }
}
