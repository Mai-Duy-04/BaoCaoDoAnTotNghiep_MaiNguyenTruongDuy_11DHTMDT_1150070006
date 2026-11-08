/* ==========================================================
   HAPPYTRIP - TRIPI SHARED CHAT SESSION
   File: wwwroot/js/tripi-session.js

   Muc dich:
   - Dung chung phien chat giua sidebar va /tripi-chat
   - Giu lich su cau hoi / cau tra loi
   - Giu tour dang chon
   - Giu danh sach Matching Score
   - Giu nguon tai lieu RAG
   - Giu nhu cau du lich
   - Chi luu trong cung tab trinh duyet

   Khong luu API key, password hay token bao mat.

   API:
   window.TripiSession.read()
   window.TripiSession.write(data)
   window.TripiSession.patch(data)
   window.TripiSession.clear()
   window.TripiSession.addMessage(role, content, sources)
   window.TripiSession.getApiHistory()
   ========================================================== */

(() => {
    "use strict";

    const STORAGE_KEY = "happytrip_tripi_chat_session_v1";
    const VERSION = 1;

    const MAX_MESSAGES = 60;
    const MAX_HISTORY = 12;
    const MAX_TOURS = 20;
    const MAX_SOURCES = 5;

    const MAX_MESSAGE_LENGTH = 1000;
    const MAX_REPLY_LENGTH = 12000;

    // Tu dong bo phien qua cu sau 24 gio.
    const SESSION_LIFETIME = 24 * 60 * 60 * 1000;

    function createEmptySession() {
        return {
            version: VERSION,
            updatedAt: Date.now(),

            messages: [],

            activeTourId: null,
            activeTourName: "",

            tours: [],

            preferences: null,

            searchPerformed: false,
            searchMessage: ""
        };
    }

    function isObject(value) {
        return value !== null &&
            typeof value === "object" &&
            !Array.isArray(value);
    }

    function safeString(value, maxLength = 1000) {
        if (typeof value !== "string") {
            return "";
        }

        return value.slice(0, maxLength);
    }

    function safeNumber(value) {
        if (
            value === null ||
            value === undefined ||
            value === ""
        ) {
            return null;
        }

        const number = Number(value);

        return Number.isFinite(number)
            ? number
            : null;
    }

    function safeTourId(value) {
        const number = safeNumber(value);

        return number !== null &&
            Number.isSafeInteger(number) &&
            number > 0
            ? number
            : null;
    }

    function safeSource(source) {
        if (!isObject(source)) {
            return null;
        }

        return {
            idTaiLieu: safeTourId(
                source.idTaiLieu ?? source.IdTaiLieu
            ),

            idTour: safeTourId(
                source.idTour ?? source.IdTour
            ),

            tieuDe: safeString(
                source.tieuDe ?? source.TieuDe,
                300
            ),

            phienBan: safeTourId(
                source.phienBan ?? source.PhienBan
            )
        };
    }

    function safeSources(sources) {
        if (!Array.isArray(sources)) {
            return [];
        }

        return sources
            .slice(0, MAX_SOURCES)
            .map(safeSource)
            .filter(Boolean);
    }

    function safeMessage(message) {
        if (!isObject(message)) {
            return null;
        }

        let role = message.role ?? message.Role;

        // Gemini su dung "model".
        // Giao dien cu co the dung "assistant".
        if (role === "assistant") {
            role = "model";
        }

        if (role !== "user" && role !== "model") {
            return null;
        }

        const maxLength = role === "user"
            ? MAX_MESSAGE_LENGTH
            : MAX_REPLY_LENGTH;

        const content = safeString(
            message.content ?? message.Content,
            maxLength
        ).trim();

        if (!content) {
            return null;
        }

        return {
            role,
            content,

            sources: role === "model"
                ? safeSources(
                    message.sources ??
                    message.ragSources ??
                    []
                )
                : []
        };
    }

    function safeMessages(messages) {
        if (!Array.isArray(messages)) {
            return [];
        }

        return messages
            .slice(-MAX_MESSAGES)
            .map(safeMessage)
            .filter(Boolean);
    }

    function safeTour(tour) {
        if (!isObject(tour)) {
            return null;
        }

        const idTour = safeTourId(
            tour.idTour ??
            tour.IdTour ??
            tour.id ??
            tour.tourId
        );

        if (!idTour) {
            return null;
        }

        const score = safeNumber(
            tour.matchingScore ??
            tour.MatchingScore ??
            tour.score
        );

        const price = safeNumber(
            tour.giaBan ??
            tour.GiaBan ??
            tour.giaKhuyenMai ??
            tour.GiaKhuyenMai ??
            tour.price
        );

        const reasons = Array.isArray(tour.lyDoGoiY)
            ? tour.lyDoGoiY
            : [];

        return {
            idTour,

            tenTour: safeString(
                tour.tenTour ??
                tour.TenTour ??
                tour.name ??
                "",
                300
            ),

            diaDiem: safeString(
                tour.diaDiem ??
                tour.DiaDiem ??
                tour.destination ??
                "",
                300
            ),

            thoiGian: safeString(
                tour.thoiGian ??
                tour.ThoiGian ??
                tour.duration ??
                "",
                100
            ),

            hinhAnh: safeString(
                tour.hinhAnh ??
                tour.HinhAnh ??
                tour.image ??
                "",
                500
            ),

            giaBan: price,

            soNgay: safeNumber(
                tour.soNgay ??
                tour.SoNgay
            ),

            matchingScore: score,

            diemSoThich: safeNumber(
                tour.diemSoThich ??
                tour.DiemSoThich
            ),

            diemDiemDen: safeNumber(
                tour.diemDiemDen ??
                tour.DiemDiemDen
            ),

            diemNganSach: safeNumber(
                tour.diemNganSach ??
                tour.DiemNganSach
            ),

            lyDoGoiY: reasons
                .filter(item => typeof item === "string")
                .slice(0, 5)
                .map(item => item.slice(0, 500))
        };
    }

    function safeTours(tours) {
        if (!Array.isArray(tours)) {
            return [];
        }

        const result = [];
        const seenIds = new Set();

        for (const rawTour of tours.slice(0, MAX_TOURS)) {
            const tour = safeTour(rawTour);

            if (!tour || seenIds.has(tour.idTour)) {
                continue;
            }

            seenIds.add(tour.idTour);
            result.push(tour);
        }

        return result;
    }

    
function safePreferences(value) {
    if (!isObject(value)) {
        return null;
    }

    const interests = Array.isArray(
        value.soThich ?? value.SoThich
    )
        ? (value.soThich ?? value.SoThich)
        : [];

    const budgetScope = safeString(
        value.loaiNganSach ??
        value.LoaiNganSach ??
        "",
        30
    );

    const tourType = safeString(
        value.loaiTour ??
        value.LoaiTour ??
        "",
        200
    );

    const departureDate = safeString(
        value.ngayDuKien ??
        value.NgayDuKien ??
        "",
        30
    );

    const guests = safeNumber(
        value.soKhach ??
        value.SoKhach
    );

    return {
        diemDen: safeString(
            value.diemDen ??
            value.DiemDen ??
            "",
            200
        ),

        soThich: interests
            .filter(item => typeof item === "string")
            .slice(0, 20)
            .map(item => item.slice(0, 100)),

        nganSach: safeNumber(
            value.nganSach ??
            value.NganSach
        ),

        loaiNganSach:
            budgetScope === "MoiNguoi" ||
            budgetScope === "CaNhom"
                ? budgetScope
                : null,

        soNgay: safeNumber(
            value.soNgay ??
            value.SoNgay
        ),

        loaiTour: tourType || null,

        soKhach:
            Number.isSafeInteger(guests) &&
            guests > 0
                ? guests
                : null,

        ngayDuKien:
            /^\d{4}-\d{2}-\d{2}$/.test(departureDate)
                ? departureDate
                : null
    };
}


    function normalizeSession(raw) {
        const empty = createEmptySession();

        if (!isObject(raw) || raw.version !== VERSION) {
            return empty;
        }

        const updatedAt = safeNumber(raw.updatedAt);

        if (
            updatedAt === null ||
            updatedAt > Date.now() + 60000 ||
            Date.now() - updatedAt > SESSION_LIFETIME
        ) {
            return empty;
        }

        return {
            version: VERSION,
            updatedAt,

            messages: safeMessages(raw.messages),

            activeTourId: safeTourId(raw.activeTourId),

            activeTourName: safeString(
                raw.activeTourName,
                300
            ),

            tours: safeTours(raw.tours),

            preferences: safePreferences(
                raw.preferences
            ),

            searchPerformed:
                raw.searchPerformed === true,

            searchMessage: safeString(
                raw.searchMessage,
                1000
            )
        };
    }

    function read() {
        try {
            const json = window.sessionStorage.getItem(
                STORAGE_KEY
            );

            if (!json) {
                return createEmptySession();
            }

            const parsed = JSON.parse(json);
            const session = normalizeSession(parsed);

            // Neu du lieu cu khong hop le hoac het han,
            // khong tiep tuc su dung no.
            if (
                !isObject(parsed) ||
                parsed.version !== VERSION ||
                parsed.updatedAt !== session.updatedAt
            ) {
                window.sessionStorage.removeItem(
                    STORAGE_KEY
                );
            }

            return session;
        } catch (error) {
            console.warn(
                "[TripiSession] Khong doc duoc phien chat:",
                error
            );

            return createEmptySession();
        }
    }

    function write(data) {
        const input = isObject(data)
            ? data
            : {};

        const normalized = normalizeSession({
            ...createEmptySession(),
            ...input,

            version: VERSION,
            updatedAt: Date.now()
        });

        try {
            window.sessionStorage.setItem(
                STORAGE_KEY,
                JSON.stringify(normalized)
            );

            return true;
        } catch (error) {
            console.warn(
                "[TripiSession] Khong luu duoc phien chat:",
                error
            );

            return false;
        }
    }

    function patch(changes) {
        if (!isObject(changes)) {
            return false;
        }

        const current = read();

        return write({
            ...current,
            ...changes
        });
    }

    function clear() {
        try {
            window.sessionStorage.removeItem(
                STORAGE_KEY
            );

            return true;
        } catch (error) {
            console.warn(
                "[TripiSession] Khong xoa duoc phien chat:",
                error
            );

            return false;
        }
    }

    function addMessage(role, content, sources = []) {
        const message = safeMessage({
            role,
            content,
            sources
        });

        if (!message) {
            return false;
        }

        const session = read();

        session.messages.push(message);

        session.messages = session.messages.slice(
            -MAX_MESSAGES
        );

        return write(session);
    }

    function getApiHistory() {
        const session = read();

        return session.messages
            .filter(message =>
                message.role === "user" ||
                message.role === "model"
            )
            .slice(-MAX_HISTORY)
            .map(message => ({
                role: message.role,
                content: message.content.slice(
                    0,
                    MAX_MESSAGE_LENGTH
                )
            }));
    }

    // Cung cap API dung chung cho hai giao dien.
    // Khong cho phep ghi de module.
    Object.defineProperty(window, "TripiSession", {
        value: Object.freeze({
            read,
            write,
            patch,
            clear,
            addMessage,
            getApiHistory
        }),

        writable: false,
        configurable: false,
        enumerable: false
    });

    console.info(
        "[HappyTrip] Tripi shared session module loaded."
    );
})();
