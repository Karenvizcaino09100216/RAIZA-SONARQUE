using Microsoft.AspNetCore.Mvc;
using Moq;
using RAIZA.Controllers;
using RAIZA.Interfaces;
using RAIZA.Models;
using Xunit;

namespace MiProyecto.Tests
{
    public class AdministradorControllerTests
    {
        private readonly Mock<AdministradorI> _mockAdminRepo;
        private readonly Mock<IUsuarioI> _mockUsuarioRepo;
        private readonly AdministradorController _controller;

        public AdministradorControllerTests()
        {
            _mockAdminRepo = new Mock<AdministradorI>();
            _mockUsuarioRepo = new Mock<IUsuarioI>();
            _controller = new AdministradorController(_mockAdminRepo.Object, _mockUsuarioRepo.Object);
        }

        [Fact]
        public async Task GetAdministradores_RetornaOkConLista()
        {
            _mockAdminRepo.Setup(r => r.GetAdministradores())
                .ReturnsAsync(new List<Administrador> { new() { idadministrador = 1 } });

            var resultado = await _controller.GetAdministradores();

            Assert.IsType<OkObjectResult>(resultado);
        }

        [Fact]
        public async Task GetAdministradores_CuandoFallaRepo_Retorna500()
        {
            _mockAdminRepo.Setup(r => r.GetAdministradores())
                .ThrowsAsync(new Exception("Error de conexión"));

            var resultado = await _controller.GetAdministradores();

            var objectResult = Assert.IsType<ObjectResult>(resultado);
            Assert.Equal(500, objectResult.StatusCode);
        }

        [Fact]
        public async Task GetAdministradorById_CuandoNoExiste_RetornaNotFound()
        {
            _mockAdminRepo.Setup(r => r.GetAdministradorById(99))
                .ReturnsAsync((Administrador?)null);

            var resultado = await _controller.GetAdministradorById(99);

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task GetAdministradorById_CuandoExiste_RetornaOk()
        {
            _mockAdminRepo.Setup(r => r.GetAdministradorById(1))
                .ReturnsAsync(new Administrador { idadministrador = 1 });

            var resultado = await _controller.GetAdministradorById(1);

            Assert.IsType<OkObjectResult>(resultado);
        }

        [Fact]
        public async Task UpdateAdministrador_ObjetoNulo_RetornaBadRequest()
        {
            var resultado = await _controller.UpdateAdministrador(1, null!);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task UpdateAdministrador_IdNoCoincide_RetornaBadRequest()
        {
            var admin = new Administrador { idadministrador = 2 };

            var resultado = await _controller.UpdateAdministrador(1, admin);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task UpdateAdministrador_NoExisteEnDb_RetornaNotFound()
        {
            var admin = new Administrador { idadministrador = 1 };
            _mockAdminRepo.Setup(r => r.GetAdministradorById(1))
                .ReturnsAsync((Administrador?)null);

            var resultado = await _controller.UpdateAdministrador(1, admin);

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task UpdateAdministrador_RepoRetornaFalse_RetornaBadRequest()
        {
            var admin = new Administrador { idadministrador = 1 };
            _mockAdminRepo.Setup(r => r.GetAdministradorById(1)).ReturnsAsync(admin);
            _mockAdminRepo.Setup(r => r.UpdateAdministrador(admin)).ReturnsAsync(false);

            var resultado = await _controller.UpdateAdministrador(1, admin);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task UpdateAdministrador_Exitoso_RetornaOk()
        {
            var admin = new Administrador { idadministrador = 1 };
            _mockAdminRepo.Setup(r => r.GetAdministradorById(1)).ReturnsAsync(admin);
            _mockAdminRepo.Setup(r => r.UpdateAdministrador(admin)).ReturnsAsync(true);

            var resultado = await _controller.UpdateAdministrador(1, admin);

            Assert.IsType<OkObjectResult>(resultado);
        }

        [Fact]
        public async Task DeleteAdministrador_NoExiste_RetornaNotFound()
        {
            _mockAdminRepo.Setup(r => r.GetAdministradorById(1))
                .ReturnsAsync((Administrador?)null);

            var resultado = await _controller.DeleteAdministrador(1);

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task DeleteAdministrador_RepoRetornaFalse_RetornaBadRequest()
        {
            _mockAdminRepo.Setup(r => r.GetAdministradorById(1))
                .ReturnsAsync(new Administrador { idadministrador = 1 });
            _mockAdminRepo.Setup(r => r.DeleteAdministrador(1)).ReturnsAsync(false);

            var resultado = await _controller.DeleteAdministrador(1);

            Assert.IsType<BadRequestObjectResult>(resultado);
        }

        [Fact]
        public async Task DeleteAdministrador_Exitoso_RetornaOk()
        {
            _mockAdminRepo.Setup(r => r.GetAdministradorById(1))
                .ReturnsAsync(new Administrador { idadministrador = 1 });
            _mockAdminRepo.Setup(r => r.DeleteAdministrador(1)).ReturnsAsync(true);

            var resultado = await _controller.DeleteAdministrador(1);

            Assert.IsType<OkObjectResult>(resultado);
        }
    }
}
