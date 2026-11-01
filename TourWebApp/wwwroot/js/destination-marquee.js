"use strict";

(function () {
    function initDestinationMarquee() {
        const viewport = document.querySelector(".ht-dm-viewport");
        if (!viewport || viewport.dataset.initialized === "true") return;

        const track = viewport.querySelector(".ht-dm-track");
        if (!track || track.children.length < 2) return;

        viewport.dataset.initialized = "true";

        const motion = window.matchMedia(
            "(prefers-reduced-motion: reduce)"
        );

        let offset = 0;
        let lastTime = 0;
        let paused = false;
        let visible = true;
        const speed = 35;

        function gap() {
            return parseFloat(getComputedStyle(track).gap) || 0;
        }

        function tick(time) {
            requestAnimationFrame(tick);

            if (!lastTime) {
                lastTime = time;
                return;
            }

            const delta = Math.min(time - lastTime, 50);
            lastTime = time;

            if (paused || !visible || document.hidden || motion.matches) {
                return;
            }

            offset += speed * delta / 1000;

            let first = track.firstElementChild;

            while (first) {
                const width = first.getBoundingClientRect().width + gap();

                if (width <= 0 || offset < width) break;

                offset -= width;
                track.appendChild(first);
                first = track.firstElementChild;
            }

            track.style.transform = `translate3d(${-offset}px, 0, 0)`;
        }

        viewport.addEventListener("mouseenter", () => {
            paused = true;
        });

        viewport.addEventListener("mouseleave", () => {
            paused = false;
            lastTime = 0;
        });

        viewport.addEventListener("focusin", () => {
            paused = true;
        });

        viewport.addEventListener("focusout", event => {
            if (!viewport.contains(event.relatedTarget)) {
                paused = false;
                lastTime = 0;
            }
        });

        document.addEventListener("visibilitychange", () => {
            lastTime = 0;
        });

        if ("IntersectionObserver" in window) {
            const observer = new IntersectionObserver(entries => {
                visible = entries[0].isIntersecting;
                lastTime = 0;
            }, { threshold: .1 });

            observer.observe(viewport);
        }

        requestAnimationFrame(tick);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initDestinationMarquee);
    } else {
        initDestinationMarquee();
    }
})();
