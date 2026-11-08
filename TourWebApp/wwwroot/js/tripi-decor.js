
/* ==========================================================
   HAPPYTRIP - TRIPI 3D DECORATIONS
   File: wwwroot/js/tripi-decor.js

   NHIEM VU:
   - Tao cac sticker PNG 3D
   - Tao phong canh nui va doi xanh
   - Trang tri trang Tripi toan man hinh
   - Trang tri popup Tripi mini

   KHONG THAY DOI:
   - Noi dung tieu de
   - Noi dung mo ta
   - Nut goi y
   - Form chat
   - Session
   - Gemini / RAG / Matching Score
   - Trang thai home / chat / results
   ========================================================== */

(() => {
    "use strict";

    /* ======================================================
       01. THU MUC HINH ANH
       ====================================================== */

    const IMAGE_FOLDER = "/img/tripi-stickers/";

    const IMAGES = Object.freeze({
        camera: "camera.png",
        cloud: "cloud.png",
        passport: "passport.png",
        signpost: "signpost.png",
        airplane: "airplane.png",
        map: "map.png",
        suitcase: "suitcase.png",
        sparkles: "sparkles.png",
        paperPlane: "paper-plane.png"
    });

    /* ======================================================
       02. STICKER TRANG LON
       ====================================================== */

    const FULL_STICKERS = [
        ["cloud", "cloud-left"],
        ["camera", "camera-left"],
        ["passport", "passport-left"],
        ["signpost", "signpost-left"],

        ["airplane", "airplane-right"],
        ["cloud", "cloud-right"],
        ["map", "map-right"],
        ["suitcase", "suitcase-right"],

        ["sparkles", "sparkle-left"],
        ["sparkles", "sparkle-right"]
    ];

    /* ======================================================
       03. STICKER POPUP MINI
       ====================================================== */

    const MINI_STICKERS = [
        ["camera", "mini-camera"],
        ["paperPlane", "mini-plane"],
        ["cloud", "mini-cloud"],
        ["sparkles", "mini-sparkle"]
    ];

    /* ======================================================
       04. TAO STICKER PNG
       ====================================================== */

    function createSticker(imageKey, position) {
        const fileName = IMAGES[imageKey];

        if (!fileName) {
            return null;
        }

        const image = document.createElement("img");

        image.className =
            "tripi-3d-sticker tripi-3d-" + position;

        image.src = IMAGE_FOLDER + fileName;

        image.alt = "";
        image.loading = "eager";
        image.decoding = "async";
        image.draggable = false;

        image.setAttribute("aria-hidden", "true");

        return image;
    }

    /* ======================================================
       05. PHONG CANH NUI
       SVG DUOC DUNG RIENG CHO PHONG CANH.
       STICKER VAN LA CAC FILE PNG 3D.
       ====================================================== */

    function createLandscapeSvg(prefix) {
        const sky = prefix + "-sky";
        const mountain = prefix + "-mountain";
        const hillBack = prefix + "-hill-back";
        const hillFront = prefix + "-hill-front";

        return `
            <svg
                xmlns="http://www.w3.org/2000/svg"
                viewBox="0 0 1440 280"
                preserveAspectRatio="none"
                aria-hidden="true"
                focusable="false">

                <defs>

                    <linearGradient
                        id="${sky}"
                        x1="0" y1="0"
                        x2="0" y2="1">

                        <stop
                            offset="0%"
                            stop-color="#fffaf2" />

                        <stop
                            offset="100%"
                            stop-color="#e4f0e5" />

                    </linearGradient>

                    <linearGradient
                        id="${mountain}"
                        x1="0" y1="0"
                        x2="0.6" y2="1">

                        <stop
                            offset="0%"
                            stop-color="#dce9ed" />

                        <stop
                            offset="55%"
                            stop-color="#9fc2cd" />

                        <stop
                            offset="100%"
                            stop-color="#719eaa" />

                    </linearGradient>

                    <linearGradient
                        id="${hillBack}"
                        x1="0" y1="0"
                        x2="0" y2="1">

                        <stop
                            offset="0%"
                            stop-color="#e2f0da" />

                        <stop
                            offset="100%"
                            stop-color="#abd0a7" />

                    </linearGradient>

                    <linearGradient
                        id="${hillFront}"
                        x1="0" y1="0"
                        x2="0" y2="1">

                        <stop
                            offset="0%"
                            stop-color="#acd398" />

                        <stop
                            offset="100%"
                            stop-color="#70ad7a" />

                    </linearGradient>

                </defs>

                <!-- Nen nhe -->

                <path
                    fill="url(#${sky})"
                    opacity=".15"
                    d="M0 0H1440V280H0Z" />

                <!-- Nui xa ben trai -->

                <path
                    fill="#e6eddf"
                    opacity=".8"
                    d="
                        M70 230
                        L300 84
                        L520 230
                        Z
                    " />

                <!-- Nui xa ben phai -->

                <path
                    fill="#dee9df"
                    opacity=".8"
                    d="
                        M900 235
                        L1135 94
                        L1395 235
                        Z
                    " />

                <!-- Nui chinh -->

                <path
                    fill="url(#${mountain})"
                    d="
                        M365 254
                        L719 19
                        L1082 254
                        Z
                    " />

                <!-- Tuyet tren dinh nui -->

                <path
                    fill="#ffffff"
                    d="
                        M618 86
                        L719 19
                        L829 91
                        L773 73
                        L744 99
                        L711 76
                        L681 101
                        L655 78
                        Z
                    " />

                <!-- Mat nui tao do sau -->

                <path
                    fill="#87afbb"
                    opacity=".52"
                    d="
                        M719 105
                        L1082 254
                        L719 254
                        Z
                    " />

                <!-- Doi xa -->

                <path
                    fill="url(#${hillBack})"
                    d="
                        M0 202
                        C145 123 279 180 421 220
                        C569 139 689 146 806 220
                        C1018 117 1231 154 1440 199
                        V280
                        H0
                        Z
                    " />

                <!-- Doi giua -->

                <path
                    fill="#b5d9a6"
                    d="
                        M0 241
                        C175 180 330 206 500 243
                        C652 168 824 180 988 247
                        C1163 183 1311 189 1440 231
                        V280
                        H0
                        Z
                    " />

                <!-- Doi truoc -->

                <path
                    fill="url(#${hillFront})"
                    d="
                        M0 257
                        C166 218 322 221 460 254
                        C633 215 773 219 925 259
                        C1093 218 1263 226 1440 251
                        V280
                        H0
                        Z
                    " />

                <!-- Co sat day -->

                <path
                    fill="#78ae78"
                    opacity=".45"
                    d="
                        M0 273
                        Q180 248 350 270
                        T710 271
                        T1080 269
                        T1440 270
                        V280
                        H0
                        Z
                    " />

            </svg>
        `;
    }

    /* ======================================================
       06. TAO PHONG CANH
       ====================================================== */

    function createLandscape(sceneType) {
        const landscape = document.createElement("div");

        landscape.className = "tripi-3d-landscape";

        landscape.setAttribute(
            "aria-hidden",
            "true"
        );

        landscape.innerHTML =
            createLandscapeSvg("tripi-" + sceneType);

        return landscape;
    }

    /* ======================================================
       07. TAO LOP TRANG TRI
       ====================================================== */

    function createScene(type, stickers) {
        const scene = document.createElement("div");

        scene.className =
            "tripi-3d-scene tripi-3d-" + type;

        scene.setAttribute(
            "aria-hidden",
            "true"
        );

        scene.appendChild(
            createLandscape(type)
        );

        stickers.forEach(([imageKey, position]) => {
            const sticker = createSticker(
                imageKey,
                position
            );

            if (sticker) {
                scene.appendChild(sticker);
            }
        });

        return scene;
    }

    /* ======================================================
       08. DON CAC LOP TRANG TRI CU
       ====================================================== */

    function removeLegacyDecorations(container) {
        if (!container) {
            return;
        }

        container
            .querySelectorAll(
                ".tripi-decor-scene, " +
                ".tripi-mini-decor"
            )
            .forEach(element => {
                element.remove();
            });
    }

    /* ======================================================
       09. TRANG TRIPI TOAN MAN HINH
       ====================================================== */

    function decorateFullPage() {
        const page = document.getElementById(
            "tripiChatPage"
        );

        if (!page) {
            return;
        }

        const chatPanel = page.querySelector(
            ".tripi-world-chat"
        );

        if (!chatPanel) {
            return;
        }

        // Khong tao sticker trung lap.
        const existingScene = chatPanel.querySelector(
            ".tripi-3d-scene.tripi-3d-full"
        );

        if (existingScene) {
            return;
        }

        removeLegacyDecorations(chatPanel);

        const scene = createScene(
            "full",
            FULL_STICKERS
        );

        chatPanel.insertBefore(
            scene,
            chatPanel.firstChild
        );

        /* QUAN TRONG:

           KHONG sua .tripi-welcome-heading
           KHONG sua h3
           KHONG sua p
           KHONG sua badge

           Noi dung chu duoc lay truc tiep
           tu Index.cshtml.
        */
    }

    /* ======================================================
       10. POPUP TRIPI MINI
       ====================================================== */

    function decorateMiniAssistant() {
        const assistant = document.getElementById(
            "tripiAssistant"
        );

        if (!assistant) {
            return;
        }

        const welcome = assistant.querySelector(
            "#tripiAssistantWelcome"
        );

        if (!welcome) {
            return;
        }

        // Khong tao them scene neu da co.
        const existingScene = welcome.querySelector(
            ".tripi-3d-scene.tripi-3d-mini"
        );

        if (existingScene) {
            return;
        }

        removeLegacyDecorations(welcome);

        const scene = createScene(
            "mini",
            MINI_STICKERS
        );

        welcome.insertBefore(
            scene,
            welcome.firstChild
        );

        // Giu nguyen noi dung va chuc nang popup.
    }

    /* ======================================================
       11. KHOI TAO
       ====================================================== */

    function initializeDecorations() {
        try {
            decorateFullPage();
        } catch (error) {
            console.warn(
                "[Tripi Decor] Loi trang lon:",
                error
            );
        }

        try {
            decorateMiniAssistant();
        } catch (error) {
            console.warn(
                "[Tripi Decor] Loi popup:",
                error
            );
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener(
            "DOMContentLoaded",
            initializeDecorations,
            { once: true }
        );
    } else {
        initializeDecorations();
    }

})();
