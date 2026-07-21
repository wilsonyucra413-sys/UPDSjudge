| Método | Ruta                                | Descripción                                      |
|--------|--------------------------------------|---------------------------------------------------|
| POST   | `/api/Auth/register`                | Registrar usuario                                  |
| POST   | `/api/Auth/login`                   | Obtener JWT                                        |
| GET    | `/api/Concursos`                    | Listar concursos por estado                        |
| POST   | `/api/Concursos/crear`              | Crear concurso + problemas + ZIP                   |
| GET    | `/api/Concursos/mis-registros`      | Concursos donde estoy inscrito                      |
| GET    | `/api/Concursos/detalle/{codigo}`   | Detalle del concurso + problemas (si ya inició)     |
| POST   | `/api/Concursos/unirse`             | Inscribirse a un concurso (valida contraseña si es privado) |
| GET    | `/api/Roles`                        | Listar roles disponibles                            |
| GET    | `/api/Roles/usuarios`               | Buscar usuarios y sus roles                         |
| POST   | `/api/Roles/usuarios`               | Asignar rol a un usuario                            |
| POST   | `/api/Roles/quitar`                 | Quitar rol a un usuario                             |
| POST   | `/api/Envios`                       | Crear envío (código fuente → Judge0)                |
| GET    | `/api/Envios/me`                    | Historial personal de envíos                        |
| GET    | `/api/Envios/{id}`                  | Estado de un envío puntual                          |
| GET    | `/api/Concursos/{codigo}/ranking`   | Ranking público del concurso                        |
| PUT    | `/api/Concursos/{codigo}`           | Editar un concurso                                  |
| DELETE | `/api/Concursos/{codigo}`           | Borrado lógico de un concurso                       |
| HUB    | `/hubs/envios`                      | Notificación en tiempo real de veredicto            |

---