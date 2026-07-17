using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace UPDSjudgeB.Migrations
{
    /// <inheritdoc />
    public partial class m1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Lenguajes",
                columns: table => new
                {
                    idLenguaje = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    idJudge0 = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    extension = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lenguajes", x => x.idLenguaje);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    idRol = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.idRol);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    idUsuario = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    correo = table.Column<string>(type: "text", nullable: false),
                    contrasena = table.Column<string>(type: "text", nullable: false),
                    fechaRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.idUsuario);
                });

            migrationBuilder.CreateTable(
                name: "Concursos",
                columns: table => new
                {
                    idConcurso = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: false),
                    fechaInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    duracionMinutos = table.Column<int>(type: "integer", nullable: false),
                    contrasena = table.Column<string>(type: "text", nullable: true),
                    urlSetProblemas = table.Column<string>(type: "text", nullable: false),
                    minutosCongelamiento = table.Column<int>(type: "integer", nullable: false),
                    codigo = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false),
                    idUsuarioCreador = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Concursos", x => x.idConcurso);
                    table.ForeignKey(
                        name: "FK_Concursos_Usuarios_idUsuarioCreador",
                        column: x => x.idUsuarioCreador,
                        principalTable: "Usuarios",
                        principalColumn: "idUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuarioRoles",
                columns: table => new
                {
                    idUsuarioRol = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    idUsuario = table.Column<int>(type: "integer", nullable: false),
                    idRol = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioRoles", x => x.idUsuarioRol);
                    table.ForeignKey(
                        name: "FK_UsuarioRoles_Roles_idRol",
                        column: x => x.idRol,
                        principalTable: "Roles",
                        principalColumn: "idRol",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsuarioRoles_Usuarios_idUsuario",
                        column: x => x.idUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "idUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParticipantesConcursos",
                columns: table => new
                {
                    idParticipanteConcurso = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fechaIngreso = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false),
                    idUsuario = table.Column<int>(type: "integer", nullable: false),
                    idConcurso = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantesConcursos", x => x.idParticipanteConcurso);
                    table.ForeignKey(
                        name: "FK_ParticipantesConcursos_Concursos_idConcurso",
                        column: x => x.idConcurso,
                        principalTable: "Concursos",
                        principalColumn: "idConcurso",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParticipantesConcursos_Usuarios_idUsuario",
                        column: x => x.idUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "idUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Problemas",
                columns: table => new
                {
                    idProblema = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    inciso = table.Column<char>(type: "character(1)", nullable: false),
                    titulo = table.Column<string>(type: "text", nullable: false),
                    tiempo = table.Column<float>(type: "real", nullable: false),
                    memoria = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false),
                    idConcurso = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Problemas", x => x.idProblema);
                    table.ForeignKey(
                        name: "FK_Problemas_Concursos_idConcurso",
                        column: x => x.idConcurso,
                        principalTable: "Concursos",
                        principalColumn: "idConcurso",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CasosPrueba",
                columns: table => new
                {
                    idCasoPrueba = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entrada = table.Column<string>(type: "text", nullable: false),
                    salida = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false),
                    idProblema = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CasosPrueba", x => x.idCasoPrueba);
                    table.ForeignKey(
                        name: "FK_CasosPrueba_Problemas_idProblema",
                        column: x => x.idProblema,
                        principalTable: "Problemas",
                        principalColumn: "idProblema",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Envios",
                columns: table => new
                {
                    idEnvio = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "text", nullable: false),
                    resultado = table.Column<string>(type: "text", nullable: false),
                    tiempo = table.Column<float>(type: "real", nullable: false),
                    memoria = table.Column<int>(type: "integer", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    upsolving = table.Column<string>(type: "text", nullable: false),
                    fechaEnvio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    idUsuario = table.Column<int>(type: "integer", nullable: false),
                    idProblema = table.Column<int>(type: "integer", nullable: false),
                    idLenguaje = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Envios", x => x.idEnvio);
                    table.ForeignKey(
                        name: "FK_Envios_Lenguajes_idLenguaje",
                        column: x => x.idLenguaje,
                        principalTable: "Lenguajes",
                        principalColumn: "idLenguaje",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Envios_Problemas_idProblema",
                        column: x => x.idProblema,
                        principalTable: "Problemas",
                        principalColumn: "idProblema",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Envios_Usuarios_idUsuario",
                        column: x => x.idUsuario,
                        principalTable: "Usuarios",
                        principalColumn: "idUsuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Lenguajes",
                columns: new[] { "idLenguaje", "estado", "extension", "idJudge0", "nombre" },
                values: new object[,]
                {
                    { 1, "Activo", "cpp", 54, "C++ (GCC 9.2.0)" },
                    { 2, "Activo", "py", 71, "Python (3.8.1)" },
                    { 3, "Activo", "cs", 51, "C# (Mono 6.6.0.161)" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "idRol", "nombre" },
                values: new object[,]
                {
                    { 1, "AdministradorRoles" },
                    { 2, "AdministradorConcursos" },
                    { 3, "Usuario" }
                });

            migrationBuilder.InsertData(
                table: "Usuarios",
                columns: new[] { "idUsuario", "contrasena", "correo", "estado", "fechaRegistro", "nombre" },
                values: new object[] { 1, "$2a$11$lHroH6rOVBeO6wtdG9F0ouo3.7i3HNXiuNXtTcykjDO2tKsGtc7kS", "wilsonyucra413@gmail.com", "Activo", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Wilson" });

            migrationBuilder.InsertData(
                table: "UsuarioRoles",
                columns: new[] { "idUsuarioRol", "idRol", "idUsuario" },
                values: new object[,]
                {
                    { 1, 1, 1 },
                    { 2, 2, 1 },
                    { 3, 3, 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CasosPrueba_idProblema",
                table: "CasosPrueba",
                column: "idProblema");

            migrationBuilder.CreateIndex(
                name: "IX_Concursos_idUsuarioCreador",
                table: "Concursos",
                column: "idUsuarioCreador");

            migrationBuilder.CreateIndex(
                name: "IX_Envios_idLenguaje",
                table: "Envios",
                column: "idLenguaje");

            migrationBuilder.CreateIndex(
                name: "IX_Envios_idProblema",
                table: "Envios",
                column: "idProblema");

            migrationBuilder.CreateIndex(
                name: "IX_Envios_idUsuario",
                table: "Envios",
                column: "idUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesConcursos_idConcurso",
                table: "ParticipantesConcursos",
                column: "idConcurso");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesConcursos_idUsuario_idConcurso",
                table: "ParticipantesConcursos",
                columns: new[] { "idUsuario", "idConcurso" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Problemas_idConcurso",
                table: "Problemas",
                column: "idConcurso");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioRoles_idRol",
                table: "UsuarioRoles",
                column: "idRol");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioRoles_idUsuario",
                table: "UsuarioRoles",
                column: "idUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_correo",
                table: "Usuarios",
                column: "correo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CasosPrueba");

            migrationBuilder.DropTable(
                name: "Envios");

            migrationBuilder.DropTable(
                name: "ParticipantesConcursos");

            migrationBuilder.DropTable(
                name: "UsuarioRoles");

            migrationBuilder.DropTable(
                name: "Lenguajes");

            migrationBuilder.DropTable(
                name: "Problemas");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Concursos");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
