using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UPDSjudgeB.DTOs;

namespace UPDSjudgeB.Services
{
    public interface IEvaluacionEnvioService
    {
        Task<EnvioResultadoDto> EvaluarYGuardarAsync(
            int idProblema, int idLenguaje, string codigoFuente,
            int idUsuario, bool esUpsolving);
    }
}