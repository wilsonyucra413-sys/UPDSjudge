using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static UPDSjudgeB.DTOs.Judge0Dto;

namespace UPDSjudgeB.Services
{
    public interface IJudge0Service
    {
        Task<Judge0SubmissionResponseDto> EjecutarAsync(
            int languageId, string sourceCode, string stdin,
            float cpuTimeLimit, int memoryLimitKb);
    }
}