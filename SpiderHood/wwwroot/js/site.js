
window.toggleDarkClass = (isDark) => {
    if (isDark) {
        document.body.classList.add('dark');
    } else {
        document.body.classList.remove('dark');
    }
};
