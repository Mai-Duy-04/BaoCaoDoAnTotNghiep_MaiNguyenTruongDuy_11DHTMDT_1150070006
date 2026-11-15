
/* =====================================================
   HAPPYTRIP - OVERVIEW & ITINERARY COLLAPSE
   File: wwwroot/js/tour-detail-content-fix.js

   Khong can thay doi Controller / SQL.
   Khong can sua luong dat tour.
   ===================================================== */

(function () {
    "use strict";

    function initContentCollapse() {

        /* ===========================================
           1. TONG QUAN: XEM THEM / THU GON
           =========================================== */

        const overviewSection =
            document.getElementById("tongquan");

        const overviewText = overviewSection
            ?.querySelector(".tour-detail-overview-text");

        if (
            overviewText &&
            overviewText.dataset.collapseReady !== "1"
        ) {
            overviewText.dataset.collapseReady = "1";

            // Mo ta duoc luu dang text trong SQL.
            // Loai bo ky tu ** Markdown dang hien tho.
            const original = overviewText.textContent || "";

            const cleanText = original
                .replace(/\*\*/g, "")
                .replace(/\r\n/g, "\n")
                .replace(/[ \t]+\n/g, "\n")
                .replace(/\n[ \t]*\n[ \t]*\n+/g, "\n\n")
                .trim();

            overviewText.textContent = cleanText;

            if (cleanText.length > 250) {
                overviewText.id = "htOverviewDescription";

                overviewText.classList.add(
                    "ht-content-collapsible",
                    "ht-content-collapsed"
                );

                const button = document.createElement(
                    "button"
                );

                button.type = "button";
                button.className = "ht-content-toggle";

                button.setAttribute(
                    "aria-controls",
                    overviewText.id
                );

                button.setAttribute(
                    "aria-expanded",
                    "false"
                );

                const label = document.createElement(
                    "span"
                );

                const arrow = document.createElement("i");

                arrow.className =
                    "fa-solid fa-chevron-down";

                arrow.setAttribute(
                    "aria-hidden",
                    "true"
                );

                label.textContent = "Xem thêm nội dung";

                button.append(label, arrow);

                overviewText.insertAdjacentElement(
                    "afterend",
                    button
                );

                button.addEventListener(
                    "click",
                    function () {

                        const isExpanded =
                            button.getAttribute(
                                "aria-expanded"
                            ) === "true";

                        const expand = !isExpanded;

                        overviewText.classList.toggle(
                            "ht-content-collapsed",
                            !expand
                        );

                        button.setAttribute(
                            "aria-expanded",
                            String(expand)
                        );

                        label.textContent = expand
                            ? "Thu gọn nội dung"
                            : "Xem thêm nội dung";
                    }
                );
            }
        }

        /* ===========================================
           2. HANH TRINH: MO TA MO RONG KHI CAN
           =========================================== */

        const itinerarySection =
            document.getElementById("hanhtrinh");

        const prose = itinerarySection
            ?.querySelector(".tour-detail-prose");

        if (
            prose &&
            prose.dataset.collapseReady !== "1"
        ) {
            prose.dataset.collapseReady = "1";

            const paragraph = prose.querySelector("p");

            if (paragraph) {
                const original = paragraph.textContent || "";

                paragraph.textContent = original
                    .replace(/\*\*/g, "")
                    .replace(/\r\n/g, "\n")
                    .replace(/[ \t]+\n/g, "\n")
                    .replace(/\n[ \t]*\n[ \t]*\n+/g, "\n\n")
                    .trim();
            }

            const proseId = "htItineraryIntroduction";

            prose.id = proseId;
            prose.classList.add("ht-prose-hidden");

            const reveal = document.createElement("button");
            reveal.type = "button";
            reveal.className = "ht-prose-reveal";

            reveal.setAttribute(
                "aria-controls",
                proseId
            );

            reveal.setAttribute(
                "aria-expanded",
                "false"
            );

            const label = document.createElement("span");
            label.textContent =
                "Xem phần giới thiệu hành trình";

            const arrow = document.createElement("i");
            arrow.className =
                "fa-solid fa-chevron-down";

            arrow.setAttribute(
                "aria-hidden",
                "true"
            );

            reveal.append(label, arrow);

            prose.parentNode.insertBefore(
                reveal,
                prose
            );

            reveal.addEventListener("click", function () {

                const isExpanded =
                    reveal.getAttribute(
                        "aria-expanded"
                    ) === "true";

                const expand = !isExpanded;

                prose.classList.toggle(
                    "ht-prose-hidden",
                    !expand
                );

                reveal.setAttribute(
                    "aria-expanded",
                    String(expand)
                );

                label.textContent = expand
                    ? "Thu gọn phần giới thiệu"
                    : "Xem phần giới thiệu hành trình";
            });
        }

        /* ===========================================
           3. CAC NGAY HANH TRINH MAC DINH THU GON
           Bootstrap collapse hien tai van hoat dong.
           =========================================== */

        if (
            itinerarySection &&
            itinerarySection.dataset.daysReady !== "1"
        ) {
            itinerarySection.dataset.daysReady = "1";

            const dayButtons =
                itinerarySection.querySelectorAll(
                    ".tour-detail-day-toggle"
                );

            dayButtons.forEach(function (button) {
                const targetSelector = button.getAttribute(
                    "data-bs-target"
                );

                if (!targetSelector) return;

                const target = itinerarySection
                    .querySelector(targetSelector);

                if (!target) return;

                // Chuyen ve trang thai dong ban dau.
                target.classList.remove("show");

                button.classList.add("collapsed");

                button.setAttribute(
                    "aria-expanded",
                    "false"
                );
            });
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener(
            "DOMContentLoaded",
            initContentCollapse
        );
    } else {
        initContentCollapse();
    }
})();
