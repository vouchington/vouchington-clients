@testable import VouchaAPI

let copyrightMediaFixtureEndpoints: [String: Endpoint] = [
    "native.posts.images.placement-limit.conflict": .setPostImages(
        postId: "00000000-0000-7000-8000-000000000801",
        images: [.init(imageId: "00000000-0000-7000-8000-000000000802", orderIndex: 0, caption: "")]
    ),
    "native.posts.images.placement.default": .postImages(
        idOrSlug: "00000000-0000-7000-8000-000000000801"
    ),
    "native.moderation.copyright.image-similarity-candidates.default": .copyrightImageSimilarityCandidates(
        noticeId: "00000000-0000-7000-8000-000000000804",
        targetId: "00000000-0000-7000-8000-000000000805"
    )
]
