// Simple interaction effect helpers for Nord-themed UI feedback.
// Row flash: temporarily add a class that triggers CSS animation.
window.nordFlashRow = (rowId) => {
    if(!rowId) return;
    const el = document.getElementById(rowId);
    if(!el) return;
    el.classList.add('nord-row-flash');
    // Remove after animation (~900ms) to allow re-trigger.
    setTimeout(() => el.classList.remove('nord-row-flash'), 1000);
};

// Button pulse effect.
window.nordPulseButton = (btnId) => {
    if(!btnId) return;
    const el = document.getElementById(btnId);
    if(!el) return;
    el.classList.add('nord-btn-pulse');
    setTimeout(() => el.classList.remove('nord-btn-pulse'), 1200);
};

// Generic interaction hook for future needs.
window.nordInteract = (id, cls, duration=800) => {
    if(!id || !cls) return;
    const el = document.getElementById(id);
    if(!el) return;
    el.classList.add(cls);
    setTimeout(() => el.classList.remove(cls), duration);
};
