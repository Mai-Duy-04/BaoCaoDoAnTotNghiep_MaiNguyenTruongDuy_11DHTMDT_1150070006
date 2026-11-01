"use strict";

document.addEventListener("DOMContentLoaded", () => {
    const section = document.querySelector(".deal-hot");

    if (!section || section.dataset.initialized === "true") {
        return;
    }

    const track = section.querySelector(".deal-hot-track");
    const prev = section.querySelector(".deal-hot-prev");
    const next = section.querySelector(".deal-hot-next");
    const dotsContainer = section.querySelector(".deal-hot-dots");

    if (!track || !prev || !next || !dotsContainer) {
        return;
    }

    const cards = Array.from(
        track.querySelectorAll(".deal-hot-card")
    );

    if (cards.length < 2) return;

    section.dataset.initialized = "true";

    const INTERVAL = 4000;

    const reducedMotion = window.matchMedia(
        "(prefers-reduced-motion: reduce)"
    );

    let currentIndex = 0;
    let autoplay = null;

    dotsContainer.replaceChildren();

    const dots = cards.map((card, index) => {
        const dot = document.createElement("button");

        dot.type = "button";
        dot.className = "deal-hot-dot";
        dot.setAttribute(
            "aria-label",
            `Xem ưu đãi ${index + 1}`
        );

        dot.addEventListener("click", () => {
            selectSlide(index);
        });

        dotsContainer.appendChild(dot);
        return dot;
    });

    function getPosition(index) {
        let offset = index - currentIndex;
        const half = Math.floor(cards.length / 2);

        if (offset > half) {
            offset -= cards.length;
        }

        if (offset < -half) {
            offset += cards.length;
        }

        return offset;
    }

    function render() {
        cards.forEach((card, index) => {
            const position = getPosition(index);
            const active = position === 0;

            card.dataset.position = String(position);
            card.classList.toggle("is-active", active);
            card.setAttribute("tabindex", active ? "0" : "-1");

            if (active) {
                card.setAttribute("aria-current", "true");
            } else {
                card.removeAttribute("aria-current");
            }
        });

        dots.forEach((dot, index) => {
            const active = index === currentIndex;

            dot.classList.toggle("active", active);
            dot.setAttribute("aria-pressed", String(active));
        });
    }

    function moveTo(index) {
        currentIndex = (index + cards.length) % cards.length;
        render();
    }

    function stopAutoplay() {
        if (autoplay !== null) {
            clearTimeout(autoplay);
            autoplay = null;
        }
    }

    function startAutoplay() {
        stopAutoplay();

        if (document.hidden || reducedMotion.matches) return;

        autoplay = setTimeout(function advance() {
            moveTo(currentIndex + 1);
            autoplay = setTimeout(advance, INTERVAL);
        }, INTERVAL);
    }

    function selectSlide(index) {
        moveTo(index);
        startAutoplay();
    }

    prev.addEventListener("click", () => {
        selectSlide(currentIndex - 1);
    });

    next.addEventListener("click", () => {
        selectSlide(currentIndex + 1);
    });

    cards.forEach((card, index) => {
        card.addEventListener("click", event => {
            if (index !== currentIndex) {
                event.preventDefault();
                selectSlide(index);
            }
        });
    });

    track.addEventListener("keydown", event => {
        if (event.key === "ArrowLeft") {
            event.preventDefault();
            selectSlide(currentIndex - 1);
        }

        if (event.key === "ArrowRight") {
            event.preventDefault();
            selectSlide(currentIndex + 1);
        }
    });

    let touchStartX = null;

    track.addEventListener("touchstart", event => {
        if (event.touches.length !== 1) return;
        touchStartX = event.touches[0].clientX;
    }, { passive: true });

    track.addEventListener("touchend", event => {
        if (
            touchStartX === null ||
            event.changedTouches.length !== 1
        ) {
            return;
        }

        const difference =
            event.changedTouches[0].clientX - touchStartX;

        touchStartX = null;

        if (Math.abs(difference) < 50) return;

        selectSlide(
            difference < 0
                ? currentIndex + 1
                : currentIndex - 1
        );
    }, { passive: true });

    document.addEventListener("visibilitychange", () => {
        if (document.hidden) {
            stopAutoplay();
        } else {
            startAutoplay();
        }
    });

    reducedMotion.addEventListener?.("change", startAutoplay);

    render();
    startAutoplay();
});
