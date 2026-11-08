/* ==========================================================
   HAPPYTRIP - TRIPI AI CHAT
   File: wwwroot/js/tripi-chat.js
   Gemini Chat + Matching Score + RAG
   Luu hoi thoai + chon tour + khoi phuc tour
   ========================================================== */
(() => {
    "use strict";

    const page = document.getElementById("tripiChatPage");
    if (!page) return;

    const $ = id => document.getElementById(id);

    const ui = {
        form: $("tripiChatForm"),
        input: $("tripiMessageInput"),
        send: $("tripiSendButton"),
        messages: $("tripiMessages"),
        typing: $("tripiTyping"),
        welcome: $("tripiWelcome"),
        newChat: $("tripiNewChat"),
        error: $("tripiChatError"),
        count: $("tripiCharacterCount"),
        results: $("tripiTourResults"),
        searching: $("tripiWorldSearching"),
        destination: $("tripiPreferenceDestination"),
        interests: $("tripiPreferenceInterests"),
        budget: $("tripiPreferenceBudget"),
        days: $("tripiPreferenceDays")
    };

    const required = [
        "form", "input", "send",
        "messages", "typing", "results"
    ];

    if (required.some(key => !ui[key])) {
        console.error("[Tripi] Thieu phan tu giao dien.");
        return;
    }

    const session = window.TripiSession;

    if (
        !session ||
        ["read", "patch", "clear", "getApiHistory"].some(
            key => typeof session[key] !== "function"
        )
    ) {
        console.error("[Tripi] Thieu tripi-session.js");
        return;
    }

    const API_URL = "/api/tripi-chat/gui-tin-nhan";
    const FOX_IMAGE = "/img/tripi/tripi-fox.png";
    const MAX_LENGTH = 1000;
    const MAX_HISTORY = 12;

    const token = ui.form.querySelector(
        'input[name="__RequestVerificationToken"]'
    );

    const greeting = ui.messages.querySelector(
        ".tripi-message-assistant"
    );

    let sending = false;
    let requestController = null;
    let conversationVersion = 0;
    let animationTimer = null;
    let activeTourId = null;
    let currentTours = [];
    
    // ======================================================
    // TRIPI AI - DYNAMIC WORKSPACE
    // Home -> Chat -> Results
    // Chi dieu khien giao dien, khong thay doi API.
    // ======================================================

    
    function setWorkspaceMode(mode) {
        const validModes = ["home", "chat", "results"];

        const nextMode = validModes.includes(mode)
            ? mode
            : "home";

        const chatPanel = page.querySelector(
            ".tripi-world-chat"
        );

        const composer = page.querySelector(
            ".tripi-chat-composer"
        );

        const welcomeHeading = ui.welcome?.querySelector(
            ".tripi-welcome-heading"
        );

        // Man hinh chao: dua o nhap ngay sau tieu de.
        // Chat/Results: tra o nhap ve cuoi khung chat.
        // Chi di chuyen DOM, KHONG tao lai form.
        if (composer && chatPanel) {
            if (
                nextMode === "home" &&
                ui.welcome &&
                welcomeHeading
            ) {
                welcomeHeading.insertAdjacentElement(
                    "afterend",
                    composer
                );
            } else if (
                composer.parentElement !== chatPanel
            ) {
                chatPanel.appendChild(composer);
            }
        }

        page.classList.remove(
            "tripi-mode-home",
            "tripi-mode-chat",
            "tripi-mode-results"
        );

        page.classList.add(`tripi-mode-${nextMode}`);

        const resultsPanel = page.querySelector(
            ".tripi-world-results"
        );

        if (resultsPanel) {
            resultsPanel.setAttribute(
                "aria-hidden",
                String(nextMode !== "results")
            );
        }
    }

    // ======================================================
    // BASIC HELPERS
    // ======================================================

    function el(tag, className = "", text = null) {
        const node = document.createElement(tag);

        if (className) {
            node.className = className;
        }

        if (text !== null && text !== undefined) {
            node.textContent = String(text);
        }

        return node;
    }

    function icon(name) {
        const node = el("i", name);
        node.setAttribute("aria-hidden", "true");
        return node;
    }

    function validTourId(value) {
        if (
            value === null ||
            value === undefined ||
            value === ""
        ) {
            return null;
        }

        const number = Number(value);

        return Number.isSafeInteger(number) && number > 0
            ? number
            : null;
    }

    function normalizeText(value) {
        return String(value ?? "")
            .trim()
            .toLowerCase()
            .replace(/đ/g, "d")
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .replace(/[^a-z0-9]+/g, " ")
            .trim();
    }

    function money(value) {
        if (
            value === null ||
            value === undefined ||
            value === ""
        ) {
            return "Chưa có thông tin";
        }

        const number = Number(value);

        if (!Number.isFinite(number) || number < 0) {
            return "Chưa có thông tin";
        }

        return new Intl.NumberFormat("vi-VN", {
            style: "currency",
            currency: "VND",
            maximumFractionDigits: 0
        }).format(number);
    }

    function dateText(value) {
        if (typeof value !== "string") {
            return null;
        }

        const match =
            /^(\d{4})-(\d{2})-(\d{2})(?:T.*)?$/.exec(
                value.trim()
            );

        return match
            ? `${match[3]}/${match[2]}/${match[1]}`
            : null;
    }

    function scrollLatest() {
        requestAnimationFrame(() => {
            ui.messages.scrollTop =
                ui.messages.scrollHeight;
        });
    }

    function showError(message) {
        if (!ui.error) return;

        ui.error.textContent = String(message || "");
        ui.error.hidden = !message;
    }

    function hideError() {
        showError("");
    }

    function updateCount() {
        const length = ui.input.value.length;

        if (ui.count) {
            ui.count.textContent =
                `${length} / ${MAX_LENGTH}`;
        }

        ui.input.style.height = "auto";

        ui.input.style.height =
            `${Math.min(ui.input.scrollHeight, 145)}px`;

        ui.send.disabled =
            sending ||
            !ui.input.value.trim() ||
            length > MAX_LENGTH;
    }

    function setSending(value) {
        sending = Boolean(value);
        ui.input.disabled = sending;

        ui.send.replaceChildren(
            icon(
                sending
                    ? "fa-solid fa-spinner fa-spin"
                    : "fa-solid fa-arrow-up"
            )
        );

        page.querySelectorAll(".tripi-suggestion")
            .forEach(button => {
                button.disabled = sending;
            });

        updateCount();
    }

    function setTyping(value) {
        ui.typing.hidden = !value;

        const label = ui.typing.querySelector("small");

        if (label && value) {
            label.textContent =
                "Tripi đang chuẩn bị câu trả lời cho bạn...";
        }

        if (value) {
            scrollLatest();
        }
    }

    // ======================================================
    // SEARCH ANIMATION
    // ======================================================

    function clearAnimationTimer() {
        if (animationTimer !== null) {
            clearTimeout(animationTimer);
            animationTimer = null;
        }
    }

    function hideSearching() {
        clearAnimationTimer();

        if (ui.searching) {
            ui.searching.hidden = true;
        }

        page.classList.remove(
            "tripi-ai-busy",
            "tripi-ai-found",
            "tripi-ai-searching"
        );
    }

    function beginProcessing() {
        hideSearching();
        page.classList.add("tripi-ai-busy");
        setTyping(true);
    }

    function beginTourReveal() {
        if (!ui.searching) return;

        clearAnimationTimer();

        const heading =
            ui.searching.querySelector("strong");

        const description =
            ui.searching.querySelector("p");

        if (heading) {
            heading.textContent =
                "Tripi vừa khám phá xong hành trình cho bạn! 🚌";
        }

        if (description) {
            description.textContent =
                "Các lựa chọn phù hợp đang xuất hiện...";
        }

        ui.searching.hidden = false;

        page.classList.remove("tripi-ai-busy");
        page.classList.add("tripi-ai-found");

        animationTimer = window.setTimeout(() => {
            ui.searching.hidden = true;

            page.classList.remove("tripi-ai-found");
            animationTimer = null;
        }, 850);
    }

    // ======================================================
    // SAFE MARKDOWN
    // ======================================================

    function renderInline(value, container) {
        const text = String(value ?? "");

        const pattern =
            /(\*\*[^*\n]+\*\*|`[^`\n]+`|\*[^*\n]+\*)/g;

        let last = 0;
        let match;

        while ((match = pattern.exec(text)) !== null) {
            if (match.index > last) {
                container.appendChild(
                    document.createTextNode(
                        text.slice(last, match.index)
                    )
                );
            }

            const part = match[0];

            const tag = part.startsWith("**")
                ? "strong"
                : part.startsWith("`")
                    ? "code"
                    : "em";

            const node = el(
                tag,
                "",
                part.slice(
                    tag === "strong" ? 2 : 1,
                    tag === "strong" ? -2 : -1
                )
            );

            container.appendChild(node);
            last = pattern.lastIndex;
        }

        if (last < text.length) {
            container.appendChild(
                document.createTextNode(
                    text.slice(last)
                )
            );
        }
    }

    function renderMarkdown(value, container) {
        const lines = String(value ?? "")
            .replace(/\r\n?/g, "\n")
            .split("\n");

        let list = null;
        let paragraph = null;

        for (const raw of lines) {
            const line = raw.trim();

            if (!line) {
                list = null;
                paragraph = null;
                continue;
            }

            const bullet =
                line.match(/^[-*•]\s+(.+)$/);

            const numbered =
                line.match(/^\d+[.)]\s+(.+)$/);

            if (bullet || numbered) {
                paragraph = null;

                const tag = numbered ? "ol" : "ul";

                if (
                    !list ||
                    list.tagName.toLowerCase() !== tag
                ) {
                    list = el(tag, "tripi-ai-list");
                    container.appendChild(list);
                }

                const item = el("li");

                renderInline(
                    bullet ? bullet[1] : numbered[1],
                    item
                );

                list.appendChild(item);
                continue;
            }

            list = null;

            const heading =
                line.match(/^#{1,4}\s+(.+)$/);

            if (heading) {
                paragraph = null;

                const node = el(
                    "h4",
                    "tripi-ai-heading"
                );

                renderInline(heading[1], node);
                container.appendChild(node);
                continue;
            }

            if (!paragraph) {
                paragraph = el(
                    "p",
                    "tripi-ai-paragraph"
                );

                container.appendChild(paragraph);
            } else {
                paragraph.appendChild(
                    document.createTextNode(" ")
                );
            }

            renderInline(line, paragraph);
        }

        if (!container.childNodes.length) {
            container.appendChild(
                el(
                    "p",
                    "",
                    "Tripi chưa có nội dung để trả lời."
                )
            );
        }
    }

    // ======================================================
    // CHAT MESSAGES
    // ======================================================

    function appendMessage(role, content, sources = []) {
        const isUser = role === "user";

        const item = el(
            "div",
            "tripi-message " +
            (isUser
                ? "tripi-message-user"
                : "tripi-message-assistant")
        );

        item.dataset.tripiDynamic = "true";

        if (!isUser) {
            const avatar = el(
                "div",
                "tripi-message-avatar"
            );

            const image = document.createElement("img");

            image.src = FOX_IMAGE;
            image.alt = "Tripi";

            avatar.appendChild(image);
            item.appendChild(avatar);
        }

        const body = el(
            "div",
            "tripi-message-body"
        );

        body.appendChild(
            el(
                "div",
                "tripi-message-author",
                isUser ? "Bạn" : "Tripi"
            )
        );

        const bubble = el(
            "div",
            "tripi-message-bubble"
        );

        if (isUser) {
            bubble.appendChild(
                el("p", "", content)
            );
        } else {
            renderMarkdown(content, bubble);
        }

        body.appendChild(bubble);

        if (
            !isUser &&
            Array.isArray(sources) &&
            sources.length > 0
        ) {
            const sourceBox = el(
                "div",
                "tripi-rag-sources"
            );

            const header = el(
                "div",
                "tripi-rag-source-heading"
            );

            header.appendChild(
                icon("fa-solid fa-book-open")
            );

            header.appendChild(
                document.createTextNode(
                    " Nguồn dữ liệu HappyTrip"
                )
            );

            sourceBox.appendChild(header);

            for (const source of sources.slice(0, 5)) {
                if (!validTourId(source?.idTour)) {
                    continue;
                }

                const row = el(
                    "div",
                    "tripi-rag-source-item"
                );

                row.appendChild(
                    el(
                        "span",
                        "",
                        source.tieuDe || "Tài liệu tour"
                    )
                );

                row.appendChild(
                    el(
                        "small",
                        "",
                        `Phiên bản ${source.phienBan ?? 1}`
                    )
                );

                sourceBox.appendChild(row);
            }

            if (sourceBox.children.length > 1) {
                body.appendChild(sourceBox);
            }
        }

        item.appendChild(body);

        ui.messages.insertBefore(
            item,
            ui.typing
        );

        scrollLatest();

        return item;
    }

    function clearMessages() {
        ui.messages.querySelectorAll(
            '[data-tripi-dynamic="true"]'
        ).forEach(node => node.remove());
    }

    // ======================================================
    // PREFERENCES
    // ======================================================

    function resetPreferences() {
        [
            ui.destination,
            ui.interests,
            ui.budget,
            ui.days
        ].forEach(node => {
            if (node) {
                node.textContent = "Chưa xác định";
            }
        });
    }

    function updatePreferences(preferences) {
        if (
            !preferences ||
            typeof preferences !== "object"
        ) {
            return;
        }

        const interests =
            Array.isArray(preferences.soThich)
                ? preferences.soThich.filter(
                    value => typeof value === "string"
                )
                : [];

        if (ui.destination) {
            ui.destination.textContent =
                preferences.diemDen || "Chưa xác định";
        }

        if (ui.interests) {
            ui.interests.textContent =
                interests.length
                    ? interests.join(", ")
                    : "Chưa xác định";
        }

        if (ui.budget) {
            ui.budget.textContent =
                preferences.nganSach != null
                    ? money(preferences.nganSach)
                    : "Chưa xác định";
        }

        if (ui.days) {
            const days = Number(preferences.soNgay);

            ui.days.textContent =
                Number.isInteger(days) && days > 0
                    ? `Tối đa ${days} ngày`
                    : "Chưa xác định";
        }

        // Giao dien goc chi co 4 muc so thich.
        // Khong tu them panel moi.
        // Cac tieu chi loaiTour, soKhach, ngayDuKien
        // van duoc luu nguyen trong session.
    }

    // ======================================================
    // TOUR DISPLAY
    // ======================================================

    function emptyState(title, description) {
        const box = el(
            "div",
            "tripi-tour-empty"
        );

        const picture = el(
            "div",
            "tripi-tour-empty-icon"
        );

        picture.appendChild(
            icon("fa-solid fa-map-location-dot")
        );

        box.appendChild(picture);
        box.appendChild(el("h4", "", title));
        box.appendChild(el("p", "", description));

        return box;
    }

    function showPlaceholder() {
        ui.results.replaceChildren(
            emptyState(
                "Hành trình đang chờ bạn!",
                "Kể Tripi nghe mong muốn của bạn. " +
                "Những tour phù hợp sẽ xuất hiện tại đây."
            )
        );
    }

    function imageUrl(raw) {
        if (
            typeof raw !== "string" ||
            !raw.trim()
        ) {
            return null;
        }

        let path = raw.trim()
            .replace(/\\/g, "/");

        if (
            /^(javascript|data|file|vbscript):/i.test(path) ||
            path.startsWith("//")
        ) {
            return null;
        }

        if (path.startsWith("~/")) {
            path = path.substring(1);
        }

        if (path.startsWith("wwwroot/")) {
            path = "/" + path.slice(8);
        }

        if (
            !path.startsWith("/") &&
            !/^https?:\/\//i.test(path)
        ) {
            path = path.includes("/")
                ? "/" + path
                : "/img/tours/" + path;
        }

        try {
            const url = new URL(
                path,
                location.origin
            );

            if (
                url.origin !== location.origin ||
                !["http:", "https:"].includes(url.protocol)
            ) {
                return null;
            }

            return url.href;
        } catch {
            return null;
        }
    }

    // ======================================================
    // ACTIVE TOUR
    // ======================================================

    function highlightSelectedTour() {
        ui.results.querySelectorAll(
            ".tripi-tour-card"
        ).forEach(card => {
            const selected =
                Number(card.dataset.tourId) ===
                activeTourId;

            card.classList.toggle(
                "tripi-tour-card-selected",
                selected
            );

            const button = card.querySelector(
                ".tripi-select-tour"
            );

            if (button) {
                button.textContent = selected
                    ? "Đang chọn để hỏi"
                    : "Hỏi về tour này";

                button.setAttribute(
                    "aria-pressed",
                    String(selected)
                );
            }
        });
    }

    function chooseTour(
        id,
        focus = false,
        save = true
    ) {
        const selectedId = validTourId(id);

        if (!selectedId) return;

        const tour = currentTours.find(
            value =>
                validTourId(value.idTour) === selectedId
        );

        if (!tour) return;

        activeTourId = selectedId;
        highlightSelectedTour();

        if (focus) {
            ui.input.placeholder =
                `Hỏi Tripi về tour: ${tour.tenTour}`;

            ui.input.focus();
        }

        if (save) {
            session.patch({
                activeTourId: selectedId,
                activeTourName: tour.tenTour || ""
            });
        }
    }

    // ======================================================
    // RESOLVE TOUR CONTEXT
    // ======================================================

    function resolveTourIdForMessage(message) {
        const available = currentTours.filter(
            tour => validTourId(tour.idTour)
        );

        if (!available.length) {
            return null;
        }

        const savedId = validTourId(
            session.read()?.activeTourId
        );

        const selectedId = validTourId(
            activeTourId
        );

        const question = normalizeText(message);

        const named = available.filter(tour => {
            const title = normalizeText(
                tour.tenTour
            );

            const mainPlace = normalizeText(
                String(tour.tenTour || "")
                    .split(/\s+-\s+/)[0]
            );

            return (
                title.length >= 5 &&
                question.includes(title)
            ) || (
                mainPlace.length >= 5 &&
                question.includes(mainPlace) &&
                /\btour\b/.test(question)
            );
        });

        if (named.length === 1) {
            return validTourId(
                named[0].idTour
            );
        }

        if (
            selectedId &&
            available.some(tour =>
                validTourId(tour.idTour) === selectedId
            )
        ) {
            return selectedId;
        }

        if (
            savedId &&
            available.some(tour =>
                validTourId(tour.idTour) === savedId
            )
        ) {
            return savedId;
        }

        if (available.length === 1) {
            return validTourId(
                available[0].idTour
            );
        }

        return null;
    }

    // ======================================================
    // TOUR META - NEW FIELDS
    // ======================================================

    function addMeta(meta, iconName, text) {
        if (
            text === null ||
            text === undefined ||
            String(text).trim() === ""
        ) {
            return;
        }

        const node = el("span");

        node.appendChild(
            icon(iconName)
        );

        node.appendChild(
            document.createTextNode(
                ` ${String(text)}`
            )
        );

        meta.appendChild(node);
    }

    // ======================================================
    // CREATE TOUR CARD
    // ======================================================

    function createTourCard(tour) {
        const id = validTourId(
            tour.idTour
        );

        const card = el(
            "article",
            "tripi-tour-card"
        );

        card.dataset.tourId = String(id);

        const picture = el(
            "div",
            "tripi-tour-card-image"
        );

        const src = imageUrl(
            tour.hinhAnh
        );

        if (src) {
            const img = document.createElement("img");

            img.src = src;
            img.alt = `Ảnh tour ${tour.tenTour || ""}`;
            img.loading = "lazy";

            img.addEventListener(
                "error",
                () => {
                    img.replaceWith(
                        icon("fa-solid fa-mountain-sun")
                    );
                },
                { once: true }
            );

            picture.appendChild(img);
        } else {
            picture.appendChild(
                icon("fa-solid fa-mountain-sun")
            );
        }

        if (tour.matchingScore != null) {
            const score = Number(
                tour.matchingScore
            );

            if (Number.isFinite(score)) {
                picture.appendChild(
                    el(
                        "span",
                        "tripi-tour-score",
                        `${score}/100 điểm`
                    )
                );
            }
        }

        card.appendChild(picture);

        const content = el(
            "div",
            "tripi-tour-card-content"
        );

        content.appendChild(
            el(
                "h4",
                "",
                tour.tenTour || "Tour HappyTrip"
            )
        );

        const meta = el(
            "div",
            "tripi-tour-card-meta"
        );

        // Thong tin cu.
        addMeta(
            meta,
            "fa-solid fa-location-dot",
            tour.diaDiem
        );

        addMeta(
            meta,
            "fa-regular fa-clock",
            tour.thoiGian
        );

        // NEW: Loai tour.
        addMeta(
            meta,
            "fa-solid fa-tags",
            tour.tenLoaiTour
        );

        // NEW: Ngay khoi hanh phu hop.
        const departure = dateText(
            tour.ngayKhoiHanhPhuHop
        );

        if (departure) {
            addMeta(
                meta,
                "fa-regular fa-calendar",
                `Khởi hành ${departure}`
            );
        }

        // NEW: So cho con lai.
        if (
            tour.soChoConLai !== null &&
            tour.soChoConLai !== undefined &&
            tour.soChoConLai !== ""
        ) {
            const seats = Number(
                tour.soChoConLai
            );

            if (
                Number.isInteger(seats) &&
                seats >= 0
            ) {
                addMeta(
                    meta,
                    "fa-solid fa-users",
                    `Còn ${seats} chỗ theo lịch`
                );
            }
        }

        content.appendChild(meta);

        const reasons =
            Array.isArray(tour.lyDoGoiY)
                ? tour.lyDoGoiY
                : [];

        if (reasons.length) {
            const box = el(
                "div",
                "tripi-tour-reasons"
            );

            reasons.slice(0, 3)
                .forEach(reason => {
                    if (typeof reason !== "string") {
                        return;
                    }

                    box.appendChild(
                        el("p", "", reason)
                    );
                });

            content.appendChild(box);
        }

        const footer = el(
            "div",
            "tripi-tour-card-price"
        );

        footer.appendChild(
            el(
                "strong",
                "",
                money(tour.giaBan)
            )
        );

        if (id) {
            const link = el(
                "a",
                "",
                "Xem chi tiết"
            );

            link.href =
                `/Tour/ChiTiet/${id}`;

            link.appendChild(
                icon("fa-solid fa-arrow-right")
            );

            footer.appendChild(link);
        }

        content.appendChild(footer);

        if (id) {
            const button = el(
                "button",
                "tripi-select-tour",
                "Hỏi về tour này"
            );

            button.type = "button";
            
            button.addEventListener("click", () => {
                if (sending) return;

                // Chon dung tour dang hoi.
                chooseTour(id, false, true);

                const question =
                    "Bạn hãy giới thiệu tổng quan về tour này, " +
                    "các điểm tham quan và những thông tin nổi bật nhé.";

                // Hien cau hoi that thay vi chi doi placeholder.
                ui.input.value = question;
                updateCount();

                // Dua ve khu hoi thoai.
                const chatPanel = page.querySelector(
                    ".tripi-world-chat"
                );

                if (chatPanel) {
                    chatPanel.scrollIntoView({
                        behavior: "smooth",
                        block: "start"
                    });
                }

                // Gui truc tiep qua API hien tai.
                sendMessage(question);
            });
             content.appendChild(button);
        }

        card.appendChild(content);

        return card;
    }

    // ======================================================
    // SHOW TOUR RESULTS
    // ======================================================

    function showTours(
        tours,
        message = "",
        suggestedId = null,
        save = true,
        animate = false
    ) {
        currentTours = Array.isArray(tours)
            ? tours.filter(
                tour =>
                    tour &&
                    validTourId(tour.idTour)
            )
            : [];

        ui.results.replaceChildren();

        if (!currentTours.length) {
            activeTourId = null;

            ui.input.placeholder =
                "Kể Tripi nghe chuyến đi của bạn...";

            ui.results.appendChild(
                emptyState(
                    "Chưa tìm được tour phù hợp",
                    message ||
                    "Mình thử điều chỉnh ngân sách, " +
                    "thời gian hoặc điểm đến nhé!"
                )
            );

            if (save) {
                session.patch({
                    tours: [],
                    activeTourId: null,
                    activeTourName: "",
                    searchPerformed: true,
                    searchMessage: message
                });
            }

            return;
        }

        ui.results.appendChild(
            el(
                "p",
                "tripi-tour-count",
                `Khám phá ${currentTours.length} hành trình phù hợp`
            )
        );

        currentTours.forEach(tour => {
            const card = createTourCard(tour);

            if (animate) {
                card.classList.add(
                    "tripi-tour-card-animated"
                );
            }

            ui.results.appendChild(card);
        });

        const requestedId = validTourId(
            suggestedId
        );

        const selectedExists =
            currentTours.some(tour =>
                validTourId(tour.idTour) === requestedId
            );

        const chosenId = selectedExists
            ? requestedId
            : validTourId(
                currentTours[0].idTour
            );

        if (chosenId) {
            chooseTour(
                chosenId,
                false,
                false
            );
        }

        if (save) {
            const selectedTour =
                currentTours.find(tour =>
                    validTourId(tour.idTour) === activeTourId
                );

            session.patch({
                tours: currentTours,
                searchPerformed: true,
                searchMessage: message,
                activeTourId,
                activeTourName:
                    selectedTour?.tenTour || ""
            });
        }

        if (animate) {
            ui.results.scrollTop = 0;
        }
    }

    // ======================================================
    // RESTORE SESSION
    // ======================================================

    function restoreChat() {
        const saved = session.read() || {};

        clearMessages();
        resetPreferences();
        showPlaceholder();
        hideSearching();

        currentTours = [];
        activeTourId = null;

        const messages =
            Array.isArray(saved.messages)
                ? saved.messages
                : [];

                setWorkspaceMode(
            saved.searchPerformed === true
                ? "results"
                : messages.length > 0
                    ? "chat"
                    : "home"
        );

        if (greeting) {
            greeting.hidden = messages.length > 0;
        }

        if (ui.welcome) {
            ui.welcome.hidden = messages.length > 0;
        }

        messages.forEach(message => {
            appendMessage(
                message.role,
                message.content,
                message.sources || []
            );
        });

        if (saved.preferences) {
            updatePreferences(
                saved.preferences
            );
        }

        if (saved.searchPerformed) {
            showTours(
                saved.tours || [],
                saved.searchMessage || "",
                saved.activeTourId,
                false,
                false
            );
        }

        const restoredId = validTourId(
            saved.activeTourId
        );

        if (
            restoredId &&
            currentTours.some(tour =>
                validTourId(tour.idTour) === restoredId
            )
        ) {
            chooseTour(
                restoredId,
                false,
                false
            );
        }

        scrollLatest();
    }

    // ======================================================
    // SAVE CHAT
    // ======================================================

    function saveExchange(
        userText,
        reply,
        sources
    ) {
        const saved = session.read() || {};

        session.patch({
            messages: [
                ...(Array.isArray(saved.messages)
                    ? saved.messages
                    : []),
                {
                    role: "user",
                    content: userText
                },
                {
                    role: "model",
                    content: reply,
                    sources: Array.isArray(sources)
                        ? sources
                        : []
                }
            ]
        });
    }

    // ======================================================
    // RESET CHAT
    // ======================================================

    function resetChat() {
        conversationVersion++;

        if (requestController) {
            requestController.abort();
            requestController = null;
        }

        hideSearching();
        setTyping(false);
        setSending(false);

        session.clear();

        setWorkspaceMode("home");

        activeTourId = null;
        currentTours = [];
        
        clearMessages();
        resetPreferences();
        showPlaceholder();

        if (greeting) {
            greeting.hidden = false;
        }

        if (ui.welcome) {
            ui.welcome.hidden = false;
        }

        ui.input.value = "";
        ui.input.disabled = false;
        ui.input.placeholder =
            "Kể Tripi nghe chuyến đi của bạn...";

        hideError();
        updateCount();

        ui.messages.scrollTop = 0;
        ui.input.focus();
    }

    // ======================================================
    // API ERRORS
    // ======================================================

    function getServerError(data, status) {
        if (data && typeof data === "object") {
            const message =
                data.message ||
                data.title ||
                data.detail;

            if (
                typeof message === "string" &&
                message.trim()
            ) {
                return message;
            }
        }

        if (status === 400) {
            return "Yêu cầu chưa hợp lệ.";
        }

        if (status === 401) {
            return "Bạn cần đăng nhập lại.";
        }

        if (status === 403) {
            return "Yêu cầu bị từ chối. Hãy tải lại trang.";
        }

        if (status === 429) {
            return "Tripi đang bận. Bạn thử lại sau nhé.";
        }

        return `Không thể xử lý yêu cầu (HTTP ${status}).`;
    }

    // ======================================================
    // SEND MESSAGE
    // ======================================================

    async function sendMessage(raw) {
        const text = String(raw || "").trim();

        if (sending || !text) {
            return;
        }

        if (text.length > MAX_LENGTH) {
            showError(
                "Tin nhắn không được vượt quá 1000 ký tự."
            );

            return;
        }

        const version = conversationVersion;

        const previousHistory =
            session.getApiHistory()
                .slice(-MAX_HISTORY);

        const requestTourId =
            resolveTourIdForMessage(text);

        if (
            requestTourId &&
            requestTourId !== activeTourId
        ) {
            chooseTour(
                requestTourId,
                false,
                true
            );
        }

        console.debug(
            "[Tripi] Tour context:",
            {
                idTourDangXem: requestTourId,
                soTour: currentTours.length
            }
        );

        hideError();
        // Neu chua co ket qua tour, chuyen tu
        // man hinh chao sang man hinh chat.
        if (!page.classList.contains("tripi-mode-results")) {
            setWorkspaceMode("chat");
        }

        if (ui.welcome) {
            ui.welcome.hidden = true;
        }
        const pending = appendMessage(
            "user",
            text
        );

        ui.input.value = "";

        setSending(true);
        beginProcessing();

        const currentController =
            new AbortController();

        requestController = currentController;

        try {
            const response = await fetch(
                API_URL,
                {
                    method: "POST",
                    credentials: "same-origin",
                    headers: {
                        "Content-Type":
                            "application/json",
                        "Accept":
                            "application/json",
                        "RequestVerificationToken":
                            token?.value || ""
                    },
                    
                    body: JSON.stringify({
                        message: text,
                        history: previousHistory,
                        idTourDangXem: requestTourId,
                        preferences: session.read()?.preferences ?? null
                    }),

                    signal: currentController.signal
                }
            );

            let data;

            try {
                data = await response.json();
            } catch {
                throw new Error(
                    "Máy chủ chưa trả về dữ liệu hợp lệ."
                );
            }

            if (version !== conversationVersion) {
                return;
            }

            if (!response.ok) {
                throw new Error(
                    getServerError(
                        data,
                        response.status
                    )
                );
            }

            if (
                typeof data.reply !== "string" ||
                !data.reply.trim()
            ) {
                throw new Error(
                    "Tripi chưa tạo được câu trả lời."
                );
            }

            const sources =
                data.ragHasEvidence === true &&
                Array.isArray(data.ragSources)
                    ? data.ragSources
                    : [];

            setTyping(false);

            appendMessage(
                "model",
                data.reply,
                sources
            );

            saveExchange(
                text,
                data.reply,
                sources
            );

            if (data.preferences) {
                updatePreferences(
                    data.preferences
                );

                session.patch({
                    preferences: data.preferences
                });
            }

            // Neu backend tim tour:
            // hien thi danh sach moi.

                    if (data.searchPerformed === true) {
                setWorkspaceMode("results");
                showTours(
                    data.tours,
                    data.searchMessage || "",
                    data.activeTourId,
                    true,
                    true
                );

                if (
                    Array.isArray(data.tours) &&
                    data.tours.length > 0
                ) {
                    beginTourReveal();
                } else {
                    hideSearching();
                }
            } else {
                hideSearching();

                const responseTourId =
                    validTourId(
                        data.activeTourId
                    );

                if (
                    !activeTourId &&
                    responseTourId &&
                    currentTours.some(tour =>
                        validTourId(tour.idTour) ===
                        responseTourId
                    )
                ) {
                    chooseTour(
                        responseTourId,
                        false,
                        true
                    );
                }
            }

        } catch (error) {
            if (
                version !== conversationVersion ||
                error?.name === "AbortError"
            ) {
                return;
            }

            console.error(
                "[Tripi Chat]",
                error
            );

            hideSearching();
            setTyping(false);

            pending.remove();

            showError(
                error?.message ||
                "Không thể kết nối Tripi."
            );

            ui.input.value = text;
            updateCount();

            if (
                !(session.read()?.messages || []).length &&
                ui.welcome
            ) {
                ui.welcome.hidden = false;
            }

        } finally {
            if (version === conversationVersion) {
                requestController = null;

                setTyping(false);
                setSending(false);

                ui.input.focus();
            }
        }
    }

    // ======================================================
    // EVENTS
    // ======================================================

    ui.form.addEventListener(
        "submit",
        event => {
            event.preventDefault();

            sendMessage(
                ui.input.value
            );
        }
    );

    ui.input.addEventListener(
        "keydown",
        event => {
            if (
                event.key === "Enter" &&
                !event.shiftKey &&
                !event.isComposing
            ) {
                event.preventDefault();

                if (!sending) {
                    sendMessage(
                        ui.input.value
                    );
                }
            }
        }
    );

    ui.input.addEventListener(
        "input",
        () => {
            hideError();
            updateCount();
        }
    );

    page.querySelectorAll(
        ".tripi-suggestion"
    ).forEach(button => {
        button.addEventListener(
            "click",
            () => {
                if (sending) return;

                const prompt =
                    button.dataset.tripiPrompt || "";

                if (prompt) {
                    sendMessage(prompt);
                }
            }
        );
    });

    ui.newChat?.addEventListener(
        "click",
        resetChat
    );

    window.addEventListener(
        "pagehide",
        () => {
            clearAnimationTimer();

            if (requestController) {
                requestController.abort();
            }
        }
    );

    // ======================================================
    // INITIALIZE
    // ======================================================

    restoreChat();
    updateCount();

    console.info(
        "[Tripi] Chat + tour context + RAG ready."
    );

})();
