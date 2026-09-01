// This gradle project is part of a conventional Skip app project.
pluginManagement {
    // Initialize the Skip plugin folder and perform a pre-build for non-Xcode builds
    val pluginPath = File.createTempFile("skip-plugin-path", ".tmp")

    // overriding outputs for an Android IDE can be done by un-commenting and setting the Xcode path:
    //System.setProperty("BUILT_PRODUCTS_DIR", "${System.getProperty("user.home")}/Library/Developer/Xcode/DerivedData/MySkipProject-HASH/Build/Products/Debug-iphonesimulator")

    val skipPluginResult = providers.exec {
        commandLine(
            "skip",
            "plugin",
            "--prebuild",
            "--package-path",
            settings.rootDir.parent,
            "--plugin-ref",
            pluginPath.absolutePath,
        )
        environment("PATH", "${System.getenv("PATH")}:/opt/homebrew/bin")
    }
    val skipPluginOutput = skipPluginResult.standardOutput.asText.get()
    print(skipPluginOutput)
    val skipPluginError = skipPluginResult.standardError.asText.get()
    print(skipPluginError)

    includeBuild(pluginPath.readText()) {
        name = "skip-plugins"
    }
}

// Skip's settings plugin applies the generated settings as a root multi-project build and then
// includes that same generated project as a composite build. The app consumes only the composite
// build, so applying the generated `include` declarations creates a second, unconsumed project
// tree. Keep the generated dependency repositories and version catalogs, but omit those root
// project declarations before including the generated project once as a composite build.
val packageRoot = settings.rootDir.parentFile
val skipEnv = java.util.Properties().apply {
    packageRoot.resolve("Skip.env").reader(Charsets.UTF_8).use(::load)
    remove("//")
}
val swiftModuleName = requireNotNull(skipEnv.getProperty("PRODUCT_NAME")) {
    "PRODUCT_NAME is required in ${packageRoot.resolve("Skip.env")}"
}
val androidPackageName = requireNotNull(skipEnv.getProperty("ANDROID_PACKAGE_NAME")) {
    "ANDROID_PACKAGE_NAME is required in ${packageRoot.resolve("Skip.env")}"
}
val builtProductsDir = System.getenv("BUILT_PRODUCTS_DIR")
    ?: System.getProperty("BUILT_PRODUCTS_DIR")
val skipOutputs = if (builtProductsDir == null) {
    packageRoot.resolve(".build/plugins/outputs")
} else {
    val xcodeBuildRoot = file(builtProductsDir).resolve("../../../")
    val buildToolPluginOutputs = xcodeBuildRoot.resolve(
        "Build/Intermediates.noindex/BuildToolPluginIntermediates",
    )
    if (buildToolPluginOutputs.isDirectory) {
        buildToolPluginOutputs
    } else {
        xcodeBuildRoot.resolve("SourcePackages/plugins")
    }
}
val skipstoneProject = skipOutputs.listFiles()
    ?.asSequence()
    ?.map { output -> output.resolve(swiftModuleName) }
    ?.flatMap { module ->
        sequenceOf(module.resolve("skipstone"), module.resolve("destination/skipstone"))
    }
    ?.firstOrNull(File::isDirectory)
    ?: error("Could not locate transpiled module $swiftModuleName in $skipOutputs")

val generatedSettings = skipstoneProject.resolve("settings.gradle.kts")
val filteredSettings = packageRoot.resolve(".build/Android/voucha-settings.gradle.kts")
val generatedProjectDeclaration = Regex(
    """^\s*(?:rootProject\.name\s*=|include\(|project\(\"[^\"]+\"\)\.projectDir\s*=).*""",
)
filteredSettings.parentFile.mkdirs()
filteredSettings.writeText(
    generatedSettings.useLines { lines ->
        lines.filterNot { generatedProjectDeclaration.matches(it) }.joinToString("\n", postfix = "\n")
    },
)

apply(from = filteredSettings)
includeBuild(skipstoneProject)
include(":app")
rootProject.name = androidPackageName

val androidBuildOutput = packageRoot.resolve(".build/Android")
gradle.projectsLoaded {
    rootProject.allprojects {
        layout.buildDirectory.set(androidBuildOutput.resolve(project.name))
    }
    val rootProjectPaths = rootProject.childProjects.values.map { it.path }.sorted()
    require(rootProjectPaths == listOf(":app")) {
        "Expected :app to be the only root Gradle subproject; found $rootProjectPaths"
    }
}

buildCache {
    local {
        // The manual/CI Android validation wrapper selects a user-private cache shared across
        // workspaces. Android Studio keeps Gradle's default cache when this variable is absent.
        System.getenv("GRADLE_BUILD_CACHE_DIR")?.takeIf { it.isNotBlank() }?.let {
            val cachePath = File(it).toPath().normalize()
            require(cachePath.isAbsolute && cachePath.nameCount > 0) {
                "GRADLE_BUILD_CACHE_DIR must be an absolute non-root path: $it"
            }
            generateSequence(cachePath) { path -> path.parent }
                .takeWhile { path -> path != cachePath.root }
                .forEach { path ->
                    require(!java.nio.file.Files.isSymbolicLink(path)) {
                        "GRADLE_BUILD_CACHE_DIR must not traverse a symlink: $it"
                    }
                }
            directory = cachePath.toFile()
        }
    }
}
