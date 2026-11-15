
/* =========================================================
   HAPPYTRIP - DEPARTURE MONTH SELECTOR
   File: wwwroot/js/tour-detail-months.js

   Chuc nang:
   - Tao tab thang tu lich co that trong HTML.
   - Chi hien thi lich cua thang dang chon.
   - Tu dong chon lich con cho trong thang.
   - Dong bo qua co che chon lich hien tai.
   - Giu nguyen tour-detail.js, dat tour, gia, waitlist.
   ========================================================= */

(function () {
    "use strict";

    function initializeDepartureMonths() {

        const calendar = document.querySelector(
            "#lichkhoihanh .tour-departure-calendar"
        );

        if (!calendar) {
            return;
        }

        if (calendar.dataset.monthsInitialized === "true") {
            return;
        }

        const monthHeadings = Array.from(
            calendar.querySelectorAll(
                ":scope > .tour-calendar-month"
            )
        );

        if (monthHeadings.length === 0) {
            return;
        }

        const monthGroups = [];

        // Lay nhom lich da render tu Razor.
        // Khong tao du lieu lich ao.
        monthHeadings.forEach(function (heading) {

            const grid = heading.nextElementSibling;

            if (
                !grid ||
                !grid.classList.contains(
                    "tour-calendar-grid"
                )
            ) {
                return;
            }

            const match = heading.textContent
                .trim()
                .match(/Tháng\s+(\d{1,2})\s*\/\s*(\d{4})/i);

            if (!match) {
                return;
            }

            const month = Number(match[1]);
            const year = Number(match[2]);

            if (
                month < 1 ||
                month > 12 ||
                year < 2000
            ) {
                return;
            }

            const rows = Array.from(
                grid.querySelectorAll(".lich-row")
            );

            if (rows.length === 0) {
                return;
            }

            monthGroups.push({
                key:
                    year + "-" +
                    String(month).padStart(2, "0"),

                year: year,
                month: month,
                heading: heading,
                grid: grid,
                rows: rows
            });
        });

        if (monthGroups.length === 0) {
            return;
        }

        monthGroups.sort(function (a, b) {
            return a.key.localeCompare(b.key);
        });

        calendar.dataset.monthsInitialized = "true";

        /* ==============================================
           1. CREATE MONTH TOOLBAR
           ============================================== */

        const toolbar = document.createElement("div");
        toolbar.className = "ht-month-toolbar";

        const toolbarTitle = document.createElement("div");
        toolbarTitle.className = "ht-month-toolbar-title";

        toolbarTitle.innerHTML =
            '<i class="fa-regular fa-calendar-days"></i>' +
            '<span>Chọn tháng khởi hành</span>';

        const tabs = document.createElement("div");
        tabs.className = "ht-month-tabs";
        tabs.setAttribute("role", "group");
        tabs.setAttribute(
            "aria-label",
            "Lọc lịch khởi hành theo tháng"
        );

        const summary = document.createElement("p");
        summary.className = "ht-month-summary";
        summary.setAttribute("aria-live", "polite");

        toolbar.appendChild(toolbarTitle);
        toolbar.appendChild(tabs);
        toolbar.appendChild(summary);

        calendar.insertBefore(
            toolbar,
            calendar.firstChild
        );

        /* ==============================================
           2. INJECT SCOPED STYLES
           ============================================== */

        if (
            !document.getElementById(
                "htDepartureMonthStyles"
            )
        ) {
            const style = document.createElement("style");
            style.id = "htDepartureMonthStyles";

            style.textContent = `
                #lichkhoihanh .ht-month-toolbar {
                    margin-bottom: 15px;
                    padding-bottom: 14px;
                    border-bottom: 1px solid #e4ece3;
                }

                #lichkhoihanh .ht-month-toolbar-title {
                    display: flex;
                    align-items: center;
                    gap: 8px;
                    margin-bottom: 12px;
                    color: #23553b;
                    font-size: 12px;
                    font-weight: 800;
                }

                #lichkhoihanh .ht-month-toolbar-title i {
                    color: #28915b;
                }

                #lichkhoihanh .ht-month-tabs {
                    display: flex;
                    gap: 9px;
                    padding: 2px 1px 8px;
                    overflow-x: auto;
                    scrollbar-width: thin;
                }

                #lichkhoihanh .ht-month-tab {
                    position: relative;
                    display: flex;
                    flex: 0 0 auto;
                    flex-direction: column;
                    justify-content: center;
                    align-items: center;
                    gap: 5px;
                    min-width: 94px;
                    min-height: 65px;
                    padding: 10px 14px;
                    border: 1px solid #dce8dc;
                    border-radius: 12px;
                    background: #fff;
                    color: #54725d;
                    font: inherit;
                    font-size: 12px;
                    font-weight: 800;
                    line-height: 1.3;
                    cursor: pointer;
                    transition:
                        background .2s,
                        border-color .2s,
                        color .2s;
                }

                #lichkhoihanh .ht-month-tab:hover {
                    border-color: #88b995;
                    background: #f3faf2;
                }

                #lichkhoihanh .ht-month-tab.active {
                    border-color: #167b49;
                    background: #176b43;
                    color: #fff;
                    box-shadow:
                        0 5px 13px rgba(23,107,67,.16);
                }

                #lichkhoihanh .ht-month-tab:focus-visible {
                    outline: 3px solid #a1d9b3;
                    outline-offset: 3px;
                }

                #lichkhoihanh .ht-month-tab-year {
                    font-size: 10px;
                    font-weight: 600;
                    opacity: .8;
                }

                #lichkhoihanh .ht-month-tab-count {
                    display: inline-flex;
                    align-items: center;
                    justify-content: center;
                    min-width: 21px;
                    height: 20px;
                    padding: 0 6px;
                    border-radius: 20px;
                    background: #eaf4e9;
                    color: #3a8753;
                    font-size: 10px;
                    font-weight: 800;
                }

                #lichkhoihanh .ht-month-tab.active
                .ht-month-tab-count {
                    background: rgba(255,255,255,.2);
                    color: #fff;
                }

                #lichkhoihanh .ht-month-summary {
                    display: flex;
                    align-items: center;
                    gap: 6px;
                    margin: 3px 0 0;
                    color: #7b927f;
                    font-size: 11px;
                    line-height: 1.6;
                }

                #lichkhoihanh .tour-calendar-month {
                    display: none !important;
                }

                #lichkhoihanh .tour-calendar-grid[hidden] {
                    display: none !important;
                }

                #lichkhoihanh .ht-month-grid {
                    display: flex;
                    flex-direction: column;
                    gap: 9px;
                }

                #lichkhoihanh .ht-month-grid .lich-row {
                    display: grid;
                    grid-template-columns:
                        43px
                        minmax(35px, .5fr)
                        minmax(55px, .7fr)
                        minmax(90px, 1.6fr);
                    align-items: center;
                    gap: 8px;
                    width: 100%;
                    min-height: 66px;
                    padding: 10px 12px;
                    border: 1px solid #e1eae0;
                    border-radius: 12px;
                    background: #fff;
                    text-align: left;
                }

                #lichkhoihanh .ht-month-grid
                .lich-row.lich-selected {
                    border-color: #27915b !important;
                    background: #edf8ef !important;
                    box-shadow:
                        0 0 0 1px rgba(39,145,91,.1);
                }

                #lichkhoihanh .ht-month-grid
                .tour-calendar-day {
                    display: grid;
                    place-items: center;
                    width: 38px;
                    height: 38px;
                    border-radius: 9px;
                    background: #eaf5eb;
                    color: #1f8450;
                    font-size: 17px;
                    font-weight: 800;
                }

                #lichkhoihanh .ht-month-grid
                .tour-detail-status {
                    justify-self: end;
                    color: #2b8853;
                    font-size: 10px;
                    font-weight: 800;
                    text-align: right;
                    line-height: 1.45;
                }

                #lichkhoihanh .ht-month-grid
                .lich-full .tour-detail-status {
                    color: #bc6258;
                }

                #lichkhoihanh .ht-month-empty {
                    margin: 20px 0 0;
                    padding: 28px 16px;
                    border-radius: 12px;
                    background: #f7faf5;
                    color: #7c9282;
                    font-size: 12px;
                    text-align: center;
                }

                @media (max-width: 650px) {
                    #lichkhoihanh .ht-month-tab {
                        min-width: 85px;
                        min-height: 60px;
                        padding: 9px 11px;
                    }

                    #lichkhoihanh .ht-month-grid .lich-row {
                        grid-template-columns:
                            32px
                            29px
                            47px
                            minmax(0, 1fr);
                        gap: 5px;
                        padding: 8px 7px;
                    }

                    #lichkhoihanh .ht-month-grid
                    .tour-calendar-day {
                        width: 31px;
                        height: 31px;
                        font-size: 14px;
                    }

                    #lichkhoihanh .ht-month-grid
                    .lich-row > span {
                        font-size: 10px;
                    }

                    #lichkhoihanh .ht-month-grid
                    .tour-detail-status {
                        font-size: 9px;
                    }
                }
            `;

            document.head.appendChild(style);
        }

        monthGroups.forEach(function (group) {
            group.grid.classList.add("ht-month-grid");
        });

        /* ==============================================
           3. FIND THE CURRENT SELECTED MONTH
           ============================================== */

        const selectedScheduleInput =
            document.getElementById("selectedLich");

        function findSelectedGroup() {
            const selectedId =
                selectedScheduleInput?.value || "";

            return monthGroups.find(function (group) {
                return group.rows.some(function (row) {
                    return row.dataset.idlich === selectedId;
                });
            });
        }

        const initialGroup = findSelectedGroup() ||
            monthGroups.find(function (group) {
                return group.rows.some(function (row) {
                    return Number(row.dataset.socho) > 0;
                });
            }) ||
            monthGroups[0];

        let activeMonthKey = initialGroup.key;

        /* ==============================================
           4. CREATE TABS
           ============================================== */

        const tabButtons = [];

        monthGroups.forEach(function (group) {

            const button = document.createElement("button");
            button.type = "button";
            button.className = "ht-month-tab";
            button.dataset.monthKey = group.key;

            const monthLabel = document.createElement("span");
            monthLabel.textContent =
                "Tháng " + group.month;

            const yearLabel = document.createElement("span");
            yearLabel.className = "ht-month-tab-year";
            yearLabel.textContent = group.year;

            const countLabel = document.createElement("span");
            countLabel.className = "ht-month-tab-count";
            countLabel.textContent = group.rows.length;
            countLabel.title =
                group.rows.length + " lịch khởi hành";

            button.appendChild(monthLabel);
            button.appendChild(yearLabel);
            button.appendChild(countLabel);

            button.setAttribute(
                "aria-label",
                "Xem " + group.rows.length +
                " lịch khởi hành tháng " +
                group.month + "/" + group.year
            );

            button.addEventListener("click", function () {
                changeMonth(group.key, true);
            });

            tabs.appendChild(button);
            tabButtons.push(button);
        });

        /* ==============================================
           5. MONTH FILTER & AUTO SELECT
           ============================================== */

        function getAvailableRow(group) {

            // Uu tien lich con cho.
            return group.rows.find(function (row) {
                return Number(row.dataset.socho) > 0;
            }) || group.rows[0];
        }

        function updateSummary(group) {

            const available = group.rows.filter(
                function (row) {
                    return Number(row.dataset.socho) > 0;
                }
            ).length;

            summary.textContent =
                "Tháng " + group.month + "/" + group.year +
                " có " + group.rows.length +
                " lịch khởi hành, " + available +
                " lịch còn chỗ.";
        }

        function changeMonth(monthKey, selectSchedule) {

            const group = monthGroups.find(
                function (item) {
                    return item.key === monthKey;
                }
            );

            if (!group) {
                return;
            }

            activeMonthKey = group.key;

            monthGroups.forEach(function (item) {

                const visible = item.key === monthKey;

                item.heading.hidden = !visible;
                item.grid.hidden = !visible;

                item.grid.setAttribute(
                    "aria-hidden",
                    visible ? "false" : "true"
                );
            });

            tabButtons.forEach(function (button) {

                const isActive =
                    button.dataset.monthKey === monthKey;

                button.classList.toggle(
                    "active",
                    isActive
                );

                button.setAttribute(
                    "aria-pressed",
                    isActive ? "true" : "false"
                );
            });

            updateSummary(group);

            if (!selectSchedule) {
                return;
            }

            // Khi chuyen thang, phai cap nhat IdLich.
            // Goi click tren lich that de tan dung
            // tour-detail.js va bo tinh gia hien tai.
            const rowToSelect = getAvailableRow(group);

            if (rowToSelect) {
                rowToSelect.click();
            }
        }

        /* ==============================================
           6. KEEP MONTH IN SYNC WITH SCHEDULE
           ============================================== */

        // Neu mot thao tac khac chon lich thuoc thang
        // khac, tab thang cung phai cap nhat theo.

        monthGroups.forEach(function (group) {

            group.rows.forEach(function (row) {

                row.addEventListener(
                    "click",
                    function () {

                        if (activeMonthKey !== group.key) {
                            changeMonth(group.key, false);
                        }
                    }
                );
            });
        });

        /* ==============================================
           7. INITIAL DISPLAY
           ============================================== */

        changeMonth(activeMonthKey, false);

        // Cuon tab dang chon vao vung hien thi.
        const activeTab = tabButtons.find(
            function (button) {
                return button.dataset.monthKey ===
                    activeMonthKey;
            }
        );

        if (activeTab) {
            activeTab.scrollIntoView({
                block: "nearest",
                inline: "nearest",
                behavior: "instant"
            });
        }

    }

    if (document.readyState === "loading") {
        document.addEventListener(
            "DOMContentLoaded",
            initializeDepartureMonths
        );
    } else {
        initializeDepartureMonths();
    }

})();
