
/* =========================================================
   HAPPYTRIP - TOUR DETAIL
   File: wwwroot/js/tour-detail.js

   UI: Tabs, Gallery, Schedule, Calculator, Comments
   Booking: Giữ nguyên đường dẫn và tham số hiện tại
   ========================================================= */

document.addEventListener("DOMContentLoaded", function () {
    initTabs();
    initScheduleSelection();
    initScrollButtons();
    initGallery();
    initCalculator();
    initSortComments();
    initAutoGrow();
});

/* =========================================================
   1. TAB NAVIGATION
   ========================================================= */

function getTourScrollOffset() {
    const navbar = document.querySelector(".travel-navbar-wrap");
    const tabBar = document.querySelector(".tour-detail-tab-wrap");

    const navbarHeight = navbar
        ? navbar.getBoundingClientRect().height
        : 82;

    const tabHeight = tabBar
        ? tabBar.getBoundingClientRect().height
        : 58;

    return navbarHeight + tabHeight + 18;
}

function scrollToTourSection(targetId) {
    const target = document.getElementById(targetId);

    if (!target) return;

    const offset = getTourScrollOffset();

    const top =
        target.getBoundingClientRect().top +
        window.scrollY -
        offset;

    window.scrollTo({
        top: Math.max(0, top),
        behavior: window.matchMedia(
            "(prefers-reduced-motion: reduce)"
        ).matches ? "auto" : "smooth"
    });
}

function initTabs() {
    const tabs = Array.from(
        document.querySelectorAll(".tour-tab-nav li[data-target]")
    );

    if (!tabs.length) return;

    const sections = tabs
        .map(function (tab) {
            return {
                tab: tab,
                id: tab.dataset.target,
                element: document.getElementById(tab.dataset.target)
            };
        })
        .filter(function (item) {
            return item.element !== null;
        });

    if (!sections.length) return;

    let activeId = "";
    let scrollingFromClick = false;
    let scrollResetTimer = null;
    let ticking = false;

    function setActiveTab(targetId) {
        if (!targetId || activeId === targetId) return;

        activeId = targetId;

        tabs.forEach(function (tab) {
            const isActive = tab.dataset.target === targetId;

            tab.classList.toggle("active", isActive);

            if (isActive) {
                tab.setAttribute("aria-current", "true");
            } else {
                tab.removeAttribute("aria-current");
            }
        });
    }

    function updateActiveOnScroll() {
        if (scrollingFromClick) return;

        const offset = getTourScrollOffset() + 25;

        let currentId = sections[0].id;

        sections.forEach(function (item) {
            const rect = item.element.getBoundingClientRect();

            if (rect.top <= offset) {
                currentId = item.id;
            }
        });

        setActiveTab(currentId);
    }

    tabs.forEach(function (tab) {
        tab.setAttribute("role", "button");
        tab.setAttribute("tabindex", "0");

        function activate() {
            const targetId = tab.dataset.target;

            if (!targetId) return;

            setActiveTab(targetId);

            scrollingFromClick = true;

            if (scrollResetTimer) {
                clearTimeout(scrollResetTimer);
            }

            scrollToTourSection(targetId);

            scrollResetTimer = setTimeout(function () {
                scrollingFromClick = false;
                updateActiveOnScroll();
            }, 850);
        }

        tab.addEventListener("click", activate);

        tab.addEventListener("keydown", function (event) {
            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                activate();
            }
        });
    });

    window.addEventListener(
        "scroll",
        function () {
            if (ticking) return;

            ticking = true;

            window.requestAnimationFrame(function () {
                updateActiveOnScroll();
                ticking = false;
            });
        },
        { passive: true }
    );

    window.addEventListener("resize", updateActiveOnScroll);

    updateActiveOnScroll();
}

/* =========================================================
   2. SCROLL BUTTONS
   ========================================================= */

function initScrollButtons() {
    const buttons = document.querySelectorAll(".scroll-btn");

    buttons.forEach(function (button) {
        button.addEventListener("click", function () {
            const targetId = button.dataset.target;

            if (!targetId) return;

            const tab = document.querySelector(
                '.tour-tab-nav li[data-target="' +
                targetId +
                '"]'
            );

            if (tab) {
                tab.click();
            } else {
                scrollToTourSection(targetId);
            }
        });
    });
}

/* =========================================================
   3. IMAGE GALLERY
   ========================================================= */

function initGallery() {
    const mainImg = document.getElementById("main-img");

    const thumbs = Array.from(
        document.querySelectorAll(".thumb-img")
    );

    const counter = document.querySelector(
        ".tour-detail-gallery-count"
    );

    if (!mainImg || !thumbs.length) return;

    let currentIndex = thumbs.findIndex(function (thumb) {
        return thumb.classList.contains("is-active");
    });

    if (currentIndex < 0) {
        currentIndex = 0;
    }

    function showImage(index) {
        const total = thumbs.length;

        if (!total) return;

        currentIndex = (index + total) % total;

        const selectedThumb = thumbs[currentIndex];

        mainImg.src = selectedThumb.src;

        mainImg.alt =
            selectedThumb.alt || "Ảnh chi tiết tour";

        thumbs.forEach(function (thumb, i) {
            thumb.classList.toggle(
                "is-active",
                i === currentIndex
            );

            const button = thumb.closest("button");

            if (button) {
                button.setAttribute(
                    "aria-pressed",
                    i === currentIndex ? "true" : "false"
                );
            }
        });

        if (counter) {
            counter.textContent =
                (currentIndex + 1) + " / " + total;
        }
    }

    thumbs.forEach(function (thumb, index) {
        const button = thumb.closest("button");

        if (button) {
            button.addEventListener("click", function () {
                showImage(index);
            });
        } else {
            thumb.addEventListener("click", function () {
                showImage(index);
            });
        }
    });

    const prevButton = document.querySelector(
        ".tour-detail-gallery-arrow.prev"
    );

    const nextButton = document.querySelector(
        ".tour-detail-gallery-arrow.next"
    );

    if (prevButton) {
        prevButton.addEventListener("click", function () {
            showImage(currentIndex - 1);
        });
    }

    if (nextButton) {
        nextButton.addEventListener("click", function () {
            showImage(currentIndex + 1);
        });
    }

    window.tourGalleryShowImage = showImage;

    showImage(currentIndex);
}

/*
   Giữ hàm global vì Razor hiện tại đang gọi:
   onclick="changeMainImage(this.src)"
*/

function changeMainImage(src) {
    const mainImg = document.getElementById("main-img");

    if (!mainImg) return;

    const thumbs = Array.from(
        document.querySelectorAll(".thumb-img")
    );

    const index = thumbs.findIndex(function (thumb) {
        return (
            thumb.src === src ||
            thumb.currentSrc === src ||
            thumb.getAttribute("src") === src
        );
    });

    if (
        index >= 0 &&
        typeof window.tourGalleryShowImage === "function"
    ) {
        window.tourGalleryShowImage(index);
        return;
    }

    mainImg.src = src;

    thumbs.forEach(function (thumb) {
        thumb.classList.toggle(
            "is-active",
            thumb.src === src
        );
    });
}

/* =========================================================
   4. SCHEDULE SELECTION
   ========================================================= */

function initScheduleSelection() {
    const rows = Array.from(
        document.querySelectorAll(".lich-row")
    );

    if (!rows.length) return;

    const selectedLichInput =
        document.getElementById("selectedLich");

    const soNgay = parseInt(
        document.getElementById("songay")?.value || "0",
        10
    );

    function updateKetThuc(ngayKhoiHanh) {
        const ketThucEl =
            document.getElementById("thoigian_ketthuc");

        if (!ketThucEl) return;

        if (!ngayKhoiHanh || soNgay <= 0) {
            ketThucEl.textContent = "--";
            return;
        }

        const parts = ngayKhoiHanh.split("/");

        if (parts.length !== 3) {
            ketThucEl.textContent = "--";
            return;
        }

        const start = new Date(
            Number(parts[2]),
            Number(parts[1]) - 1,
            Number(parts[0])
        );

        if (Number.isNaN(start.getTime())) {
            ketThucEl.textContent = "--";
            return;
        }

        start.setDate(
            start.getDate() + soNgay - 1
        );

        ketThucEl.textContent =
            start.toLocaleDateString("vi-VN");
    }

    function applyRow(row) {
        rows.forEach(function (item) {
            const selected = item === row;

            item.classList.toggle(
                "lich-selected",
                selected
            );

            item.setAttribute(
                "aria-pressed",
                selected ? "true" : "false"
            );
        });

        if (selectedLichInput) {
            selectedLichInput.value =
                row.dataset.idlich || "";
        }

        const ngay = row.dataset.ngay || "";
        const gio = row.dataset.gio || "--";
        const trangThai =
            row.dataset.trangthai || "--";
        const soCho =
            row.dataset.socho || "0";

        const ngayEl =
            document.getElementById("thoigian_ngay");

        const gioEl =
            document.getElementById("thoigian_gio");

        const trangThaiEl =
            document.getElementById("thoigian_trangthai");

        const soChoEl =
            document.getElementById("thoigian_socho");

        if (ngayEl) ngayEl.textContent = ngay;
        if (gioEl) gioEl.textContent = gio;

        if (trangThaiEl) {
            trangThaiEl.textContent = trangThai;
        }

        if (soChoEl) {
            soChoEl.textContent = soCho;
        }

        const calcBox = document.querySelector(
            ".tour-calc-box"
        );

        if (calcBox) {
            calcBox.dataset.remaining = soCho;
        }

        const waitlistScheduleId =
            document.getElementById("waitlistScheduleId");

        if (waitlistScheduleId) {
            waitlistScheduleId.value =
                row.dataset.idlich || "";
        }

        updateKetThuc(ngay);

        updateSeatAvailability();
    }

    rows.forEach(function (row) {
        row.addEventListener("click", function () {
            applyRow(row);
        });
    });

    const firstSelected =
        rows.find(function (row) {
            return row.classList.contains(
                "lich-selected"
            );
        }) || rows[0];

    if (firstSelected) {
        applyRow(firstSelected);
    }
}

/* =========================================================
   5. PRICE CALCULATOR
   ========================================================= */

function tourSafeInt(value) {
    const parsed = parseInt(value || "0", 10);

    return Number.isFinite(parsed)
        ? Math.max(0, parsed)
        : 0;
}

function tourFormatMoney(value) {
    const safeValue = Number.isFinite(value)
        ? value
        : 0;

    return safeValue.toLocaleString("vi-VN") + " đ";
}

function initCalculator() {
    const calcBox = document.querySelector(
        ".tour-calc-box"
    );

    if (!calcBox) return;

    const adultPrice =
        parseFloat(calcBox.dataset.adult || "0") || 0;

    const childPrice =
        parseFloat(calcBox.dataset.child || "0") || 0;

    const babyPrice =
        parseFloat(calcBox.dataset.baby || "0") || 0;

    const adultInput =
        document.getElementById("adultQty");

    const childInput =
        document.getElementById("childQty");

    const babyInput =
        document.getElementById("babyQty");

    const totalAmountEl =
        document.getElementById("totalAmount");

    const totalGuestEl =
        document.getElementById("totalGuest");

    if (!adultInput || !childInput || !babyInput) {
        return;
    }

    function recalc() {
        const adult = tourSafeInt(adultInput.value);
        const child = tourSafeInt(childInput.value);
        const baby = tourSafeInt(babyInput.value);

        adultInput.value = String(adult);
        childInput.value = String(child);
        babyInput.value = String(baby);

        const total =
            adult * adultPrice +
            child * childPrice +
            baby * babyPrice;

        const guest = adult + child + baby;

        if (totalAmountEl) {
            totalAmountEl.textContent =
                tourFormatMoney(total);
        }

        if (totalGuestEl) {
            totalGuestEl.textContent =
                String(guest);
        }

        const waitlistGuestCount =
            document.getElementById("waitlistGuestCount");

        if (waitlistGuestCount) {
            waitlistGuestCount.value =
                String(Math.max(1, guest));
        }

        updateSeatAvailability();
    }

    function bindQty(minusId, inputEl, plusId) {
        const minusBtn =
            document.getElementById(minusId);

        const plusBtn =
            document.getElementById(plusId);

        if (minusBtn) {
            minusBtn.addEventListener(
                "click",
                function (event) {
                    event.preventDefault();

                    inputEl.value = String(
                        Math.max(
                            tourSafeInt(inputEl.value) - 1,
                            0
                        )
                    );

                    recalc();
                }
            );
        }

        if (plusBtn) {
            plusBtn.addEventListener(
                "click",
                function (event) {
                    event.preventDefault();

                    inputEl.value = String(
                        tourSafeInt(inputEl.value) + 1
                    );

                    recalc();
                }
            );
        }

        inputEl.addEventListener("change", recalc);
        inputEl.addEventListener("input", recalc);
    }

    bindQty(
        "adultMinus",
        adultInput,
        "adultPlus"
    );

    bindQty(
        "childMinus",
        childInput,
        "childPlus"
    );

    bindQty(
        "babyMinus",
        babyInput,
        "babyPlus"
    );

    recalc();
}

/* =========================================================
   6. LIVE SEAT AVAILABILITY
   ========================================================= */

function updateSeatAvailability() {
    const calcBox = document.querySelector(
        ".tour-calc-box"
    );

    if (!calcBox) return;

    const remaining = tourSafeInt(
        calcBox.dataset.remaining || "0"
    );

    const adult = tourSafeInt(
        document.getElementById("adultQty")?.value
    );

    const child = tourSafeInt(
        document.getElementById("childQty")?.value
    );

    const baby = tourSafeInt(
        document.getElementById("babyQty")?.value
    );

    const guests = adult + child + baby;

    const full = remaining <= 0;

    const insufficient =
        !full && guests > remaining;

    const empty = guests <= 0;

    const bookingButton =
        document.getElementById("bookingButton");

    const waitlistForm =
        document.getElementById("waitlistForm");

    const warning =
        document.getElementById("seatWarning");

    if (bookingButton) {
        bookingButton.disabled =
            full || insufficient || empty;
    }

    if (waitlistForm) {
        waitlistForm.classList.toggle(
            "d-none",
            !full
        );
    }

    if (warning) {
        const shouldShow =
            full ||
            insufficient ||
            empty ||
            remaining <= 5;

        warning.classList.toggle(
            "d-none",
            !shouldShow
        );

        if (empty) {
            warning.textContent =
                "Vui lòng chọn ít nhất một khách.";
        } else if (full) {
            warning.textContent =
                "Lịch đã hết chỗ. Bạn có thể tham gia danh sách chờ.";
        } else if (insufficient) {
            warning.textContent =
                "Chỉ còn " +
                remaining +
                " chỗ, không đủ cho " +
                guests +
                " khách.";
        } else if (remaining <= 5) {
            warning.textContent =
                "Sắp hết chỗ — lịch này chỉ còn " +
                remaining +
                " chỗ.";
        } else {
            warning.textContent = "";
        }
    }
}

/* =========================================================
   7. BOOKING - GIỮ NGUYÊN LUỒNG HIỆN TẠI
   ========================================================= */

function goToBooking() {
    const selectedLichInput =
        document.getElementById("selectedLich");

    const calcBox = document.querySelector(
        ".tour-calc-box"
    );

    if (!selectedLichInput || !calcBox) {
        return;
    }

    const idLich = selectedLichInput.value;
    const idTour = calcBox.dataset.idtour;
    const user = calcBox.dataset.user;

    const adult = tourSafeInt(
        document.getElementById("adultQty")?.value
    );

    const child = tourSafeInt(
        document.getElementById("childQty")?.value
    );

    const baby = tourSafeInt(
        document.getElementById("babyQty")?.value
    );

    const adultPrice =
        parseFloat(calcBox.dataset.adult || "0") || 0;

    const childPrice =
        parseFloat(calcBox.dataset.child || "0") || 0;

    const babyPrice =
        parseFloat(calcBox.dataset.baby || "0") || 0;

    const total =
        adult * adultPrice +
        child * childPrice +
        baby * babyPrice;

    const remaining = tourSafeInt(
        calcBox.dataset.remaining || "0"
    );

    const guests = adult + child + baby;

    if (!idLich) {
        alert("Vui lòng chọn lịch khởi hành!");
        return;
    }

    if (guests <= 0) {
        alert("Vui lòng chọn ít nhất một khách.");
        return;
    }

    if (remaining <= 0 || guests > remaining) {
        updateSeatAvailability();
        return;
    }

    /*
       Không đổi tên các tham số.
       Trang DatTour/NhapThongTin đang nhận:
       idTour, idLich, adult, child, baby, total.
    */

    const bookingQuery =
        "idTour=" + encodeURIComponent(idTour || "") +
        "&idLich=" + encodeURIComponent(idLich) +
        "&adult=" + encodeURIComponent(String(adult)) +
        "&child=" + encodeURIComponent(String(child)) +
        "&baby=" + encodeURIComponent(String(baby)) +
        "&total=" + encodeURIComponent(String(total));

    const bookingUrl =
        "/DatTour/NhapThongTin?" + bookingQuery;

    if (!user) {
        const loginUrl =
            "/TaiKhoan/DangNhap?returnUrl=" +
            encodeURIComponent(bookingUrl);

        const loginBtn =
            document.getElementById("btnGoLogin");

        if (loginBtn) {
            loginBtn.href = loginUrl;
        }

        if (window.bootstrap) {
            const modalEl =
                document.getElementById("loginWarningModal");

            if (modalEl) {
                const loginPopup = new bootstrap.Modal(
                    modalEl
                );

                loginPopup.show();
                return;
            }
        }

        window.location.href = loginUrl;
        return;
    }

    window.location.href = bookingUrl;
}

/* =========================================================
   8. COMMENT SORTING
   ========================================================= */

function initSortComments() {
    const sort =
        document.getElementById("sortComment");

    if (!sort) return;

    sort.addEventListener("change", function () {
        const url = new URL(
            window.location.href
        );

        url.searchParams.set(
            "sortCmt",
            sort.value
        );

        url.hash = "binhluan";

        window.location.href = url.toString();
    });
}

/* =========================================================
   9. AUTO GROW COMMENT TEXTAREA
   ========================================================= */

function initAutoGrow() {
    const textareas =
        document.querySelectorAll(".auto-grow");

    textareas.forEach(function (textarea) {
        textarea.addEventListener(
            "input",
            function () {
                textarea.style.height = "auto";

                textarea.style.height =
                    textarea.scrollHeight + "px";
            }
        );
    });
}
