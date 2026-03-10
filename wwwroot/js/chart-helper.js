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

// Stacked bar chart for GIT coverage split
window.drawGitCoverageStackedBarChart = (canvasId, labels, onHandData, gitData) => {
    const ctx = document.getElementById(canvasId).getContext('2d');
    if (window[canvasId + 'Chart']) window[canvasId + 'Chart'].destroy();

    window[canvasId + 'Chart'] = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [
                {
                    label: 'On-Hand Coverage',
                    data: onHandData,
                    backgroundColor: '#88C0D0',
                    borderColor: '#5E81AC',
                    borderWidth: 1
                },
                {
                    label: 'GIT Dependency',
                    data: gitData,
                    backgroundColor: '#EBCB8B',
                    borderColor: '#D08770',
                    borderWidth: 1
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'top',
                    labels: { boxWidth: 15, padding: 10 }
                },
                tooltip: {
                    callbacks: {
                        label: (context) => `${context.dataset.label}: ${context.raw.toFixed(1)} shifts`
                    }
                }
            },
            scales: {
                x: {
                    stacked: true,
                    grid: { display: false },
                    ticks: {
                        maxRotation: 45,
                        minRotation: 45
                    }
                },
                y: {
                    stacked: true,
                    beginAtZero: true,
                    title: {
                        display: true,
                        text: 'Coverage (Shifts)'
                    }
                }
            }
        }
    });
};

// Scatter chart for on-hand shifts vs GIT dependency
window.drawGitDependencyScatterChart = (canvasId, scatterData) => {
    const ctx = document.getElementById(canvasId).getContext('2d');
    if (window[canvasId + 'Chart']) window[canvasId + 'Chart'].destroy();

    // Determine risk zones for coloring
    const dataWithColors = scatterData.map(point => {
        let color;
        if (point.x < 10 && point.y > 20) {
            color = '#BF616A'; // High risk: low on-hand, high GIT dependency
        } else if (point.x < 10) {
            color = '#D08770'; // Medium risk: low on-hand
        } else if (point.y > 20) {
            color = '#EBCB8B'; // Medium risk: high GIT dependency
        } else {
            color = '#A3BE8C'; // Low risk
        }
        return {
            x: point.x,
            y: point.y,
            backgroundColor: color
        };
    });

    window[canvasId + 'Chart'] = new Chart(ctx, {
        type: 'scatter',
        data: {
            datasets: [{
                label: 'Parts',
                data: dataWithColors,
                backgroundColor: dataWithColors.map(d => d.backgroundColor),
                borderColor: '#2E3440',
                borderWidth: 1,
                pointRadius: 5,
                pointHoverRadius: 7
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    callbacks: {
                        label: (context) => [
                            `On-Hand: ${context.raw.x.toFixed(1)} shifts`,
                            `GIT Dependency: ${context.raw.y.toFixed(1)} shifts`
                        ]
                    }
                }
            },
            scales: {
                x: {
                    title: {
                        display: true,
                        text: 'On-Hand Coverage (Shifts)'
                    },
                    beginAtZero: true
                },
                y: {
                    title: {
                        display: true,
                        text: 'GIT Dependency (Shifts)'
                    },
                    beginAtZero: true
                }
            }
        }
    });
};

// Volatility by Planning Point chart (ranked bar chart)
window.drawVolatilityByPlanningPointChart = (canvasId, labels, medianData, avgData) => {
    const ctx = document.getElementById(canvasId).getContext('2d');
    if (window[canvasId + 'Chart']) window[canvasId + 'Chart'].destroy();

    window[canvasId + 'Chart'] = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [
                {
                    label: 'Median Daily Change',
                    data: medianData,
                    backgroundColor: '#D08770',
                    borderColor: '#BF616A',
                    borderWidth: 1
                },
                {
                    label: 'Avg Daily Change',
                    data: avgData,
                    backgroundColor: '#EBCB8B',
                    borderColor: '#D08770',
                    borderWidth: 1
                }
            ]
        },
        options: {
            indexAxis: 'y',
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'top',
                    labels: { boxWidth: 15, padding: 10 }
                },
                tooltip: {
                    callbacks: {
                        label: (context) => `${context.dataset.label}: ${context.raw.toFixed(2)} shifts/day`
                    }
                }
            },
            scales: {
                x: {
                    beginAtZero: true,
                    title: {
                        display: true,
                        text: 'Absolute Daily Shift Change'
                    }
                },
                y: {
                    grid: { display: false }
                }
            }
        }
    });
};

// Volatility Trend chart (line chart over time)
window.drawVolatilityTrendChart = (canvasId, weekLabels, trendData) => {
    const ctx = document.getElementById(canvasId).getContext('2d');
    if (window[canvasId + 'Chart']) window[canvasId + 'Chart'].destroy();

    window[canvasId + 'Chart'] = new Chart(ctx, {
        type: 'line',
        data: {
            labels: weekLabels,
            datasets: [{
                label: 'Median Volatility',
                data: trendData,
                backgroundColor: 'rgba(191, 97, 106, 0.2)',
                borderColor: '#BF616A',
                borderWidth: 2,
                fill: true,
                tension: 0.4,
                pointRadius: 4,
                pointBackgroundColor: '#BF616A',
                pointBorderColor: '#FFFFFF',
                pointBorderWidth: 2
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    callbacks: {
                        label: (context) => `Volatility: ${context.raw.toFixed(2)} shifts`
                    }
                }
            },
            scales: {
                x: {
                    grid: { display: false },
                    ticks: {
                        maxRotation: 45,
                        minRotation: 45
                    }
                },
                y: {
                    beginAtZero: true,
                    title: {
                        display: true,
                        text: 'Median Volatility (Shifts)'
                    }
                }
            }
        }
    });
};
