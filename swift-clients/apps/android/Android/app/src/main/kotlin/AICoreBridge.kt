package voucha.android

import com.google.mlkit.genai.common.DownloadStatus
import com.google.mlkit.genai.common.FeatureStatus
import com.google.mlkit.genai.prompt.Generation
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.runBlocking

/** Small blocking boundary for native Swift. Swift invokes every method off the main thread. */
object AICoreBridge {
    private val model by lazy { Generation.getClient() }

    @JvmStatic
    fun status(): String = runBlocking {
        when (model.checkStatus()) {
            FeatureStatus.AVAILABLE -> "available"
            FeatureStatus.DOWNLOADABLE -> "downloadable"
            FeatureStatus.DOWNLOADING -> "downloading"
            FeatureStatus.UNAVAILABLE -> "unavailable"
            else -> "unavailable"
        }
    }

    @JvmStatic
    fun download(): String = runBlocking {
        var result = "downloading"
        model.download().catch { error ->
            result = "error:${error.message ?: error::class.simpleName}"
        }.collect { status ->
            result = when (status) {
                DownloadStatus.DownloadCompleted -> "available"
                is DownloadStatus.DownloadFailed -> "error:${status.e.message ?: status.e::class.simpleName}"
                else -> "downloading"
            }
        }
        result
    }

    @JvmStatic
    fun generate(prompt: String): String = runBlocking {
        model.generateContent(prompt).candidates.firstOrNull()?.text.orEmpty().trim()
    }
}
