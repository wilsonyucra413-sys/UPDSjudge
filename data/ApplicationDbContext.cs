using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UPDSjudgeB.Models;

namespace UPDSjudgeB.data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Tablas de la Base de Datos
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Rol> Roles { get; set; }
        public DbSet<UsuarioRol> UsuarioRoles { get; set; }
        public DbSet<Concurso> Concursos { get; set; }
        public DbSet<ParticipanteConcurso> ParticipantesConcursos { get; set; }
        public DbSet<Problema> Problemas { get; set; }
        public DbSet<CasoPrueba> CasosPrueba { get; set; }
        public DbSet<Lenguaje> Lenguajes { get; set; }
        public DbSet<Envio> Envios { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.correo)
                .IsUnique();

            modelBuilder.Entity<ParticipanteConcurso>()
                .HasIndex(p => new { p.idUsuario, p.idConcurso })
                .IsUnique();

            modelBuilder.Entity<UsuarioRol>()
                .HasOne(ur => ur.Usuario)
                .WithMany(u => u.UsuarioRoles)
                .HasForeignKey(ur => ur.idUsuario);

            modelBuilder.Entity<UsuarioRol>()
                .HasOne(ur => ur.Rol)
                .WithMany(r => r.UsuarioRoles)
                .HasForeignKey(ur => ur.idRol);


            modelBuilder.Entity<Rol>().HasData(
                new Rol { idRol = 1, nombre = "AdministradorRoles" },
                new Rol { idRol = 2, nombre = "AdministradorConcursos" },
                new Rol { idRol = 3, nombre = "Usuario" }
            );

            modelBuilder.Entity<Usuario>().HasData(
                new Usuario
                {
                    idUsuario = 1,
                    nombre = "Wilson",
                    correo = "wilsonyucra413@gmail.com",
                    contrasena = "$2a$11$lHroH6rOVBeO6wtdG9F0ouo3.7i3HNXiuNXtTcykjDO2tKsGtc7kS",
                    fechaRegistro = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    estado = "Activo"
                }
            );

            modelBuilder.Entity<UsuarioRol>().HasData(
                new UsuarioRol { idUsuarioRol = 1, idUsuario = 1, idRol = 1 }, // AdministradorRoles
                new UsuarioRol { idUsuarioRol = 2, idUsuario = 1, idRol = 2 }, // AdministradorConcursos
                new UsuarioRol { idUsuarioRol = 3, idUsuario = 1, idRol = 3 }  //
            );

            modelBuilder.Entity<Lenguaje>().HasData(
                new Lenguaje { idLenguaje = 1, idJudge0 = 54, nombre = "C++ (GCC 9.2.0)", extension = "cpp", estado = "Activo" },
                new Lenguaje { idLenguaje = 2, idJudge0 = 71, nombre = "Python (3.8.1)", extension = "py", estado = "Activo" },
                new Lenguaje { idLenguaje = 3, idJudge0 = 51, nombre = "C# (Mono 6.6.0.161)", extension = "cs", estado = "Activo" }
            );

        }
    }
}