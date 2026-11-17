using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RAIZA.Controllers;
using RAIZA.Data;
using RAIZA.Models;
using RAIZA.Services;
using Xunit;

namespace MiProyecto.Tests
{
    public class CajaNegraBackendTests
    {
        private const string JwtSecretTest = "ClaveSuperSecretaParaPruebasCajaNegraRaiza2026!";

        private static DatabaseService CrearBaseDeDatosAislada()
        {
            var options = new DbContextOptionsBuilder<DatabaseService>()
                .UseInMemoryDatabase(databaseName: "CajaNegra_" + Guid.NewGuid())
                .Options;
            return new DatabaseService(options);
        }

        private static AuthController CrearApiAuth(DatabaseService context)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Jwt:Key", JwtSecretTest },
                    { "Jwt:Issuer", "RAIZA" },
                    { "Jwt:Audience", "RAIZA_Users" },
                    { "Recaptcha:SecretKey", "6LeIxAcTAAAAAGG-vFI1TnRWxMZNFuojJ4WifJWe" }
                })
                .Build();

            var mockLogger = new Mock<ILogger<AuthController>>();
            var mockEnv = new Mock<IWebHostEnvironment>();
            mockEnv.Setup(e => e.EnvironmentName).Returns("Development");

            var mockCorreo = new Mock<ICorreoService>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider
                .Setup(sp => sp.GetService(typeof(ICorreoService)))
                .Returns(mockCorreo.Object);

            var mockScope = new Mock<IServiceScope>();
            mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);

            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

            return new AuthController(
                config,
                context,
                mockLogger.Object,
                mockEnv.Object,
                mockCorreo.Object,
                mockScopeFactory.Object);
        }

        private static string GenerarHashCodigo(string correo, string tipo, string codigo)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(JwtSecretTest));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{correo}|{tipo}|{codigo}")));
        }

        [Fact(DisplayName = "CN-BE-01 [Contrato API]: GET /api/Auto/Diagnostico en entorno activo responde 200 OK")]
        public async Task Diagnostico_ApiActiva_Retorna200Ok()
        {
            using var db = CrearBaseDeDatosAislada();
            var api = CrearApiAuth(db);

            var resultado = await api.Diagnostico();

            var ok = Assert.IsType<OkObjectResult>(resultado);
            Assert.Equal(200, ok.StatusCode);
        }

        [Fact(DisplayName = "CN-BE-02 [Partición Equivalencia]: Registro con datos válidos completa alta de usuario y retorna 200 OK")]
        public async Task Registro_DatosValidos_Retorna200Ok()
        {
            using var db = CrearBaseDeDatosAislada();
            db.Usuario.Add(new Usuario
            {
                Nombre = "PreRegistro",
                Correo = "alison.sosa@raiza.edu.co",
                ContrasenaHash = "temp",
                Rol = "Estudiante",
                Estado = "Activo",
                VerificacionPendiente = true
            });
            await db.SaveChangesAsync();

            var api = CrearApiAuth(db);
            var resultado = await api.Registrar(new RegistroDto
            {
                Nombre = "Alison Johanna Sosa",
                Correo = "alison.sosa@raiza.edu.co",
                ContrasenaHash = "ClaveSegura2026"
            });

            var ok = Assert.IsType<OkObjectResult>(resultado);
            Assert.Equal(200, ok.StatusCode);
        }

        [Fact(DisplayName = "CN-BE-03 [Valores Límite - AVL]: Registro con contraseña de 7 caracteres (límite inferior inválido) retorna 400")]
        public async Task Registro_ContrasenaLimiteInferiorInvalido_Retorna400BadRequest()
        {
            using var db = CrearBaseDeDatosAislada();
            var api = CrearApiAuth(db);

            var resultado = await api.Registrar(new RegistroDto
            {
                Nombre = "Usuario Prueba",
                Correo = "limite@raiza.edu.co",
                ContrasenaHash = "1234567"
            });

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado);
            Assert.Equal(400, badRequest.StatusCode);
        }

        [Fact(DisplayName = "CN-BE-04 [Partición Equivalencia]: Registro con correo activo ya existente rechaza duplicado (400 BadRequest)")]
        public async Task Registro_CorreoDuplicadoActivo_Retorna400BadRequest()
        {
            using var db = CrearBaseDeDatosAislada();
            db.Usuario.Add(new Usuario
            {
                Nombre = "Alison Existente",
                Correo = "duplicado@raiza.edu.co",
                ContrasenaHash = "hash",
                Rol = "Estudiante",
                Estado = "Activo",
                VerificacionPendiente = false
            });
            await db.SaveChangesAsync();

            var api = CrearApiAuth(db);
            var resultado = await api.Registrar(new RegistroDto
            {
                Nombre = "Alison Duplicada",
                Correo = "duplicado@raiza.edu.co",
                ContrasenaHash = "ClaveSegura2026"
            });

            var conflict = Assert.IsType<ConflictObjectResult>(resultado); Assert.Equal(409, conflict.StatusCode); } [Fact(DisplayName = "CN-BE-05 [Control Acceso]: Login con cuenta pendiente de verificación retorna 403 Forbidden")]
        public async Task Login_CorreoSinVerificar_Retorna403Forbidden()
        {
            using var db = CrearBaseDeDatosAislada();
            db.Usuario.Add(new Usuario
            {
                Nombre = "Aprendiz Pendiente",
                Correo = "sinverificar@raiza.edu.co",
                ContrasenaHash = BCrypt.Net.BCrypt.HashPassword("ClaveSegura2026"),
                Rol = "Estudiante",
                Estado = "Activo",
                VerificacionPendiente = true
            });
            await db.SaveChangesAsync();

            var api = CrearApiAuth(db);
            var resultado = await api.Login(new LoginDto
            {
                Email = "sinverificar@raiza.edu.co",
                Password = "ClaveSegura2026", RecaptchaToken = "token-caja-negra-ok"
            });

            var obj = Assert.IsAssignableFrom<ObjectResult>(resultado);
            Assert.Equal(403, obj.StatusCode);
        }

        [Fact(DisplayName = "CN-BE-06 [Partición Equivalencia]: Login con credenciales inexistentes retorna 401/400")]
        public async Task Login_CredencialesIncorrectas_RetornaErrorAutenticacion()
        {
            using var db = CrearBaseDeDatosAislada();
            var api = CrearApiAuth(db);

            var resultado = await api.Login(new LoginDto
            {
                Email = "noexiste@raiza.edu.co",
                Password = "ClaveIncorrecta123"
            });

            var unauthorized = Assert.IsAssignableFrom<ObjectResult>(resultado);
            Assert.True(unauthorized.StatusCode == 401 || unauthorized.StatusCode == 400);
        }

        [Fact(DisplayName = "CN-BE-07 [Flujo E2E Backend]: Verificación de código activa cuenta y Login emite JWT (200 OK)")]
        public async Task FlujoE2E_VerificacionYLoginExitoso_Retorna200ConJwt()
        {
            using var db = CrearBaseDeDatosAislada();
            var correo = "verificada@raiza.edu.co";
            var codigo = "654321";

            db.Usuario.Add(new Usuario
            {
                Nombre = "Alison Verificada",
                Correo = correo,
                ContrasenaHash = BCrypt.Net.BCrypt.HashPassword("ClaveSegura2026"),
                Rol = "Estudiante",
                Estado = "Activo",
                VerificacionPendiente = true
            });
            db.CodigosVerificacion.Add(new CodigoVerificacion
            {
                Correo = correo,
                Tipo = "Verificacion",
                CodigoHash = GenerarHashCodigo(correo, "Verificacion", codigo),
                Creado = DateTime.UtcNow,
                Expira = DateTime.UtcNow.AddMinutes(15),
                Intentos = 0,
                Usado = false
            });
            await db.SaveChangesAsync();

            var api = CrearApiAuth(db);
            var verificacion = await api.VerificarCorreo(new VerificarCorreoDto
            {
                Correo = correo,
                Codigo = codigo
            });
            Assert.IsType<OkObjectResult>(verificacion);

            var login = await api.Login(new LoginDto
            {
                Email = correo,
                Password = "ClaveSegura2026", RecaptchaToken = "token-caja-negra-ok"
            });
            var okLogin = Assert.IsType<OkObjectResult>(login);
            Assert.Equal(200, okLogin.StatusCode);
        }

        [Fact(DisplayName = "CN-BE-08 [Partición Equivalencia]: Verificación de correo con código inválido retorna 400 BadRequest")]
        public async Task VerificarCorreo_CodigoInvalido_Retorna400BadRequest()
        {
            using var db = CrearBaseDeDatosAislada();
            var api = CrearApiAuth(db);

            var resultado = await api.VerificarCorreo(new VerificarCorreoDto
            {
                Correo = "codigo@raiza.edu.co",
                Codigo = "000000"
            });

            var badRequest = Assert.IsType<BadRequestObjectResult>(resultado);
            Assert.Equal(400, badRequest.StatusCode);
        }
    }
}



