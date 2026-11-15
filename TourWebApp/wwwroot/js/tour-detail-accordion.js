
/* =====================================================
   HAPPYTRIP - COMPACT DEPARTURE ACCORDION
   wwwroot/js/tour-detail-accordion.js

   Giữ nguyên cơ chế chọn lịch của tour-detail.js
   và lọc tháng của tour-detail-months.js.
   ===================================================== */

(function () {
    "use strict";

    function initTourDepartureAccordion() {
        const section = document.getElementById("lichkhoihanh");
        if (!section || section.dataset.accordionReady === "1") return;

        const rows = Array.from(
            section.querySelectorAll(".tour-calendar-grid .lich-row")
        );

        if (!rows.length) return;

        section.dataset.accordionReady = "1";

        const calcBox = section.querySelector(".tour-calc-box");
        const monthTabs = section.querySelector(".ht-month-tabs");

        const money = function (value) {
            const number = Number(value);
            if (!Number.isFinite(number)) return "Chưa cập nhật";
            return number.toLocaleString("vi-VN") + " đ";
        };

        const adultPrice = Number(calcBox?.dataset.adult);
        const childPrice = Number(calcBox?.dataset.child);
        const babyPrice = Number(calcBox?.dataset.baby);

        const childAvailable =
            !document.getElementById("childQty")?.disabled;

        const babyAvailable =
            !document.getElementById("babyQty")?.disabled;

        const durationDays = Number(
            document.getElementById("songay")?.value || 0
        );

        let expandedId = null;

        function calculateEndDate(dateText) {
            const parts = dateText.split("/").map(Number);
            if (parts.length !== 3 || durationDays <= 0) return "--";

            const date = new Date(
                parts[2],
                parts[1] - 1,
                parts[0]
            );

            if (Number.isNaN(date.getTime())) return "--";

            date.setDate(date.getDate() + durationDays - 1);

            return date.toLocaleDateString("vi-VN");
        }

        function createText(tag, className, value) {
            const el = document.createElement(tag);
            el.className = className;
            el.textContent = value;
            return el;
        }

        function createInfo(label, value) {
            const item = document.createElement("div");
            item.className = "ht-acc-info";

            item.append(
                createText("span", "ht-acc-info-label", label),
                createText("strong", "ht-acc-info-value", value)
            );

            return item;
        }

        function createDetail(row) {
            const detail = document.createElement("div");
            detail.className = "ht-acc-detail";
            detail.hidden = true;
            detail.dataset.detailFor = row.dataset.idlich || "";

            const heading = createText(
                "div",
                "ht-acc-detail-title",
                "Thông tin lịch khởi hành"
            );

            const grid = document.createElement("div");
            grid.className = "ht-acc-detail-grid";

            grid.append(
                createInfo(
                    "Ngày đi",
                    row.dataset.ngay || "--"
                ),
                createInfo(
                    "Giờ khởi hành",
                    row.dataset.gio || "--"
                ),
                createInfo(
                    "Ngày kết thúc dự kiến",
                    calculateEndDate(row.dataset.ngay || "")
                ),
                createInfo(
                    "Số chỗ còn",
                    (row.dataset.socho || "0") + " chỗ"
                )
            );

            const priceTitle = createText(
                "div",
                "ht-acc-detail-title ht-acc-price-title",
                "Giá theo đối tượng"
            );

            const prices = document.createElement("div");
            prices.className = "ht-acc-prices";

            prices.append(
                createInfo("Người lớn", money(adultPrice)),
                createInfo(
                    "Trẻ em",
                    childAvailable
                        ? money(childPrice)
                        : "Chưa cập nhật"
                ),
                createInfo(
                    "Em bé",
                    babyAvailable
                        ? money(babyPrice)
                        : "Chưa cập nhật"
                )
            );

            const note = createText(
                "p",
                "ht-acc-note",
                "Giá áp dụng theo bảng giá hiện tại của tour. " +
                "Tổng tiền chính xác được tính ở khung đặt tour."
            );

            detail.append(heading, grid, priceTitle, prices, note);

            return detail;
        }

        const items = rows.map(function (row) {
            const id = row.dataset.idlich || "";
            const detail = createDetail(row);

            const statusSpan = row.querySelector(
                ".tour-detail-status"
            );

            if (statusSpan) {
                statusSpan.classList.add("ht-acc-original-status");
            }

            const seats = Number(row.dataset.socho || 0);

            const badge = createText(
                "span",
                "ht-acc-seat-badge",
                seats <= 0
                    ? "Hết chỗ"
                    : "Còn " + seats + " chỗ"
            );

            if (seats <= 0) badge.classList.add("is-full");
            else if (seats <= 5) badge.classList.add("is-low");

            const action = document.createElement("span");
            action.className = "ht-acc-action";

            const actionLabel = createText(
                "span",
                "ht-acc-action-label",
                "Chọn"
            );

            const arrow = document.createElement("i");
            arrow.className = "fa-solid fa-chevron-down";
            arrow.setAttribute("aria-hidden", "true");

            action.append(actionLabel, arrow);

            row.append(badge, action);
            row.classList.add("ht-acc-row");

            row.setAttribute("aria-expanded", "false");

            detail.id = "ht-acc-detail-" + id;
            row.setAttribute("aria-controls", detail.id);

            row.insertAdjacentElement("afterend", detail);

            return {
                id: id,
                row: row,
                detail: detail,
                actionLabel: actionLabel
            };
        });

        function renderAccordion() {
            items.forEach(function (item) {
                const open = item.id === expandedId;

                item.detail.hidden = !open;
                item.row.classList.toggle("ht-acc-open", open);
                item.row.setAttribute(
                    "aria-expanded",
                    open ? "true" : "false"
                );

                item.actionLabel.textContent = open
                    ? "Thu gọn"
                    : Number(item.row.dataset.socho || 0) <= 0
                        ? "Xem"
                        : "Chọn";
            });
        }

        // Một lịch chỉ mở khi được bấm.
        // tour-detail.js vẫn nhận click để cập nhật IdLich.
        items.forEach(function (item) {
            item.row.addEventListener("click", function () {
                expandedId =
                    expandedId === item.id
                        ? null
                        : item.id;

                renderAccordion();
            });
        });

        // Khi chuyển tháng, đóng mục cũ.
        // tour-detail-months.js sẽ chọn lịch thuộc tháng mới.
        if (monthTabs) {
            monthTabs.addEventListener(
                "click",
                function (event) {
                    if (!event.target.closest(".ht-month-tab")) return;

                    // Nếu một lịch được tự động chọn,
                    // mục đó sẽ mở nhờ sự kiện click có sẵn.
                    expandedId = null;
                    renderAccordion();
                },
                true
            );
        }

        // Khi tải trang, lịch mặc định đã được chọn
        // để tính tiền nhưng chưa cần mở khung dài.
        renderAccordion();
    }

    if (document.readyState === "loading") {
        document.addEventListener(
            "DOMContentLoaded",
            initTourDepartureAccordion
        );
    } else {
        initTourDepartureAccordion();
    }
})();