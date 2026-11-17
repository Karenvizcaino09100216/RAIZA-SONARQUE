using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using RAIZA.Controllers;
using RAIZA.Data;
using RAIZA.Models;
using RAIZA.Services;
using Xunit;

namespace MiProyecto.Tests
{
    public class AuthControllerTests
    {
        private const string JwtSecretTest = "ClaveSuperSecretaParaPruebasUnitariasDeRaiza2026!";

        private static DatabaseService CrearContextoEnMemoria()
        {
            var options = new DbContextOptionsBuilder<DatabaseService>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new DatabaseService(options);
        }

        private static IConfiguration CrearConfiguracion()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Jwt:Key", JwtSecretTest },
                    { "Jwt:Issuer", "RAIZA" },
                    { "Jwt:Audience", "RAIZA_Users" },
                    { "Recaptcha:SecretKey", "" }
                })
                .Build();
        }

        private static AuthController CrearControlador(
            DatabaseService context,
            string entorno = "Development",
            Mock<ICorreoService>? mockCorreo = null)
        {
            var config = CrearConfiguracion();
            var mockLogger = new Mock<ILogger<AuthController>>();
            var mockEnv = new Mock<IHostEnvironment>();
            mockEnv.Setup(e => e.EnvironmentName).Returns(entorno);

            mockCorreo ??= new Mock<ICorreoService>();

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

        [Fact]
        public async Task Diagnostico_EnProduccion_RetornaNotFound()
        {
            using var context = CrearContextoEnMemoria();
            var controller = CrearControlador(context, Environments.Production);

            var resultado = await controller.Diagnostico();

            Assert.IsType<NotFoundResult>(resultado);
        }

        [Fact]
        public async Task Diagnostico_EnDesarrollo_RetornaOk()
        {
            using var context = CrearContextoEnMemoria();
            var controller = CrearControlador(context, Environments.Development);

            var resultado = await controller.Diagnostico();

            Assert.IsType<OkObjectResult>(resultado);
        }

        [Fact]
        public async Task Login_CredencialesIncompletas_RetornaBadRequest()
        {
            using var context = CrearContextoEnMemoria();
            var controller = CrearControlador(context);

            var resultado = await controller.Login(new LoginDto { Email = "", Password = "" });

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Login_RecaptchaFalla_RetornaBadRequest()
        {
            using var context = CrearContextoEnMemoria();
            var controller = CrearControlador(context);

            var resultado = await controller.Login(new LoginDto
            {
                Email = "alison@raiza.com",
                Password = "Password123",
                RecaptchaToken = "token-falso"
            });

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Registrar_CorreoInvalido_RetornaBadRequest()
        {
            using var context = CrearContextoEnMemoria();
            var controller = CrearControlador(context);

            var resultado = await controller.Registrar(new RegistroDto
            {
                Nombre = "Alison Sosa",
                Correo = "correo-sin-arroba",
                ContrasenaHash = "Clave1234"
            });

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Registrar_PasswordDebil_RetornaBadRequest()
        {
            using var context = CrearContextoEnMemoria();
            var controller = CrearControlador(context);

            var resultado = await controller.Registrar(new RegistroDto
            {
                Nombre = "Alison Sosa",
                Correo = "alison@raiza.com",
                ContrasenaHash = "123"
            });

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task Registrar_CorreoYaVerificado_RetornaConflict()
        {
            using var context = CrearContextoEnMemoria();
            context.Usuario.Add(new Usuario
            {
                Nombre = "Alison",
                Correo = "alison@raiza.com",
                ContrasenaHash = BCrypt.Net.BCrypt.HashPassword("Clave1234"),
                Rol = "Estudiante",
                Estado = "Activo",
                VerificacionPendiente = false
            });
            await context.SaveChangesAsync();

            var controller = CrearControlador(context);

            var resultado = await controller.Registrar(new RegistroDto
            {
                Nombre = "Alison Nueva",
                Correo = "alison@raiza.com",
                ContrasenaHash = "Clave1234"
            });

            Assert.IsType<ConflictObjectResult>(resultado);
        }

        [Fact]
        public async Task Registrar_UsuarioPendienteExistente_ActualizaYRetornaOk()
        {
            using var context = CrearContextoEnMemoria();
            context.Usuario.Add(new Usuario
            {
                Nombre = "Alison Anterior",
                Correo = "alison@raiza.com",
                ContrasenaHash = "hash_viejo",
                Rol = "Estudiante",
                Estado = "Activo",
                VerificacionPendiente = true
            });
            await context.SaveChangesAsync();

            var controller = CrearControlador(context);

            var resultado = await controller.Registrar(new RegistroDto
            {
                Nombre = "Alison Actualizada",
                Correo = "alison@raiza.com",
                ContrasenaHash = "NuevaClave123"
            });

            Assert.IsType<OkObjectResult>(resultado);
            var usuarioDb = await context.Usuario.FirstAsync(u => u.Correo == "alison@raiza.com");
            Assert.Equal("Alison Actualizada", usuarioDb.Nombre);
        }

        [Fact]
        public async Task VerificarCorreo_CodigoValido_ActivaUsuarioYRetornaOk()
        {
            using var context = CrearContextoEnMemoria();
            var correo = "alison@raiza.com";
            var codigoPlano = "654321";

            context.Usuario.Add(new Usuario
            {
                Nombre = "Alison",
                Correo = correo,
                ContrasenaHash = "hash",
                Rol = "Estudiante",
                Estado = "Activo",
                VerificacionPendiente = true
            });

            context.CodigosVerificacion.Add(new CodigoVerificacion
            {
                Correo = correo,
                Tipo = "Verificacion",
                CodigoHash = GenerarHashCodigo(correo, "Verificacion", codigoPlano),
                Creado = DateTime.UtcNow,
                Expira = DateTime.UtcNow.AddMinutes(15),
                Intentos = 0,
                Usado = false
            });
            await context.SaveChangesAsync();

            var controller = CrearControlador(context);

            var resultado = await controller.VerificarCorreo(new VerificarCorreoDto
            {
                Correo = correo,
                Codigo = codigoPlano
            });

            Assert.IsType<OkObjectResult>(resultado);
            var usuario = await context.Usuario.FirstAsync(u => u.Correo == correo);
            Assert.False(usuario.VerificacionPendiente);
        }

        [Fact]
        public async Task VerificarCorreo_CodigoIncorrecto_IncrementaIntentosYRetornaBadRequest()
        {
            using var context = CrearContextoEnMemoria();
            var correo = "alison@raiza.com";

            context.Usuario.Add(new Usuario
            {
                Nombre = "Alison",
                Correo = correo,
                ContrasenaHash = "hash",
                Rol = "Estudiante",
                Estado = "Activo",
                VerificacionPendiente = true
            });

            context.CodigosVerificacion.Add(new CodigoVerificacion
            {
                Correo = correo,
                Tipo = "Verificacion",
                CodigoHash = GenerarHashCodigo(correo, "Verificacion", "111111"),
                Creado = DateTime.UtcNow,
                Expira = DateTime.UtcNow.AddMinutes(15),
                Intentos = 0,
                Usado = false
            });
            await context.SaveChangesAsync();

            var controller = CrearControlador(context);

            var resultado = await controller.VerificarCorreo(new VerificarCorreoDto
            {
                Correo = correo,
                Codigo = "999999"
            });

            Assert.IsType<BadRequestObjectResult>(resultado);
            var codigoDb = await context.CodigosVerificacion.FirstAsync(c => c.Correo == correo);
            Assert.Equal(1, codigoDb.Intentos);
        }

        [Fact]
        public async Task RestablecerPassword_CodigoValido_CambiaPasswordYRetornaOk()
        {
            using var context = CrearContextoEnMemoria();
            var correo = "alison@raiza.com";
            var codigoPlano = "777888";

            context.Usuario.Add(new Usuario
            {
                Nombre = "Alison",
                Correo = correo,
                ContrasenaHash = "clave_vieja",
                Rol = "Estudiante",
                Estado = "Activo",
                VerificacionPendiente = true
            });

            context.CodigosVerificacion.Add(new CodigoVerificacion
            {
                Correo = correo,
                Tipo = "Recuperacion",
                CodigoHash = GenerarHashCodigo(correo, "Recuperacion", codigoPlano),
                Creado = DateTime.UtcNow,
                Expira = DateTime.UtcNow.AddMinutes(15),
                Intentos = 0,
                Usado = false
            });
            await context.SaveChangesAsync();

            var controller = CrearControlador(context);

            var resultado = await controller.RestablecerPassword(new RestablecerPasswordDto
            {
                Email = correo,
                Codigo = codigoPlano,
                NuevaContrasena = "NuevaPassword2026"
            });

            Assert.IsType<OkObjectResult>(resultado);
            var usuario = await context.Usuario.FirstAsync(u => u.Correo == correo);
            Assert.False(usuario.VerificacionPendiente);
            Assert.True(BCrypt.Net.BCrypt.Verify("NuevaPassword2026", usuario.ContrasenaHash));
        }
    }
}