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

        public Judge0Service(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<Judge0SubmissionResponseDto> EjecutarAsync(
            int languageId, string sourceCode, string stdin,
            float cpuTimeLimit, int memoryLimitKb)
        {
            var request = new Judge0SubmissionRequestDto
            {
                LanguageId = languageId,
                SourceCode = sourceCode,
                Stdin = stdin,
                CpuTimeLimit = cpuTimeLimit,
                MemoryLimit = memoryLimitKb
            };

            string jsonBody = JsonSerializer.Serialize(request);

            using var content = new StringContent(jsonBody, Encoding.UTF8);
            // Sobrescribimos el Content-Type SIN el charset, porque Judge0 no lo acepta
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsync("submissions?wait=true", content);
            }
            catch (HttpRequestException ex)
            {
                throw new Judge0NoDisponibleException("No se pudo conectar con el motor de ejecución.", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                var cuerpoError = await response.Content.ReadAsStringAsync();
                throw new Judge0NoDisponibleException(
                    $"Judge0 respondió con error {(int)response.StatusCode}: {cuerpoError}");
            }

            var resultado = await response.Content.ReadFromJsonAsync<Judge0SubmissionResponseDto>();
            if (resultado == null)
                throw new Judge0NoDisponibleException("Judge0 devolvió una respuesta vacía o inválida.");

            return resultado;
        }
    }

    // Excepción específica para poder distinguir "Judge0 caído" de otros errores
    public class Judge0NoDisponibleException : Exception
    {
        public Judge0NoDisponibleException(string mensaje) : base(mensaje) { }
        public Judge0NoDisponibleException(string mensaje, Exception inner) : base(mensaje, inner) { }
    }
}