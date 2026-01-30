window.clickElement = function (elementId) {
    document.getElementById(elementId).click();
}

// wwwroot/js/budget.js

// Funciones para diálogos de confirmación
window.showConfirmation = async function (title, message, confirmText, cancelText) {
    return new Promise((resolve) => {
        // Usar SweetAlert si está disponible
        if (typeof Swal !== 'undefined') {
            Swal.fire({
                title: title,
                html: message,
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: confirmText,
                cancelButtonText: cancelText,
                confirmButtonColor: '#3085d6',
                cancelButtonColor: '#d33',
                reverseButtons: true
            }).then((result) => {
                resolve(result.isConfirmed);
            });
        } else {
            // Fallback a confirm nativo
            resolve(confirm(`${title}\n\n${message}\n\n¿${confirmText}?`));
        }
    });
};

// Funciones para loading/spinner
window.showLoading = function (message) {
    // Crear overlay de carga
    const overlay = document.createElement('div');
    overlay.id = 'loading-overlay';
    overlay.style.cssText = `
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        background: rgba(0, 0, 0, 0.7);
        display: flex;
        justify-content: center;
        align-items: center;
        z-index: 9999;
        flex-direction: column;
    `;

    const spinner = document.createElement('div');
    spinner.style.cssText = `
        border: 5px solid #f3f3f3;
        border-top: 5px solid #3498db;
        border-radius: 50%;
        width: 50px;
        height: 50px;
        animation: spin 1s linear infinite;
    `;

    const text = document.createElement('div');
    text.textContent = message || 'Cargando...';
    text.style.cssText = `
        color: white;
        margin-top: 20px;
        font-size: 16px;
    `;

    // Agregar animación CSS si no existe
    if (!document.querySelector('#loading-spin-animation')) {
        const style = document.createElement('style');
        style.id = 'loading-spin-animation';
        style.textContent = `
            @keyframes spin {
                0% { transform: rotate(0deg); }
                100% { transform: rotate(360deg); }
            }
        `;
        document.head.appendChild(style);
    }

    overlay.appendChild(spinner);
    overlay.appendChild(text);
    document.body.appendChild(overlay);
};

window.hideLoading = function () {
    const overlay = document.getElementById('loading-overlay');
    if (overlay) {
        overlay.remove();
    }
};

// Función para mostrar toast/notificaciones
window.showToast = function (title, message, type, duration = 5000) {
    // Verificar si ya existe una librería de toast
    if (typeof toastr !== 'undefined') {
        const toastType = type === 'success' ? 'success' :
            type === 'error' ? 'error' :
                type === 'warning' ? 'warning' : 'info';

        toastr.options = {
            closeButton: true,
            positionClass: 'toast-top-right',
            timeOut: duration,
            extendedTimeOut: 1000,
            preventDuplicates: true
        };

        toastr[toastType](message, title);
        return;
    }

    if (typeof Swal !== 'undefined') {
        const icon = type === 'success' ? 'success' :
            type === 'error' ? 'error' :
                type === 'warning' ? 'warning' : 'info';

        Swal.fire({
            title: title,
            text: message,
            icon: icon,
            timer: duration,
            showConfirmButton: false,
            toast: true,
            position: 'top-end'
        });
        return;
    }

    // Implementación nativa
    const toast = document.createElement('div');
    const toastId = 'toast-' + Date.now();

    const typeClasses = {
        'success': 'bg-success text-white',
        'error': 'bg-danger text-white',
        'warning': 'bg-warning text-dark',
        'info': 'bg-info text-white'
    };

    toast.id = toastId;
    toast.className = `toast-alert position-fixed top-0 end-0 p-3 ${typeClasses[type] || 'bg-info'}`;
    toast.style.cssText = `
        z-index: 99999;
        min-width: 300px;
        border-radius: 4px;
        margin-top: 20px;
        animation: slideIn 0.3s ease-out;
    `;

    toast.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">
                <strong>${title}</strong><br>
                ${message}
            </div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" 
                    onclick="document.getElementById('${toastId}').remove()"></button>
        </div>
    `;

    document.body.appendChild(toast);

    // Auto-remove después de la duración
    setTimeout(() => {
        const element = document.getElementById(toastId);
        if (element) {
            element.style.animation = 'slideOut 0.3s ease-out';
            setTimeout(() => element.remove(), 300);
        }
    }, duration);

    // Agregar animaciones CSS si no existen
    if (!document.querySelector('#toast-animations')) {
        const style = document.createElement('style');
        style.id = 'toast-animations';
        style.textContent = `
            @keyframes slideIn {
                from { transform: translateX(100%); opacity: 0; }
                to { transform: translateX(0); opacity: 1; }
            }
            @keyframes slideOut {
                from { transform: translateX(0); opacity: 1; }
                to { transform: translateX(100%); opacity: 0; }
            }
        `;
        document.head.appendChild(style);
    }
};