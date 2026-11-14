package voucha.android

import com.google.mlkit.genai.common.DownloadStatus
import com.google.mlkit.genai.common.FeatureStatus
import com.google.mlkit.genai.prompt.Generation
import com.google.mlkit.genai.prompt.GenerateContentRequest
import com.google.mlkit.genai.prompt.TextPart
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Job
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.runBlocking
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.atomic.AtomicBoolean

/** Small blocking boundary for native Swift. Swift invokes every method off the main thread. */
object AICoreBridge {
    private val model by lazy { Generation.getClient() }
    private val generationRequests = ConcurrentHashMap<String, GenerationRequest>()

    private class GenerationRequest {
        val cancelled = AtomicBoolean(false)
        val started = AtomicBoolean(false)
        @Volatile var job: Job? = null

        fun cancel() {
            cancelled.set(true)
            job?.cancel(CancellationException("Generation no longer needed"))
        }
    }

    @JvmStatic
    fun prepareGeneration(requestID: String) {
        require(requestID.isNotBlank())
        check(generationRequests.putIfAbsent(requestID, GenerationRequest()) == null) {
            "Generation request is already registered"
        }
    }

    @JvmStatic
    fun cancelGeneration(requestID: String) {
        generationRequests[requestID]?.cancel()
    }

    @JvmStatic
    fun finishGeneration(requestID: String) {
        generationRequests.remove(requestID)
    }

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
    fun generate(prompt: String, requestID: String): String = runBlocking {
        val request = checkNotNull(generationRequests[requestID]) {
            "Generation request is not registered"
        }
        check(request.started.compareAndSet(false, true)) {
            "Generation request was already started"
        }
        request.job = currentCoroutineContext()[Job]
        try {
            currentCoroutineContext().ensureActive()
            if (request.cancelled.get()) {
                throw CancellationException("Generation no longer needed")
            }
            val input = GenerateContentRequest.Builder(TextPart(prompt)).build()
            val response = model.generateContent(input)
            currentCoroutineContext().ensureActive()
            response.candidates.firstOrNull()?.text.orEmpty().trim()
        } finally {
            request.job = null
            generationRequests.remove(requestID, request)
        }
    }
}
