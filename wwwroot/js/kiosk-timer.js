window.startKioskTimer = (dotnetRef, interval) => {
    if (!dotnetRef) return;

    // Store interval ID so we can stop it later if needed
    if (!window._kioskIntervals) window._kioskIntervals = [];
    
    const id = setInterval(() => {
        dotnetRef.invokeMethodAsync('UpdateFromJsTimer')
            .catch(err => console.error('Kiosk timer error:', err));
    }, interval);

    window._kioskIntervals.push({ ref: dotnetRef, id: id });
};

window.stopKioskTimer = (dotnetRef) => {
    if (!window._kioskIntervals) return;
    
    window._kioskIntervals
        .filter(x => x.ref === dotnetRef)
        .forEach(x => clearInterval(x.id));

    window._kioskIntervals = window._kioskIntervals.filter(x => x.ref !== dotnetRef);
};
