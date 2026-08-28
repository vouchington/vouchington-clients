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

plugins {
    id("skip-plugin") apply true
}

buildCache {
    local {
        // pre-push.sh selects a user-private cache shared across workspaces. Android Studio keeps
        // Gradle's default GRADLE_USER_HOME cache when the environment variable is absent.
        System.getenv("GRADLE_BUILD_CACHE_DIR")?.takeIf { it.isNotBlank() }?.let {
            val cacheDirectory = File(it)
            require(cacheDirectory.isAbsolute && cacheDirectory.path != File.separator) {
                "GRADLE_BUILD_CACHE_DIR must be an absolute non-root path: $it"
            }
            require(!java.nio.file.Files.isSymbolicLink(cacheDirectory.toPath())) {
                "GRADLE_BUILD_CACHE_DIR must not be a symlink: $it"
            }
            directory = cacheDirectory
        }
    }
}
