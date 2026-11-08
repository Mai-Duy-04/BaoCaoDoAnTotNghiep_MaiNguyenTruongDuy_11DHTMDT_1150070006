
/* =========================================================
   HAPPYTRIP - TOUR COMPARE RESULT
   Trang kết quả so sánh tour
   ========================================================= */

(() => {
    "use strict";

    const STORAGE_KEY = "tour_compare_ids";
    const SAVED_KEY = "happytrip_saved_comparison";

    const MAX_TOURS = 4;
    const TOUR_LIST_URL = "/Tour/TatCa";
    const COMPARE_URL = "/Tour/SoSanh";

    const page = document.getElementById("happyTripCompare");

    if (!page) return;

    const elements = {
        share: document.getElementById("cmpShare"),
        save: document.getElementById("cmpSave"),
        restore: document.getElementById("cmpRestore"),
        clear: document.getElementById("cmpClear"),
        toggleSame: document.getElementById("cmpToggleSame"),
        cards: document.getElementById("cmpTourCards"),
        table: document.getElementById("tourCompareTable"),
        toast: document.getElementById("cmpToast")
    };

    let toastTimer = null;
    let sameRowsHidden = false;

    /* =====================================================
       01. STORAGE HELPERS
       ===================================================== */

    function normalizeIds(value) {
        let list = value;

        if (typeof list === "string") {
            list = list.split(",");
        }

        if (!Array.isArray(list)) {
            return [];
        }

        const ids = [];

        for (const item of list) {
            const id = Number(item);

            if (
                Number.isSafeInteger(id) &&
                id > 0 &&
                !ids.includes(id)
            ) {
                ids.push(id);
            }

            if (ids.length >= MAX_TOURS) {
                break;
            }
        }

        return ids;
    }

    function storageGet(key) {
        try {
            return localStorage.getItem(key);
        } catch (error) {
            console.warn(
                "[HappyTrip] Không thể đọc localStorage:",
                error
            );

            return null;
        }
    }

    function storageSet(key, value) {
        try {
            localStorage.setItem(key, value);
            return true;
        } catch (error) {
            console.warn(
                "[HappyTrip] Không thể ghi localStorage:",
                error
            );

            return false;
        }
    }

    function setCompareIds(ids) {
        const normalized = normalizeIds(ids);

        storageSet(
            STORAGE_KEY,
            JSON.stringify(normalized)
        );

        return normalized;
    }

    function getCompareIds() {
        try {
            return normalizeIds(
                JSON.parse(storageGet(STORAGE_KEY) || "[]")
            );
        } catch {
            return [];
        }
    }

    /* =====================================================
       02. GET CURRENT TOUR IDS
       ===================================================== */

    function getPageIds() {
        if (!elements.cards) return [];

        const cards = elements.cards.querySelectorAll(
            ".ht-compare-tour[data-tour-id]"
        );

        return normalizeIds(
            Array.from(cards, card => card.dataset.tourId)
        );
    }

    function getUrlIds() {
        const params = new URLSearchParams(
            window.location.search
        );

        return normalizeIds(params.get("ids") || "");
    }

    function getCurrentIds() {
        const pageIds = getPageIds();

        if (pageIds.length > 0) {
            return pageIds;
        }

        return getUrlIds();
    }

    function buildCompareUrl(ids) {
        const normalized = normalizeIds(ids);

        if (normalized.length < 2) {
            return TOUR_LIST_URL;
        }

        return (
            COMPARE_URL +
            "?ids=" +
            encodeURIComponent(normalized.join(","))
        );
    }

    function navigateToCompare(ids) {
        const normalized = setCompareIds(ids);

        window.location.assign(
            buildCompareUrl(normalized)
        );
    }

    function syncCurrentComparison() {
        const pageIds = getPageIds();

        if (pageIds.length > 0) {
            setCompareIds(pageIds);
            return;
        }

        // Trang lỗi / không có tour:
        // không tự ghi đè danh sách đang lưu.
        const urlIds = getUrlIds();

        if (urlIds.length > 0) {
            setCompareIds(urlIds);
        }
    }

    /* =====================================================
       03. TOAST
       ===================================================== */

    function showToast(message) {
        const toast = elements.toast;

        if (!toast) {
            console.log("[HappyTrip]", message);
            return;
        }

        if (toastTimer) {
            clearTimeout(toastTimer);
        }

        toast.textContent = message;
        toast.classList.add("is-visible");

        toastTimer = window.setTimeout(() => {
            toast.classList.remove("is-visible");
        }, 3000);
    }

    /* =====================================================
       04. COPY URL
       ===================================================== */

    async function copyToClipboard(value) {
        if (
            navigator.clipboard &&
            window.isSecureContext
        ) {
            try {
                await navigator.clipboard.writeText(value);
                return true;
            } catch (error) {
                console.warn(
                    "[HappyTrip] Clipboard API thất bại:",
                    error
                );
            }
        }

        const textarea = document.createElement("textarea");

        textarea.value = value;
        textarea.readOnly = true;

        Object.assign(textarea.style, {
            position: "fixed",
            left: "-9999px",
            top: "0",
            opacity: "0"
        });

        document.body.appendChild(textarea);

        textarea.focus();
        textarea.select();

        let copied = false;

        try {
            copied = document.execCommand("copy");
        } catch {
            copied = false;
        }

        textarea.remove();

        return copied;
    }

    /* =====================================================
       05. SHARE COMPARISON
       ===================================================== */

    async function shareComparison() {
        const ids = getCurrentIds();

        if (ids.length < 2) {
            showToast(
                "Vui lòng chọn ít nhất 2 tour để chia sẻ."
            );
            return;
        }

        const url = new URL(
            buildCompareUrl(ids),
            window.location.origin
        ).href;

        const success = await copyToClipboard(url);

        showToast(
            success
                ? "Đã sao chép liên kết so sánh!"
                : "Không thể sao chép liên kết."
        );
    }

    /* =====================================================
       06. SAVE COMPARISON
       ===================================================== */

    function readSavedComparison() {
        try {
            const raw = storageGet(SAVED_KEY);

            if (!raw) {
                return null;
            }

            const data = JSON.parse(raw);

            if (!data || typeof data !== "object") {
                return null;
            }

            const ids = normalizeIds(data.ids);

            if (ids.length < 2) {
                return null;
            }

            return {
                ids,
                savedAt: data.savedAt || null
            };
        } catch {
            return null;
        }
    }

    function updateRestoreButton() {
        if (!elements.restore) return;

        const saved = readSavedComparison();

        elements.restore.hidden = !saved;
    }

    function saveComparison() {
        const ids = getCurrentIds();

        if (ids.length < 2) {
            showToast(
                "Cần ít nhất 2 tour để lưu so sánh."
            );
            return;
        }

        const saved = {
            ids,
            savedAt: new Date().toISOString()
        };

        const success = storageSet(
            SAVED_KEY,
            JSON.stringify(saved)
        );

        if (!success) {
            showToast(
                "Không thể lưu so sánh trên trình duyệt."
            );
            return;
        }

        setCompareIds(ids);
        updateRestoreButton();

        showToast(
            "Đã lưu danh sách so sánh!"
        );
    }

    /* =====================================================
       07. RESTORE COMPARISON
       ===================================================== */

    function restoreComparison() {
        const saved = readSavedComparison();

        if (!saved) {
            showToast(
                "Chưa có danh sách so sánh được lưu."
            );
            return;
        }

        navigateToCompare(saved.ids);
    }

    /* =====================================================
       08. REMOVE ONE TOUR
       ===================================================== */

    function removeTour(id) {
        const tourId = Number(id);

        if (
            !Number.isSafeInteger(tourId) ||
            tourId <= 0
        ) {
            return;
        }

        const currentIds = getCurrentIds();

        if (!currentIds.includes(tourId)) {
            return;
        }

        const remainingIds = currentIds.filter(
            currentId => currentId !== tourId
        );

        setCompareIds(remainingIds);

        if (remainingIds.length >= 2) {
            window.location.assign(
                buildCompareUrl(remainingIds)
            );
            return;
        }

        // Còn dưới 2 tour:
        // quay lại danh sách để chọn tiếp.
        window.location.assign(TOUR_LIST_URL);
    }

    function bindRemoveButtons() {
        const buttons = page.querySelectorAll(
            ".remove-compare-item[data-tour-id]"
        );

        buttons.forEach(button => {
            button.addEventListener("click", () => {
                removeTour(button.dataset.tourId);
            });
        });
    }

    /* =====================================================
       09. CLEAR ALL
       ===================================================== */

    function clearComparison() {
        const ids = getCurrentIds();

        if (ids.length === 0) {
            showToast(
                "Danh sách so sánh đang trống."
            );
            return;
        }

        const confirmed = window.confirm(
            "Bạn có muốn xóa tất cả tour đang so sánh không?"
        );

        if (!confirmed) {
            return;
        }

        setCompareIds([]);

        // Bản được lưu bằng nút "Lưu so sánh"
        // vẫn được giữ để người dùng mở lại.
        window.location.assign(TOUR_LIST_URL);
    }

    /* =====================================================
       10. HIDE SAME CRITERIA
       ===================================================== */

    function normalizeValue(value) {
        return String(value ?? "")
            .trim()
            .replace(/\s+/g, " ")
            .toLocaleLowerCase("vi-VN");
    }

    function isSameRow(row) {
        const cells = Array.from(
            row.querySelectorAll(
                ".ht-compare-table__cell"
            )
        );

        if (cells.length < 2) {
            return false;
        }

        const values = cells.map(cell => {
            const raw = cell.getAttribute("data-value");

            return normalizeValue(
                raw !== null
                    ? raw
                    : cell.textContent
            );
        });

        return values.every(
            value => value === values[0]
        );
    }

    function updateSameButton() {
        const button = elements.toggleSame;

        if (!button) {
            return;
        }

        button.replaceChildren();

        const icon = document.createElement("i");

        icon.className = sameRowsHidden
            ? "fa-solid fa-eye"
            : "fa-solid fa-eye-slash";

        const text = document.createTextNode(
            sameRowsHidden
                ? " Hiện tất cả tiêu chí"
                : " Ẩn dòng giống nhau"
        );

        button.append(icon, text);

        button.setAttribute(
            "aria-pressed",
            String(sameRowsHidden)
        );
    }

    function toggleSameCriteria() {
        const table = elements.table;

        if (!table) {
            return;
        }

        sameRowsHidden = !sameRowsHidden;

        const rows = table.querySelectorAll(
            ".ht-compare-table__row"
        );

        let hiddenCount = 0;

        rows.forEach(row => {
            if (row.dataset.rowKind === "header") {
                return;
            }

            const shouldHide =
                sameRowsHidden && isSameRow(row);

            row.classList.toggle(
                "is-same-hidden",
                shouldHide
            );

            if (shouldHide) {
                hiddenCount++;
            }
        });

        updateSameButton();

        if (sameRowsHidden) {
            showToast(
                hiddenCount
                    ? `Đã ẩn ${hiddenCount} tiêu chí giống nhau.`
                    : "Không có tiêu chí giống nhau để ẩn."
            );
        } else {
            showToast(
                "Đã hiển thị tất cả tiêu chí."
            );
        }
    }

    /* =====================================================
       11. EVENTS
       ===================================================== */

    function bindEvents() {
        elements.share?.addEventListener(
            "click",
            shareComparison
        );

        elements.save?.addEventListener(
            "click",
            saveComparison
        );

        elements.restore?.addEventListener(
            "click",
            restoreComparison
        );

        elements.clear?.addEventListener(
            "click",
            clearComparison
        );

        elements.toggleSame?.addEventListener(
            "click",
            toggleSameCriteria
        );

        bindRemoveButtons();
    }

    /* =====================================================
       12. INITIALIZE
       ===================================================== */

    function initialize() {
        syncCurrentComparison();

        updateRestoreButton();
        updateSameButton();

        bindEvents();

        console.log(
            "[HappyTrip] Compare Result ready:",
            getCurrentIds()
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
