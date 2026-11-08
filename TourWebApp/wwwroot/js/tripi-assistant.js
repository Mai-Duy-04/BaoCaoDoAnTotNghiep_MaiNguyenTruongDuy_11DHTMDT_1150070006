/*
 * HAPPYTRIP - TRIPI AI ASSISTANT
 * File: wwwroot/js/tripi-assistant.js
 *
 * Sidebar AI + Gemini + Matching Score + RAG
 * Dong bo voi window.TripiSession.
 *
 * API: POST /api/tripi-chat/gui-tin-nhan
 * Khong luu API key, password hoac token.
 */

(() => {
    "use strict";

    const API_URL = "/api/tripi-chat/gui-tin-nhan";
    const FOX_IMAGE = "/img/tripi/tripi-fox.png";
    const MAX_HISTORY = 12;
    const MAX_MESSAGE_LENGTH = 1000;

    function init() {
        const root = document.getElementById("tripiAssistant");

        if (!root || root.dataset.initialized === "true") {
            return;
        }

        const $ = id => document.getElementById(id);

        const ui = {
            launcher: $("tripiAssistantLauncher"),
            backdrop: $("tripiAssistantBackdrop"),
            panel: $("tripiAssistantPanel"),
            close: $("tripiAssistantClose"),
            newChat: $("tripiAssistantNewChat"),

            messages: $("tripiAssistantMessages"),
            welcome: $("tripiAssistantWelcome"),
            typing: $("tripiAssistantTyping"),

            form: $("tripiAssistantForm"),
            input: $("tripiAssistantInput"),
            send: $("tripiAssistantSend"),
            count: $("tripiAssistantCount"),
            error: $("tripiAssistantError"),

            results: $("tripiAssistantResults"),
            resultList: $("tripiAssistantTourList"),
            collapse: $("tripiAssistantCollapseResults"),

            context: $("tripiAssistantPageContext"),
            contextName: $("tripiAssistantContextName"),
            clearContext: $("tripiAssistantClearContext")
        };

        const missing = Object.entries(ui)
            .filter(([, element]) => !element)
            .map(([name]) => name);

        if (missing.length) {
            console.error(
                "[Tripi] Thieu thanh phan:",
                missing.join(", ")
            );
            return;
        }

        const session = window.TripiSession;

        if (
            !session ||
            typeof session.read !== "function" ||
            typeof session.patch !== "function" ||
            typeof session.clear !== "function" ||
            typeof session.getApiHistory !== "function"
        ) {
            console.error(
                "[Tripi] Chua tai tripi-session.js. " +
                "Kiem tra thu tu script trong _Layout.cshtml."
            );
            return;
        }

        root.dataset.initialized = "true";

        const initialGreeting = ui.messages.querySelector(
            "[data-tripi-initial-message]"
        );

        const greetingTemplate = initialGreeting
            ? initialGreeting.cloneNode(true)
            : null;

        const state = {
            open: false,
            loading: false,
            history: [],
            tours: [],
            activeTourId: null,
            activeTourName: "",
            collapsed: false,
            previousFocus: null,
            controller: null
        };

        // =====================================================
        // CAC HAM CO BAN
        // =====================================================

        function getValue(object, keys, fallback = null) {
            if (!object || typeof object !== "object") {
                return fallback;
            }

            for (const key of keys) {
                if (
                    object[key] !== undefined &&
                    object[key] !== null &&
                    object[key] !== ""
                ) {
                    return object[key];
                }
            }

            return fallback;
        }

        function el(tag, className = "", text = null) {
            const element = document.createElement(tag);

            if (className) {
                element.className = className;
            }

            if (text !== null && text !== undefined) {
                element.textContent = String(text);
            }

            return element;
        }

        function icon(name) {
            const element = el("i", `fa-solid ${name}`);
            element.setAttribute("aria-hidden", "true");
            return element;
        }

        function validTourId(raw) {
            if (
                raw === null ||
                raw === undefined ||
                raw === ""
            ) {
                return null;
            }

            const number = Number(raw);

            return Number.isSafeInteger(number) && number > 0
                ? number
                : null;
        }

        function formatMoney(raw) {
            if (
                raw === null ||
                raw === undefined ||
                raw === ""
            ) {
                return "Liên hệ";
            }

            const number = typeof raw === "number"
                ? raw
                : Number(
                    String(raw).replace(/[^\d.-]/g, "")
                );

            if (!Number.isFinite(number) || number < 0) {
                return "Liên hệ";
            }

            return new Intl.NumberFormat("vi-VN", {
                style: "currency",
                currency: "VND",
                maximumFractionDigits: 0
            }).format(number);
        }

        function formatDepartureDate(raw) {
            if (raw === null || raw === undefined) {
                return null;
            }

            const match = String(raw).trim().match(
                /^(\d{4})-(\d{2})-(\d{2})(?:[T\s].*)?$/
            );

            if (!match) {
                return null;
            }

            const year = Number(match[1]);
            const month = Number(match[2]);
            const day = Number(match[3]);

            const date = new Date(
                Date.UTC(year, month - 1, day)
            );

            if (
                date.getUTCFullYear() !== year ||
                date.getUTCMonth() !== month - 1 ||
                date.getUTCDate() !== day
            ) {
                return null;
            }

            return `${match[3]}/${match[2]}/${match[1]}`;
        }

        function resolveImage(raw) {
            if (typeof raw !== "string") {
                return "";
            }

            let path = raw.trim().replace(/\\/g, "/");

            if (!path) return "";

            if (
                /^(data|javascript|file|vbscript):/i.test(path) ||
                path.startsWith("//")
            ) {
                return "";
            }

            if (path.startsWith("~/")) {
                path = path.substring(1);
            }

            if (/^https?:\/\//i.test(path)) {
                try {
                    const url = new URL(path);

                    return url.origin === location.origin
                        ? url.pathname + url.search
                        : "";
                } catch {
                    return "";
                }
            }

            if (path.startsWith("/")) {
                return path;
            }

            if (path.startsWith("wwwroot/")) {
                return "/" + path.substring("wwwroot/".length);
            }

            if (
                path.startsWith("img/") ||
                path.startsWith("images/") ||
                path.startsWith("uploads/")
            ) {
                return "/" + path;
            }

            if (!path.includes("/")) {
                return "/img/tours/" + path;
            }

            return "/" + path;
        }

        function scrollBottom() {
            requestAnimationFrame(() => {
                ui.messages.scrollTop =
                    ui.messages.scrollHeight;
            });
        }

        function showError(message = "") {
            ui.error.textContent = String(message);
            ui.error.hidden = !message;
        }

        function updateCount() {
            ui.count.textContent =
                `${ui.input.value.length}/${MAX_MESSAGE_LENGTH}`;

            ui.send.disabled =
                state.loading ||
                !ui.input.value.trim() ||
                ui.input.value.length > MAX_MESSAGE_LENGTH;
        }

        function setLoading(enabled) {
            state.loading = Boolean(enabled);

            root.classList.toggle("is-loading", state.loading);

            ui.typing.hidden = !state.loading;
            ui.input.disabled = state.loading;

            ui.form.setAttribute(
                "aria-busy",
                String(state.loading)
            );

            updateCount();
            scrollBottom();
        }

        // =====================================================
        // MO / DONG SIDEBAR
        // =====================================================

        function openAssistant() {
            if (state.open) return;

            state.previousFocus = document.activeElement;
            state.open = true;

            root.classList.add("is-open");

            ui.panel.setAttribute("aria-hidden", "false");
            ui.launcher.setAttribute("aria-expanded", "true");

            ui.panel.focus({ preventScroll: true });

            scrollBottom();
        }

        function closeAssistant() {
            if (!state.open) return;

            state.open = false;

            root.classList.remove("is-open");

            ui.panel.setAttribute("aria-hidden", "true");
            ui.launcher.setAttribute("aria-expanded", "false");

            const previous = state.previousFocus;

            if (
                previous &&
                document.contains(previous) &&
                typeof previous.focus === "function"
            ) {
                previous.focus({ preventScroll: true });
            } else {
                ui.launcher.focus({ preventScroll: true });
            }
        }

        // =====================================================
        // NGU CANH TOUR
        // =====================================================

        function detectPageTour() {
            const match = location.pathname.match(
                /^\/tour\/chitiet\/(\d+)(?:\/|$)/i
            );

            if (match) {
                return validTourId(match[1]);
            }

            if (/^\/tour\/chitiet\/?$/i.test(location.pathname)) {
                const params = new URLSearchParams(
                    location.search
                );

                return validTourId(
                    params.get("id") ||
                    params.get("idTour")
                );
            }

            const element = document.querySelector(
                "[data-tripi-tour-id]"
            );

            return element
                ? validTourId(
                    element.getAttribute("data-tripi-tour-id")
                )
                : null;
        }

        function detectPageTourName() {
            const element = document.querySelector(
                "[data-tripi-tour-name]"
            );

            if (element) {
                return (
                    element.getAttribute("data-tripi-tour-name") ||
                    ""
                ).trim();
            }

            const heading = document.querySelector(
                "main h1, .tour-detail h1, .tour-title"
            );

            return heading
                ? heading.textContent.trim().slice(0, 120)
                : "";
        }

        function renderContext() {
            const hasTour = state.activeTourId !== null;

            ui.context.hidden = !hasTour;

            if (hasTour) {
                ui.contextName.textContent =
                    state.activeTourName ||
                    `Tour #${state.activeTourId}`;
            }
        }

        function saveContext() {
            session.patch({
                activeTourId: state.activeTourId,
                activeTourName: state.activeTourName
            });
        }

        function selectTour(id, name = "", save = true) {
            const number = validTourId(id);

            if (!number) return;

            state.activeTourId = number;
            state.activeTourName = String(name || "").trim();

            renderContext();

            if (save) {
                saveContext();
            }
        }

        function clearTour(save = true) {
            state.activeTourId = null;
            state.activeTourName = "";

            renderContext();

            if (save) {
                saveContext();
            }
        }

        function restorePageContextIfNeeded() {
            const id = detectPageTour();

            if (!id) return;

            // Tren trang chi tiet, tour dang xem
            // duoc uu tien lam ngu canh hien tai.
            if (state.activeTourId !== id) {
                selectTour(id, detectPageTourName());
            }
        }

        // =====================================================
        // HIEN THI VAN BAN AN TOAN
        // =====================================================

        function renderInline(parent, raw) {
            const parts = String(raw || "").split(
                /(\*\*[^*\n]+\*\*)/g
            );

            for (const part of parts) {
                if (!part) continue;

                if (
                    part.startsWith("**") &&
                    part.endsWith("**") &&
                    part.length > 4
                ) {
                    parent.appendChild(
                        el("strong", "", part.slice(2, -2))
                    );
                } else {
                    parent.appendChild(
                        document.createTextNode(part)
                    );
                }
            }
        }

        function renderContent(parent, content) {
            const lines = String(content || "")
                .replace(/\r\n?/g, "\n")
                .split("\n");

            let currentList = null;

            for (const rawLine of lines) {
                const line = rawLine.trim();

                if (!line) {
                    currentList = null;
                    continue;
                }

                const bullet = line.match(/^[-*•]\s+(.+)$/);
                const numbered = line.match(/^\d+[.)]\s+(.+)$/);

                if (bullet || numbered) {
                    const tag = bullet ? "ul" : "ol";

                    if (
                        !currentList ||
                        currentList.tagName.toLowerCase() !== tag
                    ) {
                        currentList = el(tag);
                        parent.appendChild(currentList);
                    }

                    const item = el("li");

                    renderInline(
                        item,
                        bullet ? bullet[1] : numbered[1]
                    );

                    currentList.appendChild(item);
                    continue;
                }

                currentList = null;

                const paragraph = el("p");

                renderInline(
                    paragraph,
                    line.replace(/^#{1,4}\s+/, "")
                );

                parent.appendChild(paragraph);
            }

            if (!parent.childNodes.length) {
                parent.appendChild(el("p", "", content));
            }
        }

        // =====================================================
        // HIEN THI TIN NHAN + NGUON RAG
        // =====================================================

        function appendMessage(role, content, sources = []) {
            const isUser = role === "user";

            const wrapper = el(
                "div",
                "tripi-assistant-message " +
                (isUser
                    ? "tripi-assistant-message-user"
                    : "tripi-assistant-message-ai")
            );

            wrapper.dataset.tripiDynamic = "true";

            if (!isUser) {
                const avatar = el(
                    "span",
                    "tripi-assistant-message-avatar"
                );

                const image = document.createElement("img");

                image.src = FOX_IMAGE;
                image.alt = "";
                image.loading = "lazy";

                avatar.appendChild(image);
                wrapper.appendChild(avatar);
            }

            const body = el(
                "div",
                "tripi-assistant-message-content"
            );

            if (!isUser) {
                const name = el(
                    "span",
                    "tripi-assistant-message-name",
                    "Tripi "
                );

                name.appendChild(
                    icon("fa-wand-magic-sparkles")
                );

                body.appendChild(name);
            }

            const bubble = el(
                "div",
                "tripi-assistant-message-bubble"
            );

            renderContent(bubble, content);
            body.appendChild(bubble);

            if (
                !isUser &&
                Array.isArray(sources) &&
                sources.length
            ) {
                const sourceBox = el(
                    "div",
                    "tripi-assistant-source"
                );

                sourceBox.appendChild(
                    el("strong", "", "Nguồn dữ liệu HappyTrip")
                );

                const list = el("ul");

                for (const source of sources.slice(0, 5)) {
                    if (!validTourId(source?.idTour)) {
                        continue;
                    }

                    const title = getValue(
                        source,
                        ["tieuDe", "TieuDe", "title"],
                        "Tài liệu tour"
                    );

                    const item = el("li", "", title);

                    const version = getValue(
                        source,
                        ["phienBan", "PhienBan"],
                        null
                    );

                    if (version !== null) {
                        item.appendChild(
                            document.createTextNode(
                                ` (phiên bản ${version})`
                            )
                        );
                    }

                    list.appendChild(item);
                }

                if (list.children.length > 0) {
                    sourceBox.appendChild(list);
                    body.appendChild(sourceBox);
                }
            }

            wrapper.appendChild(body);

            ui.messages.insertBefore(wrapper, ui.typing);

            scrollBottom();

            return wrapper;
        }

        // =====================================================
        // MATCHING SCORE: CHUAN HOA TOUR
        // =====================================================

        function normalizeTour(raw) {
            if (!raw || typeof raw !== "object") {
                return null;
            }

            const id = validTourId(
                getValue(raw, [
                    "idTour", "IdTour",
                    "tourId", "TourId",
                    "id"
                ])
            );

            if (!id) return null;

            return {
                idTour: id,

                tenTour: String(
                    getValue(
                        raw,
                        ["tenTour", "TenTour", "name"],
                        `Tour #${id}`
                    )
                ),

                giaBan: getValue(raw, [
                    "giaBan", "GiaBan",
                    "giaKhuyenMai", "GiaKhuyenMai",
                    "price"
                ]),

                matchingScore: getValue(raw, [
                    "matchingScore", "MatchingScore",
                    "diemPhuHop", "DiemPhuHop"
                ]),

                thoiGian: getValue(raw, [
                    "thoiGian", "ThoiGian"
                ], ""),

                soNgay: getValue(raw, [
                    "soNgay", "SoNgay"
                ]),

                diaDiem: getValue(raw, [
                    "diaDiem", "DiaDiem"
                ], ""),

                hinhAnh: resolveImage(
                    getValue(raw, [
                        "hinhAnh", "HinhAnh",
                        "imageUrl", "ImageUrl"
                    ], "")
                ),

                diemSoThich: getValue(raw, [
                    "diemSoThich", "DiemSoThich"
                ]),

                diemDiemDen: getValue(raw, [
                    "diemDiemDen", "DiemDiemDen"
                ]),

                diemNganSach: getValue(raw, [
                    "diemNganSach", "DiemNganSach"
                ]),

                lyDoGoiY: getValue(raw, [
                    "lyDoGoiY", "LyDoGoiY"
                ], []),

                // NEW: Loai tour
                tenLoaiTour: getValue(raw, [
                    "tenLoaiTour", "TenLoaiTour"
                ]),

                // NEW: Lich khoi hanh
                ngayKhoiHanhPhuHop: getValue(raw, [
                    "ngayKhoiHanhPhuHop",
                    "NgayKhoiHanhPhuHop"
                ]),

                // NEW: So cho theo lich
                soChoConLai: getValue(raw, [
                    "soChoConLai", "SoChoConLai"
                ])
            };
        }

        // =====================================================
        // THE TOUR
        // =====================================================

        function createTourCard(tour) {
            const card = el(
                "article",
                "tripi-assistant-tour-card"
            );

            if (tour.hinhAnh) {
                const image = document.createElement("img");

                image.className = "tripi-assistant-tour-image";
                image.src = tour.hinhAnh;
                image.alt = tour.tenTour;
                image.loading = "lazy";

                image.addEventListener(
                    "error",
                    () => {
                        image.style.display = "none";
                    },
                    { once: true }
                );

                card.appendChild(image);
            }

            const info = el(
                "div",
                "tripi-assistant-tour-info"
            );

            info.appendChild(
                el(
                    "div",
                    "tripi-assistant-tour-name",
                    tour.tenTour
                )
            );

            const meta = el(
                "div",
                "tripi-assistant-tour-meta"
            );

            if (tour.thoiGian) {
                meta.appendChild(
                    el("span", "", tour.thoiGian)
                );
            } else if (tour.soNgay) {
                meta.appendChild(
                    el("span", "", `${tour.soNgay} ngày`)
                );
            }

            if (tour.diaDiem) {
                meta.appendChild(
                    el("span", "", tour.diaDiem)
                );
            }

            // NEW: Loai tour.
            if (tour.tenLoaiTour) {
                meta.appendChild(
                    el(
                        "span",
                        "",
                        String(tour.tenLoaiTour)
                    )
                );
            }

            // NEW: Ngay khoi hanh phu hop.
            const departure = formatDepartureDate(
                tour.ngayKhoiHanhPhuHop
            );

            if (departure) {
                meta.appendChild(
                    el(
                        "span",
                        "",
                        `Khởi hành ${departure}`
                    )
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
                    meta.appendChild(
                        el(
                            "span",
                            "",
                            `Còn ${seats} chỗ theo lịch`
                        )
                    );
                }
            }

            info.appendChild(meta);

            info.appendChild(
                el(
                    "div",
                    "tripi-assistant-tour-price",
                    formatMoney(tour.giaBan)
                )
            );

            const score = Number(tour.matchingScore);

            if (
                tour.matchingScore !== null &&
                tour.matchingScore !== undefined &&
                Number.isFinite(score)
            ) {
                info.appendChild(
                    el(
                        "span",
                        "tripi-assistant-tour-score",
                        `Phù hợp ${score}/100`
                    )
                );
            }

            const actions = el(
                "div",
                "tripi-assistant-tour-actions"
            );

            const link = el("a", "", "Xem chi tiết");

            link.href = `/Tour/ChiTiet/${tour.idTour}`;

            link.appendChild(
                icon("fa-arrow-up-right-from-square")
            );

            const ask = el(
                "button",
                "",
                "Hỏi về tour này"
            );

            ask.type = "button";

            ask.appendChild(
                icon("fa-comment-dots")
            );

            ask.addEventListener("click", () => {
                selectTour(tour.idTour, tour.tenTour);

                ui.input.value =
                    "Tour này có những hoạt động gì nổi bật?";

                updateCount();
                showError("");
                ui.input.focus();
            });

            actions.appendChild(link);
            actions.appendChild(ask);

            info.appendChild(actions);
            card.appendChild(info);

            return card;
        }

        // =====================================================
        // AN / HIEN KET QUA
        // =====================================================

        function collapseResults(enabled) {
            state.collapsed = Boolean(enabled);

            ui.resultList.hidden = state.collapsed;

            ui.collapse.setAttribute(
                "aria-expanded",
                String(!state.collapsed)
            );

            const arrow = ui.collapse.querySelector("i");

            if (arrow) {
                arrow.className = state.collapsed
                    ? "fa-solid fa-chevron-up"
                    : "fa-solid fa-chevron-down";
            }
        }

        function showTours(rawTours, save = true) {
            const tours = Array.isArray(rawTours)
                ? rawTours.map(normalizeTour).filter(Boolean)
                : [];

            state.tours = tours;

            ui.resultList.replaceChildren();

            if (!tours.length) {
                ui.results.hidden = true;
            } else {
                for (const tour of tours.slice(0, 8)) {
                    ui.resultList.appendChild(
                        createTourCard(tour)
                    );
                }

                ui.results.hidden = false;
                collapseResults(false);
            }

            if (save) {
                session.patch({
                    tours: state.tours
                });
            }
        }

        // =====================================================
        // DONG BO PHIEN CHAT
        // =====================================================

        function saveSuccessfulExchange(
            userMessage,
            aiReply,
            sources
        ) {
            const previous = session.read() || {};

            const updatedMessages = [
                ...(Array.isArray(previous.messages)
                    ? previous.messages
                    : []),
                {
                    role: "user",
                    content: userMessage,
                    sources: []
                },
                {
                    role: "model",
                    content: aiReply,
                    sources: Array.isArray(sources)
                        ? sources
                        : []
                }
            ];

            session.patch({
                messages: updatedMessages
            });

            state.history = session.getApiHistory();
        }

        function restoreMessages(savedMessages) {
            ui.messages.querySelectorAll(
                '[data-tripi-dynamic="true"]'
            ).forEach(element => element.remove());

            if (
                !Array.isArray(savedMessages) ||
                !savedMessages.length
            ) {
                ui.welcome.hidden = false;
                return;
            }

            ui.welcome.hidden = true;

            for (const message of savedMessages) {
                appendMessage(
                    message.role,
                    message.content,
                    message.sources || []
                );
            }
        }

        function restoreSession() {
            const saved = session.read() || {};

            state.history = session.getApiHistory();

            state.activeTourId = validTourId(
                saved.activeTourId
            );

            state.activeTourName =
                saved.activeTourName || "";

            renderContext();

            restoreMessages(saved.messages);

            showTours(saved.tours || [], false);

            restorePageContextIfNeeded();

            scrollBottom();
        }

        // =====================================================
        // API ERRORS
        // =====================================================

        async function serverError(response) {
            let errorMessage = "";

            try {
                const data = await response.json();

                errorMessage = String(
                    getValue(
                        data,
                        ["message", "title", "detail"],
                        ""
                    )
                );

                if (!errorMessage && data.errors) {
                    errorMessage = Object.values(data.errors)
                        .flat()
                        .filter(Boolean)
                        .join("; ");
                }
            } catch {
                // Server co the tra ve HTML.
            }

            if (errorMessage) {
                return errorMessage;
            }

            switch (response.status) {
                case 400:
                    return "Yêu cầu chưa hợp lệ.";
                case 401:
                    return "Bạn cần đăng nhập lại.";
                case 403:
                    return "Yêu cầu bị từ chối. Hãy tải lại trang.";
                case 429:
                    return "Tripi đang nhận quá nhiều yêu cầu.";
                default:
                    return `Máy chủ gặp lỗi HTTP ${response.status}.`;
            }
        }

        // =====================================================
        // SEND MESSAGE
        // =====================================================

        async function sendMessage(rawMessage) {
            const message = String(rawMessage || "").trim();

            if (!message || state.loading) {
                return;
            }

            if (message.length > MAX_MESSAGE_LENGTH) {
                showError(
                    "Tin nhắn không được vượt quá 1000 ký tự."
                );
                return;
            }

            const previousHistory = session
                .getApiHistory()
                .slice(-MAX_HISTORY);

            ui.welcome.hidden = true;
            showError("");

            const pendingMessage = appendMessage(
                "user",
                message
            );

            ui.input.value = "";
            updateCount();

            setLoading(true);

            const controller = new AbortController();
            state.controller = controller;

            try {
                const token = ui.form.querySelector(
                    'input[name="__RequestVerificationToken"]'
                )?.value;

                const headers = {
                    "Content-Type": "application/json",
                    "Accept": "application/json"
                };

                if (token) {
                    headers["RequestVerificationToken"] = token;
                }

                const response = await fetch(API_URL, {
                    method: "POST",
                    credentials: "same-origin",
                    headers,
                    signal: controller.signal,
                    
                body: JSON.stringify({
                    message,
                    history: previousHistory,
                    idTourDangXem: state.activeTourId,
                    preferences: session.read()?.preferences ?? null
                })

                });

                if (!response.ok) {
                    throw new Error(
                        await serverError(response)
                    );
                }

                const data = await response.json();

                const reply = String(
                    getValue(
                        data,
                        ["reply", "Reply"],
                        ""
                    )
                ).trim();

                if (!reply) {
                    throw new Error(
                        "Tripi chưa trả về nội dung phản hồi."
                    );
                }

                const ragHasEvidence = getValue(
                    data,
                    ["ragHasEvidence", "RagHasEvidence"],
                    false
                ) === true;

                const sources = getValue(
                    data,
                    ["ragSources", "RagSources"],
                    []
                );

                const validSources =
                    ragHasEvidence && Array.isArray(sources)
                        ? sources
                        : [];

                appendMessage(
                    "model",
                    reply,
                    validSources
                );

                saveSuccessfulExchange(
                    message,
                    reply,
                    validSources
                );

                const searchPerformed = getValue(
                    data,
                    ["searchPerformed", "SearchPerformed"],
                    false
                ) === true;

                const tours = getValue(
                    data,
                    ["tours", "Tours"],
                    []
                );

                if (searchPerformed) {
                    showTours(tours);

                    const returnedTourId = validTourId(
                        getValue(
                            data,
                            ["activeTourId", "ActiveTourId"]
                        )
                    );

                    const matchingTour = state.tours.find(
                        tour => tour.idTour === returnedTourId
                    );

                    if (matchingTour) {
                        selectTour(
                            matchingTour.idTour,
                            matchingTour.tenTour
                        );
                    } else {
                        clearTour();
                    }

                    session.patch({
                        searchPerformed: true,
                        searchMessage: String(
                            getValue(
                                data,
                                ["searchMessage", "SearchMessage"],
                                ""
                            )
                        ),
                        preferences: getValue(
                            data,
                            ["preferences", "Preferences"],
                            null
                        )
                    });
                } else {
                    // Giu nguyen ket qua Matching Score
                    // khi khach hoi thong tin tour / RAG.

                    const returnedTourId = validTourId(
                        getValue(
                            data,
                            ["activeTourId", "ActiveTourId"]
                        )
                    );

                    if (
                        returnedTourId &&
                        state.activeTourId === null
                    ) {
                        const found = state.tours.find(
                            tour => tour.idTour === returnedTourId
                        );

                        selectTour(
                            returnedTourId,
                            found ? found.tenTour : ""
                        );
                    }

                    // Khong ghi de nhu cau cu bang null.
                    const preferences = getValue(
                        data,
                        ["preferences", "Preferences"],
                        null
                    );

                    if (preferences) {
                        session.patch({
                            preferences
                        });
                    }
                }

            } catch (error) {
                if (error?.name === "AbortError") {
                    return;
                }

                console.error("[Tripi AI]", error);

                showError(
                    error?.message ||
                    "Không thể kết nối Tripi."
                );

                // Tin nhan that bai khong dua vao history.
                pendingMessage.remove();

                appendMessage(
                    "model",
                    "Tripi chưa xử lý được câu hỏi này. " +
                    "Bạn hãy thử gửi lại nhé! 🦊"
                );

                ui.input.value = message;
                updateCount();

            } finally {
                if (state.controller === controller) {
                    state.controller = null;
                    setLoading(false);
                }

                if (state.open) {
                    ui.input.focus({ preventScroll: true });
                }
            }
        }

        // =====================================================
        // TRO CHUYEN MOI
        // =====================================================

        function resetChat() {
            if (state.controller) {
                state.controller.abort();
                state.controller = null;
            }

            setLoading(false);

            session.clear();

            state.history = [];
            state.tours = [];
            state.collapsed = false;

            ui.messages.replaceChildren();

            if (greetingTemplate) {
                ui.messages.appendChild(
                    greetingTemplate.cloneNode(true)
                );
            }

            ui.messages.appendChild(ui.welcome);
            ui.messages.appendChild(ui.typing);

            ui.welcome.hidden = false;
            ui.typing.hidden = true;

            ui.resultList.replaceChildren();
            ui.results.hidden = true;

            ui.input.value = "";

            updateCount();
            showError("");

            clearTour(false);
            restorePageContextIfNeeded();

            scrollBottom();
            ui.input.focus({ preventScroll: true });
        }

        // =====================================================
        // EVENTS
        // =====================================================

        ui.launcher.addEventListener(
            "click",
            openAssistant
        );

        ui.close.addEventListener(
            "click",
            closeAssistant
        );

        ui.backdrop.addEventListener(
            "click",
            closeAssistant
        );

        ui.newChat.addEventListener(
            "click",
            resetChat
        );

        ui.clearContext.addEventListener("click", () => {
            clearTour();
            ui.input.focus();
        });

        ui.collapse.addEventListener("click", () => {
            collapseResults(!state.collapsed);
        });

        ui.input.addEventListener(
            "input",
            updateCount
        );

        ui.input.addEventListener("keydown", event => {
            if (
                event.key === "Enter" &&
                !event.shiftKey &&
                !event.isComposing
            ) {
                event.preventDefault();

                if (!state.loading) {
                    ui.form.requestSubmit();
                }
            }
        });

        ui.form.addEventListener("submit", event => {
            event.preventDefault();
            sendMessage(ui.input.value);
        });

        root.querySelectorAll("[data-tripi-suggestion]")
            .forEach(button => {
                button.addEventListener("click", () => {
                    if (state.loading) return;

                    const prompt = button.getAttribute(
                        "data-tripi-suggestion"
                    );

                    if (prompt) {
                        sendMessage(prompt);
                    }
                });
            });

        document.addEventListener("keydown", event => {
            if (
                event.key === "Escape" &&
                state.open
            ) {
                closeAssistant();
            }
        });

        document.addEventListener("click", event => {
            if (!(event.target instanceof Element)) {
                return;
            }

            const button = event.target.closest(
                "[data-open-tripi]"
            );

            if (!button) return;

            event.preventDefault();

            const id = validTourId(
                button.getAttribute("data-tripi-tour-id")
            );

            const name = button.getAttribute(
                "data-tripi-tour-name"
            ) || "";

            if (id) {
                selectTour(id, name);
            }

            openAssistant();
        });

        // Luu ngu canh khi chuyen trang.
        window.addEventListener("pagehide", () => {
            if (state.controller) {
                state.controller.abort();
            }

            if (!state.loading) {
                saveContext();
            }
        });

        // =====================================================
        // INITIALIZE
        // =====================================================

        restoreSession();
        updateCount();

        console.info(
            "[Tripi AI] Sidebar + shared session ready."
        );
    }

    if (document.readyState === "loading") {
        document.addEventListener(
            "DOMContentLoaded",
            init,
            { once: true }
        );
    } else {
        init();
    }
})();
