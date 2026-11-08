
/* =========================================================
   HAPPYTRIP - TOUR COMPARE SELECTION
   Chon tour so sanh tai trang danh sach
   ========================================================= */

(() => {
    "use strict";

    const STORAGE_KEY = "tour_compare_ids";
    const MAX_TOURS = 4;

    const COMPARE_URL = "/Tour/SoSanh";

    const elements = {
        bar: document.getElementById("compareBar"),
        count: document.getElementById("compareCount"),
        compare: document.getElementById("compareNowBtn"),
        clear: document.getElementById("clearCompareBtn")
    };

    const compareButtons = Array.from(
        document.querySelectorAll(
            ".compare-toggle-btn[data-tour-id]"
        )
    );

    if (!compareButtons.length && !elements.bar) {
        return;
    }

    /* =====================================================
       01. STORAGE
       ===================================================== */

    function normalizeIds(value) {
        let list = value;

        if (typeof list === "string") {
            list = list.split(",");
        }

        if (!Array.isArray(list)) {
            return [];
        }

        const result = [];

        for (const item of list) {
            const id = Number(item);

            if (
                Number.isSafeInteger(id) &&
                id > 0 &&
                !result.includes(id)
            ) {
                result.push(id);
            }

            if (result.length >= MAX_TOURS) {
                break;
            }
        }

        return result;
    }

    function getSelectedIds() {
        try {
            const raw = localStorage.getItem(STORAGE_KEY);

            if (!raw) {
                return [];
            }

            return normalizeIds(JSON.parse(raw));
        } catch (error) {
            console.warn(
                "[HappyTrip] Không đọc được danh sách so sánh:",
                error
            );

            return [];
        }
    }

    function saveSelectedIds(ids) {
        const normalized = normalizeIds(ids);

        try {
            localStorage.setItem(
                STORAGE_KEY,
                JSON.stringify(normalized)
            );
        } catch (error) {
            console.warn(
                "[HappyTrip] Không lưu được danh sách so sánh:",
                error
            );
        }

        return normalized;
    }

    /* =====================================================
       02. BUTTON DISPLAY
       ===================================================== */

    function updateTourButton(button, selected) {
        button.classList.toggle(
            "active",
            selected
        );

        button.classList.toggle(
            "is-selected",
            selected
        );

        button.setAttribute(
            "aria-pressed",
            String(selected)
        );

        button.setAttribute(
            "title",
            selected
                ? "Bỏ tour khỏi danh sách so sánh"
                : "Thêm tour vào danh sách so sánh"
        );

        button.replaceChildren();

        const icon = document.createElement("i");

        icon.className = selected
            ? "fa-solid fa-check"
            : "fa-solid fa-code-compare";

        const label = document.createElement("span");

        label.textContent = selected
            ? "Đã thêm"
            : "Thêm so sánh";

        button.append(icon, label);
    }

    /* =====================================================
       03. COMPARE BAR
       ===================================================== */

    function updateCompareBar(ids) {
        const total = ids.length;

        if (elements.bar) {
            const shouldShow = total > 0;

            elements.bar.hidden = !shouldShow;

            // Dam bao thanh hien thi khi CSS cu co display
            elements.bar.style.display = shouldShow
                ? ""
                : "none";

            elements.bar.classList.toggle(
                "has-selection",
                shouldShow
            );

            elements.bar.setAttribute(
                "aria-hidden",
                String(!shouldShow)
            );
        }

        if (elements.count) {
            elements.count.textContent =
                total === 1
                    ? "Đã chọn 1/4 tour"
                    : `Đã chọn ${total}/4 tour`;
        }

        if (elements.compare) {
            elements.compare.disabled = total < 2;

            elements.compare.setAttribute(
                "aria-disabled",
                String(total < 2)
            );

            elements.compare.setAttribute(
                "title",
                total < 2
                    ? "Chọn ít nhất 2 tour để so sánh"
                    : `Xem bảng so sánh ${total} tour`
            );
        }

        if (elements.clear) {
            elements.clear.disabled = total === 0;
        }
    }

    /* =====================================================
       04. UPDATE ALL UI
       ===================================================== */

    function updateUI() {
        const selectedIds = getSelectedIds();

        compareButtons.forEach(button => {
            const id = Number(button.dataset.tourId);

            updateTourButton(
                button,
                selectedIds.includes(id)
            );
        });

        updateCompareBar(selectedIds);
    }

    /* =====================================================
       05. TOGGLE SELECTED TOUR
       ===================================================== */

    function toggleTour(id) {
        const tourId = Number(id);

        if (
            !Number.isSafeInteger(tourId) ||
            tourId <= 0
        ) {
            return;
        }

        const selectedIds = getSelectedIds();

        const existingIndex =
            selectedIds.indexOf(tourId);

        if (existingIndex >= 0) {
            // Bam lai tour da chon thi bo chon
            selectedIds.splice(existingIndex, 1);
        } else {
            if (selectedIds.length >= MAX_TOURS) {
                window.alert(
                    "Bạn chỉ có thể so sánh tối đa 4 tour. " +
                    "Hãy bỏ bớt một tour để chọn tour khác."
                );

                return;
            }

            selectedIds.push(tourId);
        }

        saveSelectedIds(selectedIds);

        updateUI();

        // KHONG tu dong chuyen trang khi du 2 tour
        // Nguoi dung co the chon tiep toi da 4 tour
    }

    /* =====================================================
       06. OPEN COMPARISON
       ===================================================== */

    function openComparison() {
        const ids = getSelectedIds();

        if (ids.length < 2) {
            window.alert(
                "Vui lòng chọn ít nhất 2 tour để so sánh."
            );

            return;
        }

        const url =
            COMPARE_URL +
            "?ids=" +
            encodeURIComponent(ids.join(","));

        window.location.assign(url);
    }

    /* =====================================================
       07. CLEAR COMPARISON
       ===================================================== */

    function clearComparison() {
        const ids = getSelectedIds();

        if (!ids.length) {
            return;
        }

        saveSelectedIds([]);

        updateUI();
    }

    /* =====================================================
       08. BIND EVENTS
       ===================================================== */

    function bindTourButtons() {
        compareButtons.forEach(button => {
            button.addEventListener("click", event => {
                event.preventDefault();

                const id = button.dataset.tourId;

                toggleTour(id);
            });
        });
    }

    function bindCompareBar() {
        elements.compare?.addEventListener(
            "click",
            openComparison
        );

        elements.clear?.addEventListener(
            "click",
            clearComparison
        );
    }

    /* =====================================================
       09. MULTIPLE TAB SYNC
       ===================================================== */

    function handleStorageUpdate(event) {
        if (event.key !== STORAGE_KEY) {
            return;
        }

        updateUI();
    }

    /* =====================================================
       10. RESTORE AFTER NAVIGATION
       ===================================================== */

    function handlePageShow() {
        updateUI();
    }

    /* =====================================================
       11. INITIALIZE
       ===================================================== */

    function initialize() {
        bindTourButtons();
        bindCompareBar();

        updateUI();

        window.addEventListener(
            "storage",
            handleStorageUpdate
        );

        window.addEventListener(
            "pageshow",
            handlePageShow
        );

        console.log(
            "[HappyTrip] Compare Selection ready:",
            getSelectedIds()
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
