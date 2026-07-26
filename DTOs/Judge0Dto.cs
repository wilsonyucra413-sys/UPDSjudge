using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UPDSjudgeB.DTOs
{
    public class Judge0Dto
    {
        public class Judge0SubmissionRequestDto
        {
            [JsonPropertyName("language_id")]
            public int LanguageId { get; set; }

            [JsonPropertyName("source_code")]
            public string SourceCode { get; set; }

            [JsonPropertyName("stdin")]
            public string Stdin { get; set; }

            [JsonPropertyName("cpu_time_limit")]
            public float CpuTimeLimit { get; set; }

            [JsonPropertyName("memory_limit")]
            public int MemoryLimit { get; set; }
        }

        public class Judge0StatusDto
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("description")]
            public string? Description { get; set; }
        }

        public class Judge0SubmissionResponseDto
        {
            [JsonPropertyName("stdout")]
            public string? Stdout { get; set; }

            [JsonPropertyName("stderr")]
            public string? Stderr { get; set; }

            [JsonPropertyName("compile_output")]
            public string? CompileOutput { get; set; }

            [JsonPropertyName("message")]
            public string? Message { get; set; }

            [JsonPropertyName("time")]
            public string? Time { get; set; }

            [JsonPropertyName("memory")]
            public int? Memory { get; set; }

            [JsonPropertyName("token")]
            public string? Token { get; set; }

            [JsonPropertyName("status")]
            public Judge0StatusDto? Status { get; set; }
        }
    }
}