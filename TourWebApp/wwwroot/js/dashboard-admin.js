
"use strict";

document.addEventListener("DOMContentLoaded", function () {
    const root = document.querySelector(".htv3-dashboard");
    if (!root) return;

    const COLORS = {
        green: "#24935B",
        greenDark: "#176B48",
        yellow: "#F2B83E",
        white: "#FFFFFF",
        muted: "#748879",
        grid: "#EAF0E9",
        tooltip: "#173F30"
    };

    const PALETTE = [
        "#24935B",
        "#F2C14E",
        "#F39163",
        "#63B7CE",
        "#9CCB75",
        "#DB7980",
        "#9386BE",
        "#C79D70"
    ];

    const numberFormatter = new Intl.NumberFormat("vi-VN", {
        maximumFractionDigits: 0
    });

    const percentageFormatter = new Intl.NumberFormat("vi-VN", {
        minimumFractionDigits: 1,
        maximumFractionDigits: 1
    });

    const reducedMotion = window.matchMedia(
        "(prefers-reduced-motion: reduce)"
    ).matches;

    function toNumber(value) {
        const number = Number(value);
        return Number.isFinite(number) ? number : 0;
    }

    function positive(value) {
        return Math.max(0, toNumber(value));
    }

    function asArray(value) {
        return Array.isArray(value) ? value : [];
    }

    function asText(value, fallback = "") {
        if (value === null || value === undefined) {
            return fallback;
        }

        return String(value).trim() || fallback;
    }

    function numberText(value) {
        return numberFormatter.format(toNumber(value));
    }

    function moneyText(value) {
        return numberText(value) + " đ";
    }

    function shortMoney(value) {
        const amount = toNumber(value);
        const absolute = Math.abs(amount);

        if (absolute >= 1000000000) {
            return (amount / 1000000000).toLocaleString("vi-VN", {
                maximumFractionDigits: 1
            }) + " tỷ";
        }

        if (absolute >= 1000000) {
            return (amount / 1000000).toLocaleString("vi-VN", {
                maximumFractionDigits: 1
            }) + " triệu";
        }

        if (absolute >= 1000) {
            return (amount / 1000).toLocaleString("vi-VN", {
                maximumFractionDigits: 0
            }) + " nghìn";
        }

        return numberText(amount);
    }

    function make(tag, className = "", content = null) {
        const element = document.createElement(tag);

        if (className) {
            element.className = className;
        }

        if (content !== null && content !== undefined) {
            element.textContent = String(content);
        }

        return element;
    }

    function makeIcon(className) {
        const icon = make("i");
        icon.className = className;
        icon.setAttribute("aria-hidden", "true");
        return icon;
    }

    function emptyState(message, includeLink = false) {
        const box = make("div", "htv3-no-data");

        box.style.display = "flex";
        box.style.flexDirection = "column";
        box.style.alignItems = "center";
        box.style.justifyContent = "center";
        box.style.gap = "10px";
        box.style.padding = "18px";

        const graphic = makeIcon(
            "fa-solid fa-chart-simple"
        );

        graphic.style.color = "#94BDA1";
        graphic.style.fontSize = "25px";

        box.append(
            graphic,
            make("span", "", message)
        );

        if (includeLink) {
            const link = make(
                "a",
                "",
                "Xem quản lý lịch khởi hành"
            );

            link.href = "/QuanTri/DanhSachLich";
            link.style.color = COLORS.green;
            link.style.fontSize = "11px";
            link.style.fontWeight = "800";

            box.appendChild(link);
        }

        return box;
    }

    function safeImageUrl(value) {
        const path = asText(value);

        if (path.startsWith("/") && !path.startsWith("//")) {
            return path;
        }

        if (/^https:\/\//i.test(path)) {
            return path;
        }

        return "/images/default-tour.jpg";
    }

    function makeImage(source, description) {
        const image = make("img");

        image.src = safeImageUrl(source);
        image.alt = description || "Ảnh du lịch";
        image.loading = "lazy";
        image.decoding = "async";

        image.onerror = function () {
            image.onerror = null;
            image.src = "/images/default-tour.jpg";
        };

        return image;
    }

    function addNote(container, message) {
        if (!container) return;

        let note = container.querySelector(".htv3-insight-note");

        if (!note) {
            note = make("p", "htv3-insight-note");
            container.appendChild(note);
        }

        note.textContent = message;
        note.style.margin = "11px 0 0";
        note.style.color = COLORS.muted;
        note.style.fontSize = "10px";
        note.style.lineHeight = "1.6";
    }

    function showChartEmpty(canvas, message) {
        if (!canvas) return;

        const wrapper = canvas.parentElement;
        if (!wrapper) return;

        canvas.style.display = "none";

        const existing = wrapper.querySelector(
            ".htv3-chart-empty"
        );

        if (existing) {
            existing.remove();
        }

        const empty = emptyState(message);
        empty.classList.add("htv3-chart-empty");

        empty.style.minHeight = "220px";
        empty.style.height = "100%";
        empty.style.background = "#FAFCF9";
        empty.style.border = "1px dashed #DDE9DF";

        wrapper.appendChild(empty);
    }

    function initDatePicker() {
        const wrapper = root.querySelector(
            ".htv3-date-wrapper"
        );

        if (!wrapper) return;

        const oldInput = wrapper.querySelector(
            "#singleDatePicker"
        );

        const existingNative = wrapper.querySelector(
            "#htv3NativeDate"
        );

        const selectedDate = asText(
            window.selectedDate ||
            existingNative?.value ||
            oldInput?.dataset.date
        );

        const validDate = /^\d{4}-\d{2}-\d{2}$/.test(
            selectedDate
        );

        if (oldInput?._flatpickr) {
            oldInput._flatpickr.destroy();
        }

        if (oldInput) {
            oldInput.disabled = true;
            oldInput.style.display = "none";
        }

        let label = wrapper.querySelector("#htv3DateText");

        if (!label) {
            label = make("span");
            label.id = "htv3DateText";

            const arrow = wrapper.querySelector(
                ".fa-chevron-down"
            );

            if (arrow) {
                wrapper.insertBefore(label, arrow);
            } else {
                wrapper.appendChild(label);
            }
        }

        label.style.color = "#294337";
        label.style.fontWeight = "800";
        label.style.fontSize = "12px";
        label.style.whiteSpace = "nowrap";
        label.style.pointerEvents = "none";

        function displayDate(iso) {
            if (!/^\d{4}-\d{2}-\d{2}$/.test(iso)) {
                return "Chọn ngày";
            }

            const [year, month, day] = iso.split("-");
            return `${day}/${month}/${year}`;
        }

        label.textContent = displayDate(selectedDate);

        const input = existingNative ||
            document.createElement("input");

        input.id = "htv3NativeDate";
        input.type = "date";

        input.setAttribute(
            "aria-label",
            "Chọn ngày thống kê HappyTrip"
        );

        if (validDate) {
            input.value = selectedDate;
        }

        input.removeAttribute("min");
        input.removeAttribute("max");

        wrapper.style.position = "relative";
        wrapper.style.zIndex = "30";
        wrapper.style.cursor = "pointer";

        Object.assign(input.style, {
            position: "absolute",
            inset: "0",
            width: "100%",
            height: "100%",
            opacity: "0",
            zIndex: "10",
            border: "0",
            cursor: "pointer"
        });

        if (!existingNative) {
            wrapper.appendChild(input);
        }

        function getActivePeriod() {
            const active = root.querySelector(
                ".htv3-period-filter a.active"
            );

            if (active) {
                const url = new URL(
                    active.href,
                    window.location.origin
                );

                const value = url.searchParams.get("period");

                if (["day", "week", "month", "quarter", "year"]
                    .includes(value)) {
                    return value;
                }
            }

            const params = new URLSearchParams(
                window.location.search
            );

            const value = params.get("period");

            return ["day", "week", "month", "quarter", "year"]
                .includes(value) ? value : "month";
        }

        function openCalendar() {
            if (typeof input.showPicker === "function") {
                try {
                    input.showPicker();
                    return;
                } catch (_) {
                    input.focus();
                }
            } else {
                input.focus();
            }
        }

        wrapper.addEventListener("click", function (event) {
            if (event.target !== input) {
                openCalendar();
            }
        });

        input.addEventListener("click", function () {
            input.focus();
        });

        input.addEventListener("change", function () {
            if (!/^\d{4}-\d{2}-\d{2}$/.test(input.value)) {
                return;
            }

            label.textContent = displayDate(input.value);

            const url = new URL(
                "/QuanTri/Dashboard",
                window.location.origin
            );

            url.searchParams.set(
                "period",
                getActivePeriod()
            );

            url.searchParams.set(
                "selectedDate",
                input.value
            );

            window.location.assign(url.toString());
        });
    }

    initDatePicker();

    root.querySelectorAll(".counter").forEach(function (counter) {
        const target = Math.floor(
            positive(counter.dataset.count)
        );

        if (reducedMotion || target === 0) {
            counter.textContent = numberText(target);
            return;
        }

        const duration = 800;
        const started = performance.now();

        function animate(timestamp) {
            const progress = Math.min(
                (timestamp - started) / duration,
                1
            );

            const eased = 1 - Math.pow(1 - progress, 3);

            counter.textContent = numberText(
                Math.floor(target * eased)
            );

            if (progress < 1) {
                requestAnimationFrame(animate);
            } else {
                counter.textContent = numberText(target);
            }
        }

        requestAnimationFrame(animate);
    });

    const revenueLabels = asArray(
        window.labelDoanhThu
    ).map(String);

    const revenueValues = asArray(
        window.dataDoanhThu
    ).map(positive);

    const orderLabels = asArray(
        window.dashboardOrderLabels
    ).map(String);

    const orderValues = asArray(
        window.dashboardOrderValues
    ).map(positive);

    const orderLookup = new Map();

    orderLabels.forEach(function (label, index) {
        orderLookup.set(label, orderValues[index] ?? 0);
    });

    const orders = revenueLabels.map(function (label) {
        return orderLookup.get(label) ?? 0;
    });

    const revenueCanvas = document.getElementById(
        "chartDoanhThu"
    );

    const revenuePanel = root.querySelector(
        ".htv3-revenue-panel"
    );

    if (revenueLabels.length !== revenueValues.length ||
        orderLabels.length !== orderValues.length ||
        revenueLabels.length !== orderLabels.length ||
        revenueLabels.some((label, index) =>
            label !== orderLabels[index])) {
        console.error(
            "HappyTrip: Mốc dữ liệu doanh thu và đơn hàng không đồng bộ."
        );
    }

    const hasRevenue = revenueValues.some(value => value > 0);
    const hasOrders = orders.some(value => value > 0);

    if (typeof window.Chart === "undefined") {
        console.error("HappyTrip: Chưa tải được Chart.js");

        showChartEmpty(
            revenueCanvas,
            "Không tải được thư viện biểu đồ."
        );

        const categoryCanvas = document.getElementById(
            "chartCoCauDoanhThu"
        );

        showChartEmpty(
            categoryCanvas,
            "Không tải được thư viện biểu đồ."
        );
    } else {
        const Chart = window.Chart;

        Chart.defaults.font.family =
            "'Plus Jakarta Sans', 'Segoe UI', Arial, sans-serif";

        Chart.defaults.font.size = 11;
        Chart.defaults.color = COLORS.muted;
        Chart.defaults.animation.duration =
            reducedMotion ? 0 : 650;

        const tooltipOptions = {
            backgroundColor: COLORS.tooltip,
            titleColor: COLORS.white,
            bodyColor: "#F3FAF5",
            padding: 12,
            cornerRadius: 10,
            displayColors: true,
            boxPadding: 5
        };

        if (revenueCanvas && revenueLabels.length > 0) {
            const chartWrapper = revenueCanvas.parentElement;

            if (!hasRevenue && !hasOrders) {
                showChartEmpty(
                    revenueCanvas,
                    "Chưa phát sinh doanh thu hoặc đơn đặt tour trong kỳ đã chọn."
                );
            } else {
                const ctx = revenueCanvas.getContext("2d");

                if (ctx) {
                    const gradient = ctx.createLinearGradient(
                        0, 0, 0, 300
                    );

                    gradient.addColorStop(
                        0, "rgba(38, 160, 92, 0.94)"
                    );

                    gradient.addColorStop(
                        1, "rgba(91, 191, 127, 0.65)"
                    );

                    new Chart(revenueCanvas, {
                        type: "bar",

                        data: {
                            labels: revenueLabels,
                            datasets: [
                                {
                                    type: "bar",
                                    label: "Doanh thu",
                                    data: revenueValues,
                                    yAxisID: "yRevenue",

                                    backgroundColor: gradient,
                                    hoverBackgroundColor: "#18794C",

                                    borderRadius: 7,
                                    borderSkipped: false,

                                    maxBarThickness: 28,
                                    categoryPercentage: 0.75,
                                    barPercentage: 0.8,

                                    order: 2
                                },
                                {
                                    type: "line",
                                    label: "Số đơn đặt",
                                    data: orders,
                                    yAxisID: "yOrders",

                                    borderColor: COLORS.yellow,
                                    backgroundColor: COLORS.yellow,

                                    borderWidth: 3,
                                    tension: 0.32,

                                    pointRadius: revenueLabels.length > 18
                                        ? 0
                                        : 3,

                                    pointHoverRadius: 6,
                                    pointHitRadius: 12,

                                    pointBackgroundColor: COLORS.white,
                                    pointBorderColor: COLORS.yellow,
                                    pointBorderWidth: 2,

                                    fill: false,
                                    spanGaps: false,
                                    order: 1
                                }
                            ]
                        },

                        options: {
                            responsive: true,
                            maintainAspectRatio: false,
                            animation: reducedMotion
                                ? false
                                : { duration: 650 },

                            interaction: {
                                mode: "index",
                                intersect: false
                            },

                            layout: {
                                padding: {
                                    top: 12,
                                    right: 4,
                                    bottom: 4,
                                    left: 2
                                }
                            },

                            plugins: {
                                legend: {
                                    display: false
                                },

                                tooltip: {
                                    ...tooltipOptions,
                                    callbacks: {
                                        label(context) {
                                            if (context.dataset.yAxisID ===
                                                "yRevenue") {
                                                return "Doanh thu: " +
                                                    moneyText(context.parsed.y);
                                            }

                                            return "Số đơn: " +
                                                numberText(context.parsed.y) +
                                                " đơn";
                                        }
                                    }
                                }
                            },

                            scales: {
                                x: {
                                    grid: {
                                        display: false
                                    },

                                    border: {
                                        display: false
                                    },

                                    ticks: {
                                        color: COLORS.muted,
                                        maxRotation: 0,
                                        minRotation: 0,
                                        autoSkip: true,
                                        maxTicksLimit: 10,
                                        font: {
                                            size: 10,
                                            weight: "600"
                                        },
                                        padding: 8
                                    }
                                },

                                yRevenue: {
                                    type: "linear",
                                    position: "left",
                                    beginAtZero: true,

                                    border: {
                                        display: false,
                                        dash: [4, 4]
                                    },

                                    grid: {
                                        color: COLORS.grid,
                                        drawTicks: false
                                    },

                                    title: {
                                        display: true,
                                        text: "Doanh thu (VNĐ)",
                                        color: COLORS.muted,
                                        font: {
                                            size: 10,
                                            weight: "600"
                                        }
                                    },

                                    ticks: {
                                        color: COLORS.muted,
                                        maxTicksLimit: 5,
                                        padding: 8,
                                        callback(value) {
                                            return shortMoney(value);
                                        }
                                    }
                                },

                                yOrders: {
                                    type: "linear",
                                    position: "right",
                                    beginAtZero: true,
                                    suggestedMax: Math.max(
                                        1,
                                        Math.ceil(Math.max(...orders, 0) * 1.15)
                                    ),

                                    border: {
                                        display: false
                                    },

                                    grid: {
                                        drawOnChartArea: false,
                                        drawTicks: false
                                    },

                                    title: {
                                        display: true,
                                        text: "Số đơn",
                                        color: COLORS.muted,
                                        font: {
                                            size: 10,
                                            weight: "600"
                                        }
                                    },

                                    ticks: {
                                        color: COLORS.muted,
                                        precision: 0,
                                        maxTicksLimit: 5,
                                        stepSize: 1,
                                        padding: 7,
                                        callback(value) {
                                            return Number.isInteger(value)
                                                ? numberText(value)
                                                : "";
                                        }
                                    }
                                }
                            }
                        }
                    });
                }
            }

            if (chartWrapper && (hasRevenue || hasOrders)) {
                addNote(
                    revenuePanel,
                    "Cột xanh: doanh thu từ đơn đã thanh toán. Đường vàng: số đơn chưa hủy, tính theo ngày đặt."
                );
            }
        }

        const categoryCanvas = document.getElementById(
            "chartCoCauDoanhThu"
        );

        const categoryLegend = document.getElementById(
            "htv3CategoryLegend"
        );

        const categoryPanel = root.querySelector(
            ".htv3-category-panel"
        );

        const categories = asArray(
            window.dashboardCategoryRevenue
        )
            .map(item => ({
                name: asText(
                    item?.name ?? item?.Name,
                    "Chưa phân loại"
                ),
                revenue: positive(
                    item?.revenue ?? item?.Revenue
                )
            }))
            .filter(item => item.revenue > 0)
            .sort((a, b) => b.revenue - a.revenue);

        const categoryTotal = categories.reduce(
            (sum, item) => sum + item.revenue,
            0
        );

        if (categoryCanvas && categoryTotal > 0) {
            const centerPlugin = {
                id: "happytripDashboardDonutCenter",

                afterDraw(chart) {
                    const arc = chart.getDatasetMeta(0)
                        ?.data?.[0];

                    if (!arc) return;

                    const ctx = chart.ctx;
                    const x = arc.x;
                    const y = arc.y;

                    ctx.save();

                    ctx.textAlign = "center";
                    ctx.textBaseline = "middle";

                    ctx.fillStyle = COLORS.greenDark;
                    ctx.font =
                        "800 17px 'Plus Jakarta Sans', sans-serif";

                    ctx.fillText(
                        shortMoney(categoryTotal),
                        x,
                        y - 8,
                        arc.innerRadius * 1.85
                    );

                    ctx.fillStyle = COLORS.muted;
                    ctx.font =
                        "600 10px 'Plus Jakarta Sans', sans-serif";

                    ctx.fillText(
                        "Tổng doanh thu",
                        x,
                        y + 15,
                        arc.innerRadius * 1.85
                    );

                    ctx.restore();
                }
            };

            new Chart(categoryCanvas, {
                type: "doughnut",

                data: {
                    labels: categories.map(item => item.name),

                    datasets: [{
                        data: categories.map(item => item.revenue),

                        backgroundColor: categories.map(
                            (_, index) =>
                                PALETTE[index % PALETTE.length]
                        ),

                        borderColor: COLORS.white,
                        borderWidth: categories.length === 1 ? 0 : 3,

                        hoverOffset: 6,
                        borderRadius: categories.length > 1 ? 3 : 0
                    }]
                },

                plugins: [centerPlugin],

                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    cutout: "69%",

                    animation: reducedMotion
                        ? false
                        : { duration: 700 },

                    plugins: {
                        legend: {
                            display: false
                        },

                        tooltip: {
                            ...tooltipOptions,
                            callbacks: {
                                label(context) {
                                    const amount = positive(context.raw);
                                    const share = amount / categoryTotal * 100;

                                    return (
                                        moneyText(amount) +
                                        " (" +
                                        percentageFormatter.format(share) +
                                        "%)"
                                    );
                                }
                            }
                        }
                    }
                }
            });

            if (categoryLegend) {
                categoryLegend.replaceChildren();

                categories.forEach(function (item, index) {
                    const row = make(
                        "div",
                        "htv3-category-legend-item"
                    );

                    const label = make(
                        "span",
                        "htv3-category-legend-item-label"
                    );

                    const dot = make(
                        "span",
                        "htv3-category-color"
                    );

                    dot.style.backgroundColor =
                        PALETTE[index % PALETTE.length];

                    const name = make("span", "", item.name);
                    name.title = item.name;

                    label.append(dot, name);

                    const value = make(
                        "strong",
                        "",
                        percentageFormatter.format(
                            item.revenue / categoryTotal * 100
                        ) + "%"
                    );

                    row.append(label, value);
                    categoryLegend.appendChild(row);
                });
            }

            addNote(
                categoryPanel,
                categories.length === 1
                    ? "Chỉ có một loại tour phát sinh doanh thu trong kỳ."
                    : `${categories.length} loại tour phát sinh doanh thu trong kỳ.`
            );
        } else {
            if (categoryCanvas) {
                showChartEmpty(
                    categoryCanvas,
                    "Chưa có doanh thu theo loại tour trong kỳ."
                );
            }

            if (categoryLegend) {
                categoryLegend.replaceChildren();
            }
        }
    }

    const destinationContainer = document.getElementById(
        "htv3DestinationList"
    );

    if (destinationContainer) {
        destinationContainer.replaceChildren();

        const destinations = asArray(
            window.dashboardDestinations
        )
            .map(item => ({
                name: asText(
                    item?.name ?? item?.Name,
                    "Chưa xác định"
                ),
                revenue: positive(
                    item?.revenue ?? item?.Revenue
                ),
                guests: positive(
                    item?.guests ?? item?.Guests
                ),
                imageUrl: asText(
                    item?.imageUrl ?? item?.ImageUrl
                )
            }))
            .filter(item => item.revenue > 0)
            .sort((a, b) => b.revenue - a.revenue)
            .slice(0, 5);

        if (destinations.length === 0) {
            destinationContainer.appendChild(
                emptyState(
                    "Chưa có doanh thu theo điểm đến trong kỳ."
                )
            );
        } else {
            const maximum = Math.max(
                ...destinations.map(item => item.revenue)
            );

            destinations.forEach(function (item, index) {
                const row = make(
                    "div",
                    "htv3-destination-row"
                );

                const image = makeImage(
                    item.imageUrl,
                    item.name
                );

                const name = make(
                    "span",
                    "htv3-destination-name",
                    item.name
                );

                name.title = item.name;

                const track = make(
                    "div",
                    "htv3-destination-track"
                );

                const fill = make(
                    "div",
                    "htv3-destination-fill"
                );

                fill.style.width = (
                    maximum > 0
                        ? item.revenue / maximum * 100
                        : 0
                ) + "%";

                fill.style.backgroundColor =
                    PALETTE[index % PALETTE.length];

                track.appendChild(fill);

                const value = make(
                    "strong",
                    "htv3-destination-value",
                    shortMoney(item.revenue)
                );

                row.title =
                    `${item.name}: ${moneyText(item.revenue)}; ` +
                    `${numberText(item.guests)} khách`;

                row.append(image, name, track, value);
                destinationContainer.appendChild(row);
            });
        }
    }

    const timeline = document.getElementById(
        "htv3UpcomingTimeline"
    );

    if (timeline) {
        timeline.replaceChildren();

        const upcoming = asArray(
            window.dashboardUpcoming
        )
            .map(item => ({
                tourName: asText(
                    item?.tourName ?? item?.TourName,
                    "Tour du lịch"
                ),
                destination: asText(
                    item?.destination ?? item?.Destination,
                    "Việt Nam"
                ),
                imageUrl: asText(
                    item?.imageUrl ?? item?.ImageUrl
                ),
                departureDate: asText(
                    item?.departureDate ?? item?.DepartureDate
                ),
                day: positive(
                    item?.day ?? item?.Day
                ),
                month: positive(
                    item?.month ?? item?.Month
                ),
                guests: positive(
                    item?.guests ?? item?.Guests
                ),
                daysRemaining: toNumber(
                    item?.daysRemaining ?? item?.DaysRemaining
                ),
                remainingSeats: positive(
                    item?.remainingSeats ?? item?.RemainingSeats
                )
            }))
            .slice(0, 3);

        if (upcoming.length === 0) {
            timeline.appendChild(
                emptyState(
                    "Hiện chưa có lịch khởi hành sắp tới.",
                    true
                )
            );
        } else {
            upcoming.forEach(function (item) {
                const row = make(
                    "div",
                    "htv3-schedule-item"
                );

                const date = make(
                    "div",
                    "htv3-schedule-date"
                );

                date.append(
                    make(
                        "strong",
                        "",
                        String(item.day).padStart(2, "0")
                    ),
                    make(
                        "span",
                        "",
                        "Th" + String(item.month).padStart(2, "0")
                    )
                );

                const image = makeImage(
                    item.imageUrl,
                    item.tourName
                );

                image.className = "htv3-schedule-image";

                const info = make(
                    "div",
                    "htv3-schedule-info"
                );

                const title = make(
                    "strong",
                    "",
                    item.tourName
                );

                title.title = item.tourName;

                const meta = make(
                    "div",
                    "htv3-schedule-meta"
                );

                const guests = make("span");

                guests.append(
                    makeIcon("fa-solid fa-users"),
                    document.createTextNode(
                        " " + numberText(item.guests) + " khách"
                    )
                );

                const remaining = make("span");

                const remainingText = item.daysRemaining === 0
                    ? "Khởi hành hôm nay"
                    : item.daysRemaining === 1
                        ? "Còn 1 ngày"
                        : "Còn " +
                          numberText(item.daysRemaining) +
                          " ngày";

                remaining.append(
                    makeIcon("fa-solid fa-flag"),
                    document.createTextNode(
                        " " + remainingText
                    )
                );

                meta.append(guests, remaining);
                info.append(title, meta);

                row.title =
                    `Khởi hành: ${item.departureDate} | ` +
                    `Còn ${numberText(item.remainingSeats)} chỗ`;

                row.append(date, image, info);
                timeline.appendChild(row);
            });
        }
    }
});
