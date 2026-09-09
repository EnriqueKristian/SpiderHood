
// Gráficos para pantallas de Reportes. Chart.js ya se carga globalmente desde
// App.razor, pero hasta ahora nunca se usaba de verdad (el Dashboard sólo tenía
// un placeholder) -- este es el primer uso real, empezando por el reporte de
// Morosidad (Components/Pages/ReportPages/DelinquencyReport.razor).
window.spiderHoodReportCharts = (function () {
    var instances = {};

    return {
        // Reemplaza el gráfico anterior en el mismo canvas si ya existía (evita
        // acumular instancias de Chart.js al recargar el reporte con otro filtro).
        horizontalBar: function (canvasId, labels, data, datasetLabel) {
            var canvas = document.getElementById(canvasId);
            if (!canvas) return;

            if (instances[canvasId]) {
                instances[canvasId].destroy();
            }

            instances[canvasId] = new Chart(canvas.getContext('2d'), {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [{
                        label: datasetLabel,
                        data: data,
                        backgroundColor: 'rgba(220, 53, 69, 0.55)',
                        borderColor: 'rgba(220, 53, 69, 1)',
                        borderWidth: 1
                    }]
                },
                options: {
                    indexAxis: 'y',
                    responsive: true,
                    maintainAspectRatio: false,
                    // Sin el plugin de datalabels a propósito: App.razor lo carga pero
                    // nunca se llegó a registrar/probar (Chart.register(ChartDataLabels))
                    // en ningún gráfico real de la app -- más seguro apoyarse en el
                    // tooltip nativo de Chart.js, que no depende de eso.
                    plugins: { legend: { display: false } },
                    scales: { x: { beginAtZero: true } }
                }
            });
        },

        destroy: function (canvasId) {
            if (instances[canvasId]) {
                instances[canvasId].destroy();
                delete instances[canvasId];
            }
        }
    };
})();
