#pragma once

#if defined(__APPLE__)
#define ML_LLAMA_EXPORT __attribute__((visibility("default")))
#else
#define ML_LLAMA_EXPORT
#endif

#ifdef __cplusplus
extern "C" {
#endif

ML_LLAMA_EXPORT int ml_llama_get_api_version(void);

ML_LLAMA_EXPORT int ml_llama_generate(
    const char * model_path,
    const char * prompt,
    int context_size,
    int max_output_tokens,
    int thread_count,
    float temperature,
    float top_p,
    unsigned char * output,
    int output_capacity);

ML_LLAMA_EXPORT int ml_llama_get_last_error(
    unsigned char * output,
    int output_capacity);

#ifdef __cplusplus
}
#endif
