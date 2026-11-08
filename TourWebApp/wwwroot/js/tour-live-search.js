
(() => {
    "use strict";

    const API_URL = "/Tour/GoiYTour";
    const SEARCH_DELAY = 120;

    const inputs = new Set();
    const state = new WeakMap();

    // Khung gợi ý riêng, không dùng goiYBox cũ
    const dropdown = document.createElement("div");
    dropdown.id = "happytrip-live-results";

    Object.assign(dropdown.style, {
        position: "fixed",
        zIndex: "2147483647",
        display: "none",
        background: "#ffffff",
        border: "1px solid #dce6dc",
        borderRadius: "14px",
        boxShadow: "0 16px 45px rgba(20,50,35,.22)",
        padding: "7px",
        maxHeight: "380px",
        overflowY: "auto",
        boxSizing: "border-box",
        fontFamily: "Arial, sans-serif"
    });

    let activeInput = null;

    function registerInputs() {
        const found = document.querySelectorAll(
            'input[name="diadiem"],' +
            '#diadiemInputTop,' +
            '#diadiemInput'
        );

        found.forEach(input => {
            if (inputs.has(input)) return;

            inputs.add(input);
            input.setAttribute("autocomplete", "off");

            state.set(input, {
                timer: null,
                controller: null,
                sequence: 0,
                results: [],
                selectedIndex: -1
            });
        });
    }

    function positionDropdown() {
        if (!activeInput || dropdown.style.display === "none") {
            return;
        }

        const rect = activeInput.getBoundingClientRect();
        const viewportWidth = window.innerWidth;
        const viewportHeight = window.innerHeight;

        const width = Math.min(
            Math.max(rect.width, 320),
            viewportWidth - 20
        );

        const left = Math.max(
            10,
            Math.min(rect.left, viewportWidth - width - 10)
        );

        const spaceBelow = viewportHeight - rect.bottom;
        const openAbove = spaceBelow < 220 && rect.top > 220;

        dropdown.style.width = `${width}px`;
        dropdown.style.left = `${left}px`;
        dropdown.style.maxHeight = `${Math.min(
            380,
            Math.max(130, openAbove ? rect.top - 15 : spaceBelow - 15)
        )}px`;

        if (openAbove) {
            dropdown.style.top = "auto";
            dropdown.style.bottom =
                `${viewportHeight - rect.top + 5}px`;
        } else {
            dropdown.style.bottom = "auto";
            dropdown.style.top = `${rect.bottom + 7}px`;
        }
    }

    function show() {
        dropdown.style.display = "block";
        positionDropdown();
    }

    function hide() {
        dropdown.style.display = "none";
        dropdown.replaceChildren();
    }

    function makeElement(tag, className, text) {
        const element = document.createElement(tag);

        if (className) element.className = className;
        if (text !== undefined) element.textContent = text;

        return element;
    }

    function showMessage(message) {
        dropdown.replaceChildren();

        const item = makeElement("div", "", message);

        Object.assign(item.style, {
            padding: "16px 12px",
            color: "#64776a",
            fontSize: "13px",
            lineHeight: "1.5"
        });

        dropdown.appendChild(item);
        show();
    }

    function makeResult(tour) {
        const link = makeElement("a");

        const id = Number(tour.idTour);
        link.href = tour.url ||
            `/Tour/ChiTiet?id=${encodeURIComponent(id)}`;

        Object.assign(link.style, {
            display: "flex",
            alignItems: "center",
            gap: "12px",
            padding: "12px 10px",
            borderRadius: "9px",
            color: "#244b36",
            textDecoration: "none",
            cursor: "pointer"
        });

        const info = makeElement("span");

        Object.assign(info.style, {
            display: "flex",
            flexDirection: "column",
            gap: "5px",
            flex: "1",
            minWidth: "0"
        });

        const name = makeElement(
            "strong",
            "",
            tour.tenTour || "Tour du lịch"
        );

        Object.assign(name.style, {
            fontSize: "13px",
            lineHeight: "1.45",
            fontWeight: "700",
            whiteSpace: "normal"
        });

        const location = makeElement(
            "small",
            "",
            tour.diaDiem || "Chưa cập nhật địa điểm"
        );

        Object.assign(location.style, {
            fontSize: "12px",
            color: "#7b8b80"
        });

        const price = makeElement(
            "span",
            "",
            tour.gia || "Liên hệ"
        );

        Object.assign(price.style, {
            fontSize: "12px",
            color: "#a5762a",
            fontWeight: "700",
            flexShrink: "0"
        });

        info.append(name, location);
        link.append(info, price);

        link.addEventListener("mouseenter", () => {
            link.style.background = "#eff7ed";
        });

        link.addEventListener("mouseleave", () => {
            link.style.background = "transparent";
        });

        return link;
    }

    function renderResults(tours) {
        dropdown.replaceChildren();

        if (!Array.isArray(tours) || tours.length === 0) {
            showMessage("Không tìm thấy tour phù hợp.");
            return;
        }

        tours.forEach(tour => {
            dropdown.appendChild(makeResult(tour));
        });

        const footer = makeElement(
            "div",
            "",
            `Tìm thấy ${tours.length} gợi ý • Bấm vào tour để xem chi tiết`
        );

        Object.assign(footer.style, {
            padding: "10px",
            color: "#819086",
            fontSize: "11px",
            borderTop: "1px solid #edf1ec",
            marginTop: "4px"
        });

        dropdown.appendChild(footer);
        show();
    }

    async function search(input, keyword, currentSequence) {
        const s = state.get(input);
        if (!s) return;

        const controller = new AbortController();
        s.controller = controller;

        try {
            const response = await fetch(
                `${API_URL}?keyword=${encodeURIComponent(keyword)}`,
                {
                    signal: controller.signal,
                    headers: {
                        Accept: "application/json"
                    },
                    cache: "no-store"
                }
            );

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const data = await response.json();

            if (
                activeInput !== input ||
                input.value.trim() !== keyword ||
                s.sequence !== currentSequence
            ) {
                return;
            }

            s.results = Array.isArray(data) ? data : [];
            renderResults(s.results);

        } catch (error) {
            if (error.name === "AbortError") return;

            console.error("[HappyTrip] Live Search:", error);

            if (
                activeInput === input &&
                s.sequence === currentSequence
            ) {
                showMessage(
                    "Không tải được kết quả tìm kiếm. Vui lòng thử lại."
                );
            }
        }
    }

    function handleInput(input) {
        const s = state.get(input);
        if (!s) return;

        activeInput = input;

        clearTimeout(s.timer);
        s.controller?.abort();

        const keyword = input.value.trim();
        const currentSequence = ++s.sequence;

        if (!keyword) {
            hide();
            return;
        }

        // Có phản hồi ngay từ ký tự đầu tiên
        showMessage("Đang tìm tour phù hợp...");

        s.timer = setTimeout(() => {
            search(input, keyword, currentSequence);
        }, SEARCH_DELAY);
    }

    function initialize() {
        if (!document.body) return;

        document.body.appendChild(dropdown);
        registerInputs();

        // Bắt sự kiện trên document để không phụ thuộc ID
        document.addEventListener("input", event => {
            const input = event.target;

            if (!(input instanceof HTMLInputElement)) return;

            if (
                input.name !== "diadiem" &&
                input.id !== "diadiemInputTop" &&
                input.id !== "diadiemInput"
            ) {
                return;
            }

            if (!state.has(input)) {
                registerInputs();
            }

            handleInput(input);
        }, true);

        document.addEventListener("focusin", event => {
            const input = event.target;

            if (!state.has(input)) return;

            activeInput = input;

            if (input.value.trim()) {
                handleInput(input);
            }
        });

        document.addEventListener("keydown", event => {
            if (event.key === "Escape") {
                hide();
            }
        });

        document.addEventListener("pointerdown", event => {
            if (
                activeInput &&
                event.target !== activeInput &&
                !dropdown.contains(event.target)
            ) {
                hide();
            }
        });

        window.addEventListener(
            "resize",
            positionDropdown
        );

        window.addEventListener(
            "scroll",
            positionDropdown,
            true
        );

        console.log(
            `[HappyTrip] Live Search ready: ${inputs.size} input(s)`
        );
    }

    if (document.readyState === "loading") {
        document.addEventListener(
            "DOMContentLoaded",
            initialize,
            { once: true }
        );
    } else {
        initialize();
    }
})();
