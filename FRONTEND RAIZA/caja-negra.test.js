/**
 * SUITE DE PRUEBAS DE CAJA NEGRA - PROYECTO RAIZA
 * Enfoque: Especificación funcional (Entradas -> Salidas esperadas)
 * Técnicas: Partición de Equivalencia (PE), Análisis de Valores Límite (AVL) y Tabla de Decisión
 */
const fs = require('fs');
const path = require('path');

describe('Pruebas Funcionales de Caja Negra - Sistema RAIZA', () => {
    beforeEach(() => {
        document.body.innerHTML = fs.readFileSync(path.join(__dirname, 'login.html'), 'utf8');
        localStorage.clear();
        jest.spyOn(console, 'error').mockImplementation(() => {});
        global.fetch = jest.fn();
        global.grecaptcha = { getResponse: jest.fn(() => 'captcha-ok'), reset: jest.fn() };
        jest.resetModules();
        Object.assign(window, require('./login.js'));
        document.dispatchEvent(new Event('DOMContentLoaded'));
    });

    test('CN-01 [Tabla de Decisión]: Login sin completar reCAPTCHA bloquea envío y alerta al usuario', async () => {
        global.grecaptcha.getResponse.mockReturnValue('');
        document.getElementById('email').value = 'estudiante@raiza.edu.co';
        document.getElementById('password').value = 'ClaveSegura2026';

        await window.enviarLogin({ preventDefault: jest.fn(), target: document.getElementById('formLogin') });

        const alerta = document.getElementById('alertaBox');
        expect(alerta.hidden).toBe(false);
        expect(alerta.textContent).toMatch(/no soy un robot/i);
    });

    test('CN-02 [Partición Equivalencia]: Credenciales válidas con correo sin verificar redirigen a verificación', async () => {
        global.fetch.mockResolvedValue({
            ok: false,
            status: 403,
            json: async () => ({ codigo: 'CORREO_NO_VERIFICADO', mensaje: 'Debes verificar tu correo.' })
        });
        document.getElementById('email').value = 'nuevo@raiza.edu.co';
        document.getElementById('password').value = 'ClaveSegura2026';

        await window.enviarLogin({ preventDefault: jest.fn(), target: document.getElementById('formLogin') });

        expect(document.getElementById('formVerificar').style.display).toBe('block');
        expect(document.getElementById('correoDestino').textContent).toBe('nuevo@raiza.edu.co');
    });

    test('CN-03 [Partición Equivalencia]: Credenciales válidas verificadas almacenan sesión JWT', async () => {
        global.fetch.mockResolvedValue({
            ok: true,
            status: 200,
            json: async () => ({ token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9', rol: 'Estudiante', nombre: 'Alison Sosa', idUsuario: 1 })
        });
        document.getElementById('email').value = 'alison@raiza.edu.co';
        document.getElementById('password').value = 'ClaveSegura2026';

        await window.enviarLogin({ preventDefault: jest.fn(), target: document.getElementById('formLogin') });

        expect(localStorage.getItem('raiza_jwt')).toBeTruthy();
        expect(localStorage.getItem('raiza_rol')).toBe('Estudiante');
    });

    test('CN-04 [Valores Límite - AVL]: Registro con contraseña de 7 caracteres (límite inferior inválido) es rechazado', async () => {
        document.getElementById('regNombre').value = 'Alison Sosa';
        document.getElementById('regEmail').value = 'alison@raiza.edu.co';
        document.getElementById('regPassword').value = '1234567'; // 7 caracteres
        document.getElementById('regConsentimiento').checked = true;

        await window.enviarRegistro({ preventDefault: jest.fn(), target: document.getElementById('formRegistro') });

        expect(global.fetch).not.toHaveBeenCalled();
        expect(document.getElementById('alertaBox').textContent).toMatch(/8 caracteres/i);
    });

    test('CN-05 [Valores Límite - AVL]: Registro con contraseña de 8 caracteres (límite válido) sin consentimiento es rechazado', async () => {
        document.getElementById('regNombre').value = 'Alison Sosa';
        document.getElementById('regEmail').value = 'alison@raiza.edu.co';
        document.getElementById('regPassword').value = 'Clave123'; // 8 caracteres exactos
        document.getElementById('regConsentimiento').checked = false;

        await window.enviarRegistro({ preventDefault: jest.fn(), target: document.getElementById('formRegistro') });

        expect(global.fetch).not.toHaveBeenCalled();
        expect(document.getElementById('alertaBox').textContent).toMatch(/tratamiento/i);
    });

    test('CN-06 [Partición Equivalencia]: Registro válido completo avanza a pantalla de verificación de código', async () => {
        global.fetch.mockResolvedValue({
            ok: true,
            status: 200,
            json: async () => ({ mensaje: 'Código de verificación enviado al correo.' })
        });
        document.getElementById('regNombre').value = 'Alison Sosa';
        document.getElementById('regEmail').value = 'alison@raiza.edu.co';
        document.getElementById('regPassword').value = 'Clave2026';
        document.getElementById('regConsentimiento').checked = true;

        await window.enviarRegistro({ preventDefault: jest.fn(), target: document.getElementById('formRegistro') });

        expect(document.getElementById('formVerificar').style.display).toBe('block');
    });

    test('CN-07 [Partición Equivalencia]: Código de verificación inválido muestra mensaje de error del servidor', async () => {
        global.fetch.mockResolvedValue({
            ok: false,
            status: 400,
            json: async () => ({ mensaje: 'El código ingresado es incorrecto o ha expirado.' })
        });
        document.getElementById('codigoVerificacion').value = '000000';

        await window.enviarVerificacion({ preventDefault: jest.fn(), target: document.getElementById('formVerificar') });

        expect(document.getElementById('alertaBox').textContent).toBe('El código ingresado es incorrecto o ha expirado.');
    });

    test('CN-08 [Flujo E2E Recuperación]: Solicitud y restablecimiento de contraseña completan ciclo hacia Login', async () => {
        global.fetch.mockResolvedValue({
            ok: true,
            status: 200,
            json: async () => ({ mensaje: 'Contraseña actualizada correctamente.' })
        });
        document.getElementById('emailRecuperar').value = 'alison@raiza.edu.co';
        await window.enviarSolicitudRecuperacion({ preventDefault: jest.fn(), target: document.getElementById('formRecuperar') });
        expect(document.getElementById('formRestablecer').style.display).toBe('block');

        document.getElementById('codigoRecuperacion').value = '987654';
        document.getElementById('nuevaPassword').value = 'NuevaClave2026';
        await window.enviarRestablecer({ preventDefault: jest.fn(), target: document.getElementById('formRestablecer') });
        expect(document.getElementById('formLogin').style.display).toBe('block');
    });
});



