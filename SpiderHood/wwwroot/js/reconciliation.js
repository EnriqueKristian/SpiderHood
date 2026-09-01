// Funciones JavaScript para la página de conciliación

// Descargar JSON con los datos de conciliación
window.descargarJSON = function (data, filename) {
    const jsonStr = JSON.stringify(data, null, 2);
    const blob = new Blob([jsonStr], { type: 'application/json' });
    const url = URL.createObjectURL(blob);

    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);

    URL.revokeObjectURL(url);
};

// Simular clic en input file
window.clickFileInput = function (elementId) {
    document.getElementById(elementId).click();
};

// Exportar a Excel (simplificado)
window.exportarExcel = function (data, filename) {
    // En una implementación real, usarías una librería como SheetJS
    console.log('Exportando a Excel:', data);
    alert('Función de exportación a Excel en desarrollo');
};

// Función para buscar coincidencias difusas
window.buscarCoincidenciasDifusas = function (transacciones, gastos, umbral) {
    // Implementación básica de búsqueda difusa
    const coincidencias = [];

    transacciones.forEach(transaccion => {
        gastos.forEach(gasto => {
            // Comparar montos (±umbral%)
            const diferenciaMonto = Math.abs(Math.abs(transaccion.monto) - gasto.monto);
            const porcentajeDiferencia = diferenciaMonto / Math.abs(transaccion.monto);

            if (porcentajeDiferencia <= umbral) {
                // También comparar descripciones (simplificado)
                const descTransaccion = transaccion.descripcion.toLowerCase();
                const descGasto = gasto.descripcion.toLowerCase();

                // Buscar palabras clave comunes
                const palabrasClave = ['cfe', 'agua', 'mantenimiento', 'limpieza', 'seguridad'];
                const tienePalabraClave = palabrasClave.some(palabra =>
                    descTransaccion.includes(palabra) && descGasto.includes(palabra)
                );

                if (tienePalabraClave) {
                    coincidencias.push({
                        transaccion: transaccion,
                        gasto: gasto,
                        confianza: 1 - porcentajeDiferencia
                    });
                }
            }
        });
    });

    return coincidencias;
};