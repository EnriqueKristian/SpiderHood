// Funciones para descargar archivos desde Blazor

// Descarga genérica de cualquier archivo por su base64 + content type — usada por
// InstallmentTable.razor y ListadoCuotas.razor para descargar el PDF del recibo
// (antes vivía duplicada como script local en InstallmentTable.razor).
window.downloadFile = (base64String, fileName, contentType) => {
    const link = document.createElement('a');
    link.download = fileName;
    link.href = `data:${contentType};base64,${base64String}`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
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