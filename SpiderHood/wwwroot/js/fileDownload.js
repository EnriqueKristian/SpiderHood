// Funciones para descargar archivos desde Blazor

// Descarga genérica de cualquier archivo por su base64 + content type — usada por
// InstallmentTable.razor y InstallmentList.razor para descargar el PDF del recibo
// (antes vivía duplicada como script local en InstallmentTable.razor).
window.downloadFile = (base64String, fileName, contentType) => {
    const link = document.createElement('a');
    link.download = fileName;
    link.href = `data:${contentType};base64,${base64String}`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

// Abre un PDF (base64) en una pestaña nueva para VERLO (visor nativo del navegador),
// a diferencia de downloadFile (que fuerza la descarga vía <a download>). Usada por
// MyReceipts.razor ("Ver Detalle" carga el recibo PDF en vez de forzar su descarga).
window.openPdfInNewTab = (base64String, fileName) => {
    const byteCharacters = atob(base64String);
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
    }
    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray], { type: 'application/pdf' });
    const url = URL.createObjectURL(blob);
    const opened = window.open(url, '_blank');
    if (!opened) {
        // Bloqueado por el navegador (popup blocker) -- como fallback, al menos
        // ofrecer la descarga en vez de dejar el click sin ningún efecto.
        window.downloadFile(base64String, fileName, 'application/pdf');
    }
    setTimeout(() => URL.revokeObjectURL(url), 60000);
};

window.descargarArchivo = (filename, base64Data) => {
    const link = document.createElement('a');
    link.download = filename;
    link.href = `data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64,${base64Data}`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

window.saveAsFile = (filename, base64Data) => {
    const blob = base64ToBlob(base64Data);
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
};

function base64ToBlob(base64) {
    const byteCharacters = atob(base64);
    const byteNumbers = new Array(byteCharacters.length);

    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
    }

    const byteArray = new Uint8Array(byteNumbers);
    return new Blob([byteArray], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
}