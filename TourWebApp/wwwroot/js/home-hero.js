
"use strict";

(function () {
    function initHeroVideo() {
        const video = document.querySelector(
            ".ht-video-hero .ht-hero-video"
        );

        if (!video || video.dataset.initialized === "true") {
            return;
        }

        video.dataset.initialized = "true";

        video.muted = true;
        video.defaultMuted = true;
        video.loop = true;
        video.playbackRate = 2;

        const motionPreference = window.matchMedia(
            "(prefers-reduced-motion: reduce)"
        );

        async function startVideo() {
            if (motionPreference.matches || document.hidden) {
                video.pause();
                return;
            }

            video.playbackRate = 2;

            try {
                await video.play();
                video.playbackRate = 2;
            } catch {
                video.pause();
            }
        }

        video.addEventListener("loadedmetadata", () => {
            video.playbackRate = 2;
        });

        video.addEventListener("playing", () => {
            if (video.playbackRate !== 2) {
                video.playbackRate = 2;
            }
        });

        document.addEventListener("visibilitychange", () => {
            if (document.hidden) {
                video.pause();
            } else {
                startVideo();
            }
        });

        motionPreference.addEventListener?.("change", () => {
            startVideo();
        });

        startVideo();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initHeroVideo);
    } else {
        initHeroVideo();
    }
})();
