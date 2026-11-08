const rangeMin = document.querySelector(".range-min");
const rangeMax = document.querySelector(".range-max");
const track = document.querySelector(".slider-track");

const inputMin = document.getElementById("giaminInput");
const inputMax = document.getElementById("giamaxInput");
const filterForm = document.getElementById("filterForm");

function onlyDigits(value) {
    return (value || "").toString().replace(/\D/g, "");
}

function asNumber(value, fallback = 0) {
    const n = parseInt(value, 10);
    return Number.isNaN(n) ? fallback : n;
}

function updateSlider() {
    if (!rangeMin || !rangeMax || !track || !inputMin || !inputMax) return;

    let minVal = asNumber(rangeMin.value, 0);
    let maxVal = asNumber(rangeMax.value, asNumber(rangeMax.max, 20000000));

    if (minVal > maxVal) minVal = maxVal;
    if (maxVal < minVal) maxVal = minVal;

    rangeMin.value = String(minVal);
    rangeMax.value = String(maxVal);

    inputMin.value = minVal.toLocaleString("vi-VN") + " d";
    inputMax.value = maxVal.toLocaleString("vi-VN") + " d";

    const maxLimit = asNumber(rangeMax.max, 20000000);
    const minPercent = (minVal / maxLimit) * 100;
    const maxPercent = (maxVal / maxLimit) * 100;

    track.style.background = `linear-gradient(
        to right,
        #ddd ${minPercent}%,
        #0d6efd ${minPercent}%,
        #0d6efd ${maxPercent}%,
        #ddd ${maxPercent}%
    )`;
}

function initPriceRange() {
    if (!rangeMin || !rangeMax || !inputMin || !inputMax) return;

    // Khoi tao tu query (neu co)
    const queryMin = onlyDigits(inputMin.value);
    const queryMax = onlyDigits(inputMax.value);

    if (queryMin) rangeMin.value = queryMin;
    if (queryMax) rangeMax.value = queryMax;

    rangeMin.addEventListener("input", () => {
        if (+rangeMin.value > +rangeMax.value) rangeMin.value = rangeMax.value;
        updateSlider();
    });

    rangeMax.addEventListener("input", () => {
        if (+rangeMax.value < +rangeMin.value) rangeMax.value = rangeMin.value;
        updateSlider();
    });

    updateSlider();

    if (filterForm) {
        filterForm.addEventListener("submit", () => {
            inputMin.value = onlyDigits(inputMin.value);
            inputMax.value = onlyDigits(inputMax.value);
        });
    }
}

function initAutoCompleteDiaDiem() {
    const pairs = [
        {
            input: document.getElementById("diadiemInputTop"),
            box: document.getElementById("goiYBoxTop")
        },
        {
            input: document.getElementById("diadiemInput"),
            box: document.getElementById("goiYBox")
        }
    ].filter(p => p.input && p.box);

    pairs.forEach(({ input, box }) => {
        let timer = null;
        let controller = null;

        input.setAttribute("autocomplete", "off");

        function hide() {
            box.style.display = "none";
        }

        function showMessage(message) {
            box.replaceChildren();

            const item = document.createElement("div");
            item.className = "ht-search-message";
            item.textContent = message;

            box.appendChild(item);
            box.style.display = "block";
        }

        function renderResults(items) {
            box.replaceChildren();

            if (!items.length) {
                showMessage("Không tìm thấy tour phù hợp.");
                return;
            }

            items.forEach(tour => {
                const link = document.createElement("a");

                link.className = "ht-live-tour-item";
                link.href = tour.url ||
                    `/Tour/ChiTiet/${tour.idTour}`;

                const icon = document.createElement("span");
                icon.className = "ht-live-tour-icon";

                const iconElement = document.createElement("i");
                iconElement.className =
                    "fa-solid fa-location-dot";
                icon.appendChild(iconElement);

                const info = document.createElement("span");
                info.className = "ht-live-tour-info";

                const name = document.createElement("strong");
                name.textContent = tour.tenTour || "Tour";

                const destination = document.createElement("small");
                destination.textContent =
                    tour.diaDiem || "Chưa cập nhật điểm đến";

                info.append(name, destination);

                const price = document.createElement("span");
                price.className = "ht-live-tour-price";
                price.textContent = tour.gia || "Liên hệ";

                link.append(icon, info, price);
                box.appendChild(link);
            });

            const footer = document.createElement("div");
            footer.className = "ht-live-tour-footer";
            footer.textContent =
                "Chọn một tour để xem chi tiết. " +
                "Nhấn Enter để lọc toàn bộ danh sách.";

            box.appendChild(footer);
            box.style.display = "block";
        }

        input.addEventListener("input", () => {
            clearTimeout(timer);
            controller?.abort();

            const keyword = input.value.trim();

            if (!keyword) {
                hide();
                return;
            }

            showMessage("Đang tìm tour...");

            timer = setTimeout(async () => {
                controller = new AbortController();

                try {
                    const response = await fetch(
                        `/Tour/GoiYTour?keyword=${encodeURIComponent(keyword)}`,
                        {
                            signal: controller.signal,
                            headers: {
                                "Accept": "application/json"
                            }
                        }
                    );

                    if (!response.ok) {
                        throw new Error("Không lấy được dữ liệu");
                    }

                    const data = await response.json();

                    // Tránh hiển thị kết quả của từ khóa cũ
                    if (input.value.trim() !== keyword) {
                        return;
                    }

                    renderResults(
                        Array.isArray(data) ? data : []
                    );
                } catch (error) {
                    if (error.name !== "AbortError") {
                        showMessage(
                            "Không thể tải gợi ý. Vui lòng thử lại."
                        );
                    }
                }
            }, 180);
        });

        input.addEventListener("keydown", event => {
            if (event.key === "Escape") {
                hide();
            }
        });

        document.addEventListener("click", event => {
            if (
                event.target !== input &&
                !box.contains(event.target)
            ) {
                hide();
            }
        });
    });
}

function initCustomSelects() {
    const selects = document.querySelectorAll(
        "#quickFilterForm select[name='phuongtien'], #quickFilterForm select[name='idLoaiTour'], #filterForm select[name='phuongtien'], #filterForm select[name='idLoaiTour']"
    );

    if (!selects.length) return;

    let openDropdown = null;

    function closeDropdown(dropdown) {
        if (!dropdown) return;
        dropdown.classList.remove("is-open");
        const panel = dropdown.querySelector(".tour-custom-select__menu");
        if (panel) panel.hidden = true;
        openDropdown = openDropdown === dropdown ? null : openDropdown;
    }

    function openDropdownMenu(dropdown) {
        if (!dropdown) return;
        if (openDropdown && openDropdown !== dropdown) closeDropdown(openDropdown);
        dropdown.classList.add("is-open");
        const panel = dropdown.querySelector(".tour-custom-select__menu");
        if (panel) panel.hidden = false;
        openDropdown = dropdown;
    }

    selects.forEach((select) => {
        if (select.dataset.customized === "1") return;
        select.dataset.customized = "1";
        select.classList.add("tour-native-select");

        const wrapper = document.createElement("div");
        wrapper.className = "tour-custom-select";

        const button = document.createElement("button");
        button.type = "button";
        button.className = "tour-custom-select__trigger";
        button.setAttribute("aria-haspopup", "listbox");
        button.setAttribute("aria-expanded", "false");

        const label = document.createElement("span");
        label.className = "tour-custom-select__label";
        button.appendChild(label);

        const arrow = document.createElement("span");
        arrow.className = "tour-custom-select__arrow";
        button.appendChild(arrow);

        const menu = document.createElement("div");
        menu.className = "tour-custom-select__menu";
        menu.setAttribute("role", "listbox");
        menu.hidden = true;

        function syncFromSelect() {
            const selectedOption = select.options[select.selectedIndex];
            label.textContent = selectedOption ? selectedOption.textContent : "Chọn";

            menu.querySelectorAll(".tour-custom-select__option").forEach((optEl) => {
                const isActive = optEl.dataset.value === select.value;
                optEl.classList.toggle("is-active", isActive);
                optEl.setAttribute("aria-selected", isActive ? "true" : "false");
            });
        }

        Array.from(select.options).forEach((opt) => {
            const optEl = document.createElement("button");
            optEl.type = "button";
            optEl.className = "tour-custom-select__option";
            optEl.dataset.value = opt.value;
            optEl.setAttribute("role", "option");
            optEl.textContent = opt.textContent;

            optEl.addEventListener("click", () => {
                if (select.value !== opt.value) {
                    select.value = opt.value;
                    select.dispatchEvent(new Event("change", { bubbles: true }));
                }
                syncFromSelect();
                closeDropdown(wrapper);
            });

            menu.appendChild(optEl);
        });

        button.addEventListener("click", () => {
            const isOpen = wrapper.classList.contains("is-open");
            if (isOpen) {
                closeDropdown(wrapper);
                button.setAttribute("aria-expanded", "false");
            } else {
                openDropdownMenu(wrapper);
                button.setAttribute("aria-expanded", "true");
            }
        });

        button.addEventListener("keydown", (e) => {
            if (e.key === "ArrowDown" || e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                openDropdownMenu(wrapper);
                button.setAttribute("aria-expanded", "true");
            }
            if (e.key === "Escape") {
                closeDropdown(wrapper);
                button.setAttribute("aria-expanded", "false");
            }
        });

        select.addEventListener("change", syncFromSelect);

        select.parentNode.insertBefore(wrapper, select);
        wrapper.appendChild(button);
        wrapper.appendChild(menu);
        wrapper.appendChild(select);

        syncFromSelect();
    });

    document.addEventListener("click", (e) => {
        if (!openDropdown) return;
        if (!openDropdown.contains(e.target)) {
            closeDropdown(openDropdown);
        }
    });

    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape" && openDropdown) {
            closeDropdown(openDropdown);
        }
    });
}

initPriceRange();
// initAutoCompleteDiaDiem();
initCustomSelects();
