# API REST - Endpoints

| # | Método | Ruta | Descripción |
|---:|:------:|------|-------------|
| 1 | POST | `/api/Auth/login` | Iniciar sesión y obtener un token JWT. |
| 2 | POST | `/api/Auth/register` | Registrar un nuevo usuario. |
| 3 | GET | `/api/Concursos` | Listar concursos con filtros, búsqueda y paginación. |
| 4 | GET | `/api/Concursos/detalle/{codigo}` | Obtener la información detallada de un concurso. |
| 5 | POST | `/api/ParticipanteConcursos/unirse` | Inscribirse a un concurso público o privado. |
| 6 | POST | `/api/Envios` | Enviar una solución para evaluar un problema. |
| 7 | GET | `/api/Envios/mis-envios` | Listar los envíos del usuario con filtros y paginación. |
| 8 | GET | `/api/Envios/concurso/{concursoCodigo}` | Listar los envíos del usuario de un concurso específico. |
| 9 | GET | `/api/ParticipanteConcursos/stats-contest` | Obtener las estadísticas generales del usuario en concursos. |
| 10 | GET | `/api/Concursos/dashboard/{codigo}` | Obtener el dashboard del concurso para el usuario. |
| 11 | GET | `/api/Concursos/{codigoConcurso}/ranking` | Obtener el ranking ICPC del concurso. |
| 12 | POST | `/api/Concursos/crear` | Crear un nuevo concurso con sus problemas y casos de prueba. |
| 13 | PUT | `/api/Concursos/{codigo}` | Actualizar un concurso antes de que inicie. |
| 14 | GET | `/api/Concursos/editar/{codigo}` | Obtener los datos de un concurso para su edición. |
| 15 | GET | `/api/Concursos/mis-creados` | Listar los concursos creados por el administrador. |
| 16 | GET | `/api/Concursos/mis-resumen` | Obtener el resumen de concursos creados por el administrador. |
| 17 | GET | `/api/Concursos/mis-registros` | Obtener los concursos en los que el usuario está inscrito. |
| 18 | GET | `/api/Roles` | Listar todos los roles disponibles. |
| 19 | GET | `/api/Roles/usuarios` | Buscar usuarios con sus roles y paginación. |
| 20 | POST | `/api/Roles/agregar` | Asignar un rol a un usuario. |
| 21 | POST | `/api/Roles/quitar` | Quitar un rol a un usuario. |