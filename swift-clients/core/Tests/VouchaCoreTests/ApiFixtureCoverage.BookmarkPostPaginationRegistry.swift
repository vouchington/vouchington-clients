import VouchaModels

let bookmarkPostPaginationFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.bookmarks.posts.saved.next-page") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<Post>.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    }
]
