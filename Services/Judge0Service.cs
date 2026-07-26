using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UPDSjudgeB.DTOs;
using static UPDSjudgeB.DTOs.Judge0Dto;

namespace UPDSjudgeB.Services
{
    public class Judge0Service : IJudge0Service
    {
        private readonly HttpClient _httpClient;
        private static readonly JsonSerializerOptions OpcionesJson = new()
        {
            PropertyNameCaseInsensitive = true
        };
        public Judge0Service(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }


        public async Task<Judge0SubmissionResponseDto> EjecutarAsync(
            int languageId, string sourceCode, string stdin,
            float cpuTimeLimit, int memoryLimitKb)
        {
            string sourceCodeBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(sourceCode ?? string.Empty));
            string stdinBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(stdin ?? string.Empty));

            var request = new Judge0SubmissionRequestDto
            {
                LanguageId = languageId,
                SourceCode = sourceCodeBase64,
                Stdin = stdinBase64,
                CpuTimeLimit = cpuTimeLimit,
                MemoryLimit = memoryLimitKb
            };

            string jsonBody = JsonSerializer.Serialize(request);
            using var content = new StringContent(jsonBody, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage response;
            try
            {
                // base64_encoded=true va en la URL, como especificaste
                response = await _httpClient.PostAsync("submissions?wait=true&base64_encoded=true", content);
            }
            catch (HttpRequestException ex)
            {
                throw new Judge0NoDisponibleException("No se pudo conectar con el motor de ejecución.", ex);
            }

            string rawJson = await response.Content.ReadAsStringAsync();

            // TEMPORAL: para confirmar exactamente qué llegó, bórralo cuando funcione
            Console.WriteLine($"[JUDGE0 RAW RESPONSE] Status HTTP: {(int)response.StatusCode} | Body: {rawJson}");

            if (!response.IsSuccessStatusCode)
            {
                throw new Judge0NoDisponibleException(
                    $"Judge0 respondió con error {(int)response.StatusCode}: {rawJson}");
            }

            Judge0SubmissionResponseDto? crudo;
            try
            {
                crudo = JsonSerializer.Deserialize<Judge0SubmissionResponseDto>(rawJson, OpcionesJson);
            }
            catch (JsonException ex)
            {
                throw new Judge0NoDisponibleException($"No se pudo interpretar la respuesta de Judge0: {ex.Message}. JSON crudo: {rawJson}");
            }

            if (crudo == null)
                throw new Judge0NoDisponibleException($"Judge0 devolvió una respuesta vacía. JSON crudo: {rawJson}");

            if (crudo.Status == null)
                throw new Judge0NoDisponibleException($"Judge0 respondió sin campo 'status'. JSON crudo: {rawJson}");

            crudo.Stdout = DecodificarBase64(crudo.Stdout);
            crudo.Stderr = DecodificarBase64(crudo.Stderr);
            crudo.CompileOutput = DecodificarBase64(crudo.CompileOutput);
            crudo.Message = DecodificarBase64(crudo.Message);

            return crudo;
        }

        private static string? DecodificarBase64(string? valor)
        {
            if (string.IsNullOrEmpty(valor)) return valor;
            try
            {
                var bytes = Convert.FromBase64String(valor);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException)
            {
                return valor;
            }
        }
    }

    public class Judge0NoDisponibleException : Exception
    {
        public Judge0NoDisponibleException(string mensaje) : base(mensaje) { }
        public Judge0NoDisponibleException(string mensaje, Exception inner) : base(mensaje, inner) { }
    }
}