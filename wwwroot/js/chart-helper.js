window.drawPackagePieChart = (labels, data) => {
    const ctx = document.getElementById('packagePieChart').getContext('2d');
    if (window.packageChart) window.packageChart.destroy();

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

    window.packageChart = new Chart(ctx, {
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
