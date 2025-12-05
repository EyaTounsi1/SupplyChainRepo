window.drawPackagePieChart = (canvasId, labels, data) => {
    const ctx = document.getElementById(canvasId).getContext('2d');
    if (window[canvasId + 'Chart']) window[canvasId + 'Chart'].destroy();

    // Build a contrasted Nord-based palette with tints and aurora accents.
    const baseColors = [
        '#5E81AC',
        '#81A1C1',
        '#4C566A',
        '#88C0D0',
        '#8FBCBB',
        '#A3BE8C',
        '#BF616A',
        '#D08770',
        '#EBCB8B',
        '#B48EAD',
        '#2E3440',
        '#D8DEE9' 
    ];

    // When more segments than palette, rotate.
    const colors = labels.map((_, i) => baseColors[i % baseColors.length]);
    const borderColors = colors.map(c => '#ffffff');

    window[canvasId + 'Chart'] = new Chart(ctx, {
        type: 'pie',
        data: {
            labels,
            datasets: [{
                data,
                backgroundColor: colors,
                borderColor: borderColors,
                borderWidth: 2,
                hoverOffset: 6,
                spacing: 2
            }]
        },
        options: {
            responsive: true,
            plugins: {
                legend: { position: 'bottom', labels: { boxWidth: 18, padding: 14 } },
                tooltip: {
                    callbacks: {
                        label: (context) => `${context.label}: ${context.raw}%`
                    }
                }
            }
        }
    });
};

window.drawCostHistogram = (labels, data) => {
    const ctx = document.getElementById('costHistogram').getContext('2d');
    if (window.costChart) window.costChart.destroy();

    window.costChart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels,
            datasets: [{
                label: 'Daily Cost',
                data,
                backgroundColor: '#5E81AC',
                borderColor: '#4C566A',
                borderWidth: 1
            }]
        },
        options: {
            responsive: true,
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        callback: (value) => `$${value.toFixed(2)}`
                    }
                }
            },
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label: (context) => `Cost: $${context.raw.toFixed(2)}`
                    }
                }
            }
        }
    });
};
