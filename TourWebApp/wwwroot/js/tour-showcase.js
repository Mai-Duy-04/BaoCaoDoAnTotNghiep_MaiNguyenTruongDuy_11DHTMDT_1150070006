"use strict";

(function () {
    function initTourShowcase() {
        const section = document.querySelector(".ht-showcase-section");
        if (!section || section.dataset.initialized === "true") return;

        const stage = section.querySelector(".ht-showcase-stage");
        const slides = Array.from(section.querySelectorAll(".ht-showcase-slide"));
        const previous = section.querySelector(".ht-showcase-prev");
        const next = section.querySelector(".ht-showcase-next");
        const current = section.querySelector(".ht-showcase-current");

        if (!stage || slides.length === 0) return;

        section.dataset.initialized = "true";

        const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

        let activeIndex = 0;
        let timer = null;
        let hovering = false;
        let focusing = false;
        let visible = true;
        let touchStartX = null;

        function getPosition(index) {
            const total = slides.length;
            let distance = (index - activeIndex + total) % total;

            if (distance === 0) return "0";
            if (distance === 1) return "1";
            if (distance === total - 1) return "-1";

            return "hidden";
        }

        function render() {
            slides.forEach((slide, index) => {
                const position = getPosition(index);
                const active = position === "0";

                slide.dataset.position = position;
                slide.setAttribute("aria-hidden", String(!active));

                if ("inert" in slide) {
                    slide.inert = !active;
                }

                slide.querySelectorAll("a, button").forEach(element => {
                    element.tabIndex = active ? 0 : -1;
                });
            });

            if (current) {
                current.textContent = String(activeIndex + 1).padStart(2, "0");
            }
        }

        function stopAutoplay() {
            if (timer !== null) {
                clearTimeout(timer);
                timer = null;
            }
        }

        function startAutoplay() {
            stopAutoplay();

            if (
                slides.length < 2 ||
                hovering ||
                focusing ||
                !visible ||
                document.hidden ||
                reducedMotion.matches
            ) {
                return;
            }

            timer = setTimeout(() => {
                timer = null;
                activeIndex = (activeIndex + 1) % slides.length;
                render();
                startAutoplay();
            }, 5500);
        }

        function changeTour(direction) {
            activeIndex =
                (activeIndex + direction + slides.length) % slides.length;

            render();
            startAutoplay();
        }

        previous?.addEventListener("click", () => changeTour(-1));
        next?.addEventListener("click", () => changeTour(1));

        stage.addEventListener("mouseenter", () => {
            hovering = true;
            stopAutoplay();
        });

        stage.addEventListener("mouseleave", () => {
            hovering = false;
            startAutoplay();
        });

        section.addEventListener("focusin", () => {
            focusing = true;
            stopAutoplay();
        });

        section.addEventListener("focusout", event => {
            if (!section.contains(event.relatedTarget)) {
                focusing = false;
                startAutoplay();
            }
        });

        stage.addEventListener("touchstart", event => {
            if (event.touches.length === 1) {
                touchStartX = event.touches[0].clientX;
            }
        }, { passive: true });

        stage.addEventListener("touchend", event => {
            if (touchStartX === null || event.changedTouches.length !== 1) {
                return;
            }

            const distance =
                event.changedTouches[0].clientX - touchStartX;

            touchStartX = null;

            if (Math.abs(distance) > 45) {
                changeTour(distance < 0 ? 1 : -1);
            }
        }, { passive: true });

        section.addEventListener("keydown", event => {
            if (event.key === "ArrowLeft") {
                event.preventDefault();
                changeTour(-1);
            } else if (event.key === "ArrowRight") {
                event.preventDefault();
                changeTour(1);
            }
        });

        document.addEventListener("visibilitychange", startAutoplay);

        if (reducedMotion.addEventListener) {
            reducedMotion.addEventListener("change", startAutoplay);
        }

        if ("IntersectionObserver" in window) {
            const observer = new IntersectionObserver(entries => {
                visible = entries[0].isIntersecting;
                startAutoplay();
            }, { threshold: 0.15 });

            observer.observe(section);
        }

        render();
        startAutoplay();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initTourShowcase);
    } else {
        initTourShowcase();
    }
})();
