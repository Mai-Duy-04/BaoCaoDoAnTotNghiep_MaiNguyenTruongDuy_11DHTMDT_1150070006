"use strict";
/* ==========================================================
   HAPPYTRIP - ADMIN TOUR MANAGEMENT

   - Sort by newest / oldest
   - Sort A-Z / Z-A (Vietnamese)
   - Sort by price
   - Pagination: 8 / 12 / 20
   - Preserve current search / type filters
   - No Controller or SQL changes
   ========================================================== */

function confirmNgungBan() {
    return confirm(
        "Bạn có chắc muốn thay đổi trạng thái tour này?"
    );
}

document.addEventListener("DOMContentLoaded", function () {

    const root = document.getElementById("htTourManager");
    const cardContainer = document.getElementById("htTourCards");

    if (!root || !cardContainer) {
        return;
    }

    const sortSelect = document.getElementById("htTourSort");
    const pageSizeSelect = document.getElementById("htTourPageSize");

    const paginationButtons = document.getElementById(
        "htTourPaginationButtons"
    );

    const pageInfo = document.getElementById("htTourPageInfo");

    if (
        !sortSelect ||
        !pageSizeSelect ||
        !paginationButtons ||
        !pageInfo
    ) {
        return;
    }

    const allCards = Array.from(
        cardContainer.querySelectorAll(".ht-tour-card")
    );

    if (allCards.length === 0) {
        return;
    }

    const collator = new Intl.Collator("vi", {
        sensitivity: "base",
        numeric: true
    });

    const formatter = new Intl.NumberFormat("vi-VN");

    let currentPage = 1;
    let pageSize = 8;
    let sortBy = "newest";

    /* ======================================================
       GET VALUES
       ====================================================== */

    function getTourId(card) {
        const value = Number(card.dataset.tourId);

        return Number.isFinite(value) ? value : 0;
    }

    function getTourName(card) {
        return String(card.dataset.tourName || "").trim();
    }

    function getTourPrice(card) {
        const value = Number(card.dataset.tourPrice);

        return Number.isFinite(value) ? value : 0;
    }

    /* ======================================================
       SORT
       ====================================================== */

    function getSortedCards() {

        const cards = allCards.slice();

        cards.sort(function (a, b) {

            switch (sortBy) {

                case "oldest":
                    return getTourId(a) - getTourId(b);

                case "az":
                    return collator.compare(
                        getTourName(a),
                        getTourName(b)
                    );

                case "za":
                    return collator.compare(
                        getTourName(b),
                        getTourName(a)
                    );

                case "priceAsc":
                    return (
                        getTourPrice(a) - getTourPrice(b)
                    ) || (
                        getTourId(b) - getTourId(a)
                    );

                case "priceDesc":
                    return (
                        getTourPrice(b) - getTourPrice(a)
                    ) || (
                        getTourId(b) - getTourId(a)
                    );

                case "newest":
                default:
                    return getTourId(b) - getTourId(a);
            }
        });

        return cards;
    }

    /* ======================================================
       PAGE BUTTON
       ====================================================== */

    function createButton(label, targetPage, options = {}) {

        const button = document.createElement("button");

        button.type = "button";
        button.className = "ht-tour-page-btn";
        button.textContent = label;

        if (options.current) {
            button.classList.add("active");
            button.setAttribute("aria-current", "page");
        }

        if (options.disabled) {
            button.disabled = true;
        }

        button.setAttribute(
            "aria-label",
            options.ariaLabel || `Trang ${targetPage}`
        );

        button.addEventListener("click", function () {
            if (button.disabled) return;

            currentPage = targetPage;
            render();

            // Scroll to list, not to top of entire Dashboard.
            const listSection = root.querySelector(
                ".ht-tour-list-section"
            );

            if (listSection) {
                const reducedMotion = window.matchMedia(
                    "(prefers-reduced-motion: reduce)"
                ).matches;

                listSection.scrollIntoView({
                    behavior: reducedMotion ? "auto" : "smooth",
                    block: "start"
                });
            }
        });

        return button;
    }

    function addEllipsis() {

        const ellipsis = document.createElement("span");

        ellipsis.className = "ht-tour-pagination-ellipsis";
        ellipsis.textContent = "…";
        ellipsis.setAttribute("aria-hidden", "true");

        paginationButtons.appendChild(ellipsis);
    }

    /* ======================================================
       PAGINATION
       ====================================================== */

    function renderPagination(totalPages) {

        paginationButtons.replaceChildren();

        if (totalPages <= 1) {
            return;
        }

        paginationButtons.appendChild(
            createButton("‹", currentPage - 1, {
                disabled: currentPage === 1,
                ariaLabel: "Trang trước"
            })
        );

        // Up to 5 page numbers close to active page.
        const visiblePages = new Set([
            1,
            totalPages,
            currentPage - 1,
            currentPage,
            currentPage + 1
        ]);

        const pages = Array.from(visiblePages)
            .filter(p => p >= 1 && p <= totalPages)
            .sort((a, b) => a - b);

        let previousPage = 0;

        pages.forEach(function (page) {

            if (previousPage > 0 && page - previousPage > 1) {
                addEllipsis();
            }

            paginationButtons.appendChild(
                createButton(String(page), page, {
                    current: page === currentPage,
                    ariaLabel: `Đến trang ${page}`
                })
            );

            previousPage = page;
        });

        paginationButtons.appendChild(
            createButton("›", currentPage + 1, {
                disabled: currentPage === totalPages,
                ariaLabel: "Trang tiếp theo"
            })
        );
    }

    /* ======================================================
       RENDER
       ====================================================== */

    function render() {

        const sortedCards = getSortedCards();

        const total = sortedCards.length;

        const totalPages = Math.max(
            1,
            Math.ceil(total / pageSize)
        );

        // Ensure page stays valid after changing page size.
        currentPage = Math.min(
            Math.max(1, currentPage),
            totalPages
        );

        const startIndex = (currentPage - 1) * pageSize;
        const endIndex = Math.min(
            startIndex + pageSize,
            total
        );

        // Reorder DOM to match selected sort.
        const fragment = document.createDocumentFragment();

        sortedCards.forEach(function (card) {
            fragment.appendChild(card);
        });

        cardContainer.appendChild(fragment);

        // Only show cards of current page.
        sortedCards.forEach(function (card, index) {
            card.hidden = (
                index < startIndex ||
                index >= endIndex
            );
        });

        const firstNumber = total > 0
            ? startIndex + 1
            : 0;

        pageInfo.replaceChildren();

        const prefix = document.createTextNode(
            "Hiển thị "
        );

        const range = document.createElement("strong");

        range.textContent =
            formatter.format(firstNumber) +
            "–" +
            formatter.format(endIndex);

        const suffix = document.createTextNode(
            " trong tổng " +
            formatter.format(total) +
            " tour · Trang " +
            currentPage +
            "/" +
            totalPages
        );

        pageInfo.append(prefix, range, suffix);

        renderPagination(totalPages);
    }

    /* ======================================================
       EVENTS
       ====================================================== */

    sortSelect.addEventListener("change", function () {

        sortBy = this.value;

        // Return to first page when sort changes.
        currentPage = 1;

        render();
    });

    pageSizeSelect.addEventListener("change", function () {

        const requested = Number(this.value);

        pageSize = [8, 12, 20].includes(requested)
            ? requested
            : 8;

        currentPage = 1;

        render();
    });

    /* ======================================================
       INITIAL LOAD
       ====================================================== */

    sortSelect.value = "newest";
    pageSizeSelect.value = "8";

    render();

    console.info(
        "HappyTrip Tour Manager: Sorting and pagination ready."
    );
});
