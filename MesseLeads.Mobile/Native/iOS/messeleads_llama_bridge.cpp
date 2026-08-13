#include "messeleads_llama_bridge.h"

#include "ggml-backend.h"
#include "llama.h"

#include <algorithm>
#include <cstdint>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

namespace
{
    std::mutex g_mutex;
    std::string g_last_error;

    int fail(const std::string& message, int code)
    {
        g_last_error = message;
        return code;
    }

    void copy_text(
        const std::string& value,
        unsigned char* output,
        int capacity)
    {
        if (output == nullptr || capacity <= 0)
        {
            return;
        }

        const int available = capacity - 1;
        const int value_length = static_cast<int>(value.size());
        const int count = std::min(value_length, available);

        if (count > 0)
        {
            std::memcpy(output, value.data(), static_cast<std::size_t>(count));
        }

        output[count] = 0;
    }

    void free_runtime(
        llama_sampler* sampler,
        llama_context* context,
        llama_model* model)
    {
        if (sampler != nullptr)
        {
            llama_sampler_free(sampler);
        }

        if (context != nullptr)
        {
            llama_free(context);
        }

        if (model != nullptr)
        {
            llama_model_free(model);
        }
    }

    bool append_token_piece(
        const llama_vocab* vocab,
        llama_token token,
        std::string& generated)
    {
        char stack_buffer[512];

        int piece_length = llama_token_to_piece(
            vocab,
            token,
            stack_buffer,
            static_cast<int32_t>(sizeof(stack_buffer)),
            0,
            true);

        if (piece_length >= 0)
        {
            if (piece_length > 0)
            {
                generated.append(
                    stack_buffer,
                    static_cast<std::size_t>(piece_length));
            }

            return true;
        }

        const int required_size = -piece_length;

        if (required_size <= 0)
        {
            return false;
        }

        std::vector<char> dynamic_buffer(
            static_cast<std::size_t>(required_size));

        piece_length = llama_token_to_piece(
            vocab,
            token,
            dynamic_buffer.data(),
            static_cast<int32_t>(dynamic_buffer.size()),
            0,
            true);

        if (piece_length < 0)
        {
            return false;
        }

        if (piece_length > 0)
        {
            generated.append(
                dynamic_buffer.data(),
                static_cast<std::size_t>(piece_length));
        }

        return true;
    }
}

extern "C" ML_LLAMA_EXPORT int ml_llama_get_api_version(void)
{
    return 1;
}

extern "C" ML_LLAMA_EXPORT int ml_llama_get_last_error(
    unsigned char* output,
    int output_capacity)
{
    std::lock_guard<std::mutex> guard(g_mutex);

    copy_text(
        g_last_error,
        output,
        output_capacity);

    return static_cast<int>(g_last_error.size());
}

extern "C" ML_LLAMA_EXPORT int ml_llama_generate(
    const char* model_path,
    const char* prompt,
    int context_size,
    int max_output_tokens,
    int thread_count,
    float temperature,
    float top_p,
    unsigned char* output,
    int output_capacity)
{
    std::lock_guard<std::mutex> guard(g_mutex);
    g_last_error.clear();

    if (model_path == nullptr ||
        prompt == nullptr ||
        output == nullptr ||
        output_capacity < 2)
    {
        return fail(
            "Ungueltige Parameter fuer llama.cpp.",
            -1);
    }

    output[0] = 0;

    llama_model* model = nullptr;
    llama_context* context = nullptr;
    llama_sampler* sampler = nullptr;

    ggml_backend_load_all();

    llama_model_params model_params =
        llama_model_default_params();

    // Zunaechst konservativ nur CPU verwenden.
    // GPU-/Metal-Offloading kann spaeter separat aktiviert werden.
    model_params.n_gpu_layers = 0;

    model = llama_model_load_from_file(
        model_path,
        model_params);

    if (model == nullptr)
    {
        return fail(
            "Das GGUF-Modell konnte nicht geladen werden.",
            -2);
    }

    const llama_vocab* vocab =
        llama_model_get_vocab(model);

    if (vocab == nullptr)
    {
        free_runtime(sampler, context, model);

        return fail(
            "Das Vokabular des GGUF-Modells konnte nicht geladen werden.",
            -3);
    }

    const std::size_t prompt_length =
        std::strlen(prompt);

    const int32_t token_count = -llama_tokenize(
        vocab,
        prompt,
        static_cast<int32_t>(prompt_length),
        nullptr,
        0,
        true,
        true);

    if (token_count <= 0)
    {
        free_runtime(sampler, context, model);

        return fail(
            "Der Qwen-Prompt konnte nicht tokenisiert werden.",
            -4);
    }

    std::vector<llama_token> prompt_tokens(
        static_cast<std::size_t>(token_count));

    const int32_t actual_token_count =
        llama_tokenize(
            vocab,
            prompt,
            static_cast<int32_t>(prompt_length),
            prompt_tokens.data(),
            static_cast<int32_t>(prompt_tokens.size()),
            true,
            true);

    if (actual_token_count < 0)
    {
        free_runtime(sampler, context, model);

        return fail(
            "Der Qwen-Prompt konnte nicht in den Tokenpuffer geschrieben werden.",
            -5);
    }

    if (actual_token_count == 0)
    {
        free_runtime(sampler, context, model);

        return fail(
            "Der Qwen-Prompt enthaelt keine auswertbaren Tokens.",
            -6);
    }

    prompt_tokens.resize(
        static_cast<std::size_t>(actual_token_count));

    const int safe_context_size =
        std::max(context_size, 512);

    const int safe_output_tokens =
        std::max(max_output_tokens, 32);

    const int required_context_size =
        actual_token_count +
        safe_output_tokens +
        16;

    const uint32_t final_context_size =
        static_cast<uint32_t>(
            std::max(
                safe_context_size,
                required_context_size));

    const uint32_t final_batch_size =
        std::min<uint32_t>(
            final_context_size,
            static_cast<uint32_t>(
                std::max(actual_token_count, 128)));

    llama_context_params context_params =
        llama_context_default_params();

    context_params.n_ctx = final_context_size;
    context_params.n_batch = final_batch_size;
    context_params.n_threads =
        std::max(thread_count, 1);
    context_params.n_threads_batch =
        std::max(thread_count, 1);
    context_params.no_perf = true;

    context = llama_init_from_model(
        model,
        context_params);

    if (context == nullptr)
    {
        free_runtime(sampler, context, model);

        return fail(
            "Der llama.cpp-Kontext konnte nicht erstellt werden.",
            -7);
    }

    llama_sampler_chain_params sampler_params =
        llama_sampler_chain_default_params();

    sampler_params.no_perf = true;

    sampler =
        llama_sampler_chain_init(sampler_params);

    if (sampler == nullptr)
    {
        free_runtime(sampler, context, model);

        return fail(
            "Der llama.cpp-Sampler konnte nicht erstellt werden.",
            -8);
    }

    if (temperature <= 0.0f)
    {
        llama_sampler_chain_add(
            sampler,
            llama_sampler_init_greedy());
    }
    else
    {
        const float safe_top_p =
            std::clamp(top_p, 0.05f, 1.0f);

        llama_sampler_chain_add(
            sampler,
            llama_sampler_init_top_p(
                safe_top_p,
                1));

        llama_sampler_chain_add(
            sampler,
            llama_sampler_init_temp(
                temperature));

        llama_sampler_chain_add(
            sampler,
            llama_sampler_init_dist(
                LLAMA_DEFAULT_SEED));
    }

    llama_batch batch =
        llama_batch_get_one(
            prompt_tokens.data(),
            static_cast<int32_t>(
                prompt_tokens.size()));

    if (llama_model_has_encoder(model))
    {
        const int encode_result =
            llama_encode(context, batch);

        if (encode_result != 0)
        {
            free_runtime(sampler, context, model);

            return fail(
                "Der Qwen-Prompt konnte vom Encoder nicht verarbeitet werden.",
                -9);
        }

        llama_token decoder_start_token =
            llama_model_decoder_start_token(model);

        if (decoder_start_token == LLAMA_TOKEN_NULL)
        {
            decoder_start_token =
                llama_vocab_bos(vocab);
        }

        batch = llama_batch_get_one(
            &decoder_start_token,
            1);
    }
    else
    {
        const int decode_result =
            llama_decode(context, batch);

        if (decode_result != 0)
        {
            free_runtime(sampler, context, model);

            return fail(
                "Der Qwen-Prompt konnte nicht ausgewertet werden.",
                -10);
        }
    }

    std::string generated;
    generated.reserve(4096);

    for (int index = 0;
         index < safe_output_tokens;
         ++index)
    {
        const llama_token token =
            llama_sampler_sample(
                sampler,
                context,
                -1);

        if (llama_vocab_is_eog(vocab, token))
        {
            break;
        }

        if (!append_token_piece(
                vocab,
                token,
                generated))
        {
            free_runtime(sampler, context, model);

            return fail(
                "Ein Ausgabetoken konnte nicht in Text umgewandelt werden.",
                -11);
        }

        llama_token next_token = token;

        batch = llama_batch_get_one(
            &next_token,
            1);

        const int decode_result =
            llama_decode(context, batch);

        if (decode_result != 0)
        {
            free_runtime(sampler, context, model);

            return fail(
                "Die Qwen-Ausgabe konnte nicht fortgesetzt werden.",
                -12);
        }
    }

    free_runtime(sampler, context, model);

    if (generated.empty())
    {
        return fail(
            "Qwen hat keine Ausgabe erzeugt.",
            -13);
    }

    if (generated.size() >=
        static_cast<std::size_t>(output_capacity))
    {
        return fail(
            "Die Qwen-Ausgabe ist groesser als der Ausgabepuffer.",
            -14);
    }

    copy_text(
        generated,
        output,
        output_capacity);

    return static_cast<int>(generated.size());
}