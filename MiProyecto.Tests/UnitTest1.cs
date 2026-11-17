using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using RAIZA.Services;
using Xunit;

namespace MiProyecto.Tests
{
    public class CorreoServiceTests
    {
        private static IConfiguration CrearConfiguracion(Dictionary<string, string?> valores)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(valores)
                .Build();
        }

        [Fact]
        public async Task EnviarCodigoAsync_SinHostEnDesarrollo_NoLanzaExcepcionYRegistraLog()
        {
           
            var config = CrearConfiguracion(new Dictionary<string, string?>
            {
                { "Correo:Host", "" }
            });
            var mockLogger = new Mock<ILogger<CorreoService>>();
            var mockEnv = new Mock<IHostEnvironment>();
            mockEnv.Setup(e => e.EnvironmentName).Returns(Environments.Development);
            var servicio = new CorreoService(config, mockLogger.Object, mockEnv.Object);
            var excepcion = await Record.ExceptionAsync(() =>
                servicio.EnviarCodigoAsync("usuario@ejemplo.com", "Alison", "123456", true));

            Assert.Null(excepcion);
        }

        [Fact]
        public async Task EnviarCodigoAsync_SinHostEnProduccion_LanzaInvalidOperationException()
        {
            var config = CrearConfiguracion(new Dictionary<string, string?>
            {
                { "Correo:Host", "" }
            });
            var mockLogger = new Mock<ILogger<CorreoService>>();
            var mockEnv = new Mock<IHostEnvironment>();
            mockEnv.Setup(e => e.EnvironmentName).Returns(Environments.Production);
            var servicio = new CorreoService(config, mockLogger.Object, mockEnv.Object);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                servicio.EnviarCodigoAsync("usuario@ejemplo.com", "Alison", "123456", true));

            Assert.Equal("Falta la configuración Correo:Host", ex.Message);
        }

        [Fact]
        public async Task EnviarCodigoAsync_ConHostPeroSinUsuario_LanzaInvalidOperationException()
        {
            var config = CrearConfiguracion(new Dictionary<string, string?>
            {
                { "Correo:Host", "smtp.gmail.com" },
                { "Correo:Usuario", "" },
                { "Correo:Clave", "clave123" }
            });
            var mockLogger = new Mock<ILogger<CorreoService>>();
            var mockEnv = new Mock<IHostEnvironment>();
            var servicio = new CorreoService(config, mockLogger.Object, mockEnv.Object);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                servicio.EnviarCodigoAsync("usuario@ejemplo.com", "Alison", "123456", false));

            Assert.Equal("Falta la configuración Correo:Usuario o Correo:Clave", ex.Message);
        }

        [Fact]
        public async Task EnviarCodigoAsync_ConHostYUsuarioPeroSinClave_LanzaInvalidOperationException()
        {
            var config = CrearConfiguracion(new Dictionary<string, string?>
            {
                { "Correo:Host", "smtp.gmail.com" },
                { "Correo:Usuario", "correo@raiza.com" },
                { "Correo:Clave", "" }
            });
            var mockLogger = new Mock<ILogger<CorreoService>>();
            var mockEnv = new Mock<IHostEnvironment>();
            var servicio = new CorreoService(config, mockLogger.Object, mockEnv.Object);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                servicio.EnviarCodigoAsync("usuario@ejemplo.com", "Alison", "123456", true));

            Assert.Equal("Falta la configuración Correo:Usuario o Correo:Clave", ex.Message);
        }
    }
}