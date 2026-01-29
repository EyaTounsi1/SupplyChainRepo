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

window.drawPremiumTrendsChart = (canvasId, labels, costData, countData) => {
    const ctx = document.getElementById(canvasId).getContext('2d');
    if (window[canvasId + 'Chart']) window[canvasId + 'Chart'].destroy();

    window[canvasId + 'Chart'] = new Chart(ctx, {
        type: 'line',
        data: {
            labels,
            datasets: [
                {
                    label: 'Total Cost (EUR)',
                    data: costData,
                    borderColor: '#BF616A',
                    backgroundColor: 'rgba(191, 97, 106, 0.1)',
                    borderWidth: 2,
                    tension: 0.4,
                    yAxisID: 'y-cost',
                    fill: true
                },
                {
                    label: 'Number of Bookings',
                    data: countData,
                    borderColor: '#5E81AC',
                    backgroundColor: 'rgba(94, 129, 172, 0.1)',
                    borderWidth: 2,
                    tension: 0.4,
                    yAxisID: 'y-count',
                    fill: true
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: {
                mode: 'index',
                intersect: false
            },
            plugins: {
                legend: {
                    position: 'top',
                    labels: { boxWidth: 20, padding: 15 }
                },
                tooltip: {
                    callbacks: {
                        label: (context) => {
                            let label = context.dataset.label || '';
                            if (label) label += ': ';
                            if (context.dataset.yAxisID === 'y-cost') {
                                label += '€' + context.parsed.y.toFixed(2);
                            } else {
                                label += context.parsed.y;
                            }
                            return label;
                        }
                    }
                }
            },
            scales: {
                'y-cost': {
                    type: 'linear',
                    position: 'left',
                    beginAtZero: true,
                    ticks: {
                        callback: (value) => '€' + value.toFixed(0)
                    },
                    grid: {
                        drawOnChartArea: true
                    }
                },
                'y-count': {
                    type: 'linear',
                    position: 'right',
                    beginAtZero: true,
                    ticks: {
                        stepSize: 1
                    },
                    grid: {
                        drawOnChartArea: false
                    }
                },
                x: {
                    grid: {
                        display: false
                    }
                }
            }
        }
    });
};

window.drawReasonCodeTrendsChart = (canvasId, labels, reasonCodeData) => {
    const ctx = document.getElementById(canvasId).getContext('2d');
    if (window[canvasId + 'Chart']) window[canvasId + 'Chart'].destroy();

    // Colors for different reason codes
    const colors = [
        '#5E81AC', // Nord blue
        '#BF616A', // Nord red
        '#A3BE8C', // Nord green
        '#EBCB8B', // Nord yellow
        '#B48EAD', // Nord purple
        '#88C0D0', // Nord cyan
        '#D08770', // Nord orange
        '#8FBCBB', // Nord teal
    ];

    // Convert the dictionary to datasets
    const datasets = [];
    let colorIndex = 0;
    for (const [reasonCode, data] of Object.entries(reasonCodeData)) {
        const color = colors[colorIndex % colors.length];
        datasets.push({
            label: reasonCode,
            data: data,
            borderColor: color,
            backgroundColor: color.replace(')', ', 0.1)').replace('rgb', 'rgba'),
            borderWidth: 2,
            tension: 0.4,
            fill: false
        });
        colorIndex++;
    }

    window[canvasId + 'Chart'] = new Chart(ctx, {
        type: 'line',
        data: {
            labels,
            datasets: datasets
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: {
                mode: 'index',
                intersect: false
            },
            plugins: {
                legend: {
                    position: 'top',
                    labels: { boxWidth: 20, padding: 10 }
                },
                tooltip: {
                    callbacks: {
                        label: (context) => {
                            let label = context.dataset.label || '';
                            if (label) label += ': ';
                            label += context.parsed.y;
                            return label;
                        }
                    }
                }
            },
            scales: {
                y: {
                    type: 'linear',
                    position: 'left',
                    beginAtZero: true,
                    ticks: {
                        stepSize: 1
                    }
                },
                x: {
                    grid: {
                        display: false
                    }
                }
            }
        }
    });
};

window.drawReasonCodePieChart = (canvasId, labels, data) => {
    const ctx = document.getElementById(canvasId).getContext('2d');
    if (window[canvasId + 'Chart']) window[canvasId + 'Chart'].destroy();

    // Nord color palette
    const baseColors = [
        '#5E81AC', // Nord blue
        '#BF616A', // Nord red
        '#A3BE8C', // Nord green
        '#EBCB8B', // Nord yellow
        '#B48EAD', // Nord purple
        '#88C0D0', // Nord cyan
        '#D08770', // Nord orange
        '#8FBCBB', // Nord teal
        '#81A1C1', // Nord light blue
        '#4C566A', // Nord dark gray
    ];

    const colors = labels.map((_, i) => baseColors[i % baseColors.length]);
    const borderColors = colors.map(() => '#ffffff');

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
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'right',
                    labels: {
                        boxWidth: 20,
                        padding: 15,
                        font: {
                            size: 12
                        }
                    }
                },
                tooltip: {
                    callbacks: {
                        label: (context) => {
                            const label = context.label || '';
                            const value = context.raw || 0;
                            const total = context.dataset.data.reduce((a, b) => a + b, 0);
                            const percentage = ((value / total) * 100).toFixed(1);
                            return `${label}: ${value} (${percentage}%)`;
                        }
                    }
                }
            }
        }
    });
};

window.drawSpeedUpCostChart = (canvasId, labels, data2025, data2026) => {
    const ctx = document.getElementById(canvasId).getContext('2d');
    if (window[canvasId + 'Chart']) window[canvasId + 'Chart'].destroy();

    window[canvasId + 'Chart'] = new Chart(ctx, {
        type: 'bar',
        data: {
            labels,
            datasets: [
                {
                    label: '2025 Costs',
                    data: data2025,
                    backgroundColor: '#5E81AC',
                    borderColor: '#4C566A',
                    borderWidth: 1
                },
                {
                    label: '2026 Costs',
                    data: data2026,
                    backgroundColor: '#88C0D0',
                    borderColor: '#4C566A',
                    borderWidth: 1
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: {
                mode: 'index',
                intersect: false
            },
            plugins: {
                legend: {
                    position: 'top',
                    labels: { boxWidth: 20, padding: 15 }
                },
                tooltip: {
                    callbacks: {
                        label: (context) => {
                            let label = context.dataset.label || '';
                            if (label) label += ': ';
                            label += '€' + context.parsed.y.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
                            return label;
                        }
                    }
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        callback: (value) => '€' + value.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 0 })
                    }
                },
                x: {
                    grid: {
                        display: false
                    }
                }
            }
        }
    });
};

window.drawPremiumCostChart = (canvasId, labels, costData2025, costData2026) => {
    const ctx = document.getElementById(canvasId).getContext('2d');
    if (window[canvasId + 'Chart']) window[canvasId + 'Chart'].destroy();

        window[canvasId + 'Chart'] = new Chart(ctx, {
            type: 'line',
            data: {
                labels,
                datasets: [
                    {
                        label: '2025 Cost (SEK)',
                        data: costData2025,
                        borderColor: '#A3BE8C',
                        backgroundColor: 'rgba(163, 190, 140, 0.1)',
                        borderWidth: 2,
                        tension: 0.4,
                        fill: true
                    },
                    {
                        label: '2026 Cost (SEK)',
                        data: costData2026,
                        borderColor: '#5E81AC',
                        backgroundColor: 'rgba(94, 129, 172, 0.1)',
                        borderWidth: 2,
                        tension: 0.4,
                        fill: true
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                plugins: {
                    legend: {
                        position: 'top',
                    labels: { boxWidth: 20, padding: 15 }
                },
                tooltip: {
                    callbacks: {
                        label: (context) => {
                            let label = context.dataset.label || '';
                            if (label) label += ': ';
                            label += context.parsed.y.toLocaleString('sv-SE', { minimumFractionDigits: 0, maximumFractionDigits: 0 }) + ' SEK';
                            return label;
                        }
                    }
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        callback: (value) => value.toLocaleString('sv-SE', { minimumFractionDigits: 0, maximumFractionDigits: 0 }) + ' SEK'
                    }
                },
                x: {
                    grid: {
                        display: false
                    }
                }
            }
        }
    });
};


