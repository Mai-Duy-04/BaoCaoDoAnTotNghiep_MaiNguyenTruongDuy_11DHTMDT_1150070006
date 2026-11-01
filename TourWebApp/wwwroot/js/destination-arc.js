"use strict";

(function () {
    function initDestinationArc() {
        const section = document.querySelector(".ht-arc-section");
        if (!section || section.dataset.initialized === "true") return;

        const stage = section.querySelector(".ht-arc-stage");
        const slides = Array.from(section.querySelectorAll(".ht-arc-slide"));
        const prevButton = section.querySelector(".ht-arc-prev");
        const nextButton = section.querySelector(".ht-arc-next");
        const currentLabel = section.querySelector(".ht-arc-current");

        if (!stage || !slides.length) return;

        section.dataset.initialized = "true";

        const reducedMotion = window.matchMedia(
            "(prefers-reduced-motion: reduce)"
        );

        const circle = Math.PI * 2;
        const step = circle / slides.length;
        const rotationSpeed = circle / 24000;

        let rotation = 0;
        let manualRotation = 0;
        let lastTime = 0;
        let pausedByHover = false;
        let pausedByFocus = false;
        let visible = true;
        let currentIndex = -1;
        let touchStartX = null;

        function normalizeAngle(angle) {
            return Math.atan2(Math.sin(angle), Math.cos(angle));
        }

        function render() {
            const isMobile = window.innerWidth <= 767;
            const isTablet = window.innerWidth <= 1100;

            const radiusX = isMobile
                ? Math.min(window.innerWidth * 0.64, 275)
                : isTablet
                    ? Math.min(stage.clientWidth * 0.46, 440)
                    : Math.min(stage.clientWidth * 0.46, 540);

            const radiusZ = isMobile ? 195 : 315;
            const radiusY = isMobile ? 24 : 42;

            let nearestIndex = 0;
            let nearestDistance = Infinity;

            slides.forEach((slide, index) => {
                const angle = normalizeAngle(index * step - rotation);

                const sine = Math.sin(angle);
                const cosine = Math.cos(angle);
                const depth = (cosine + 1) / 2;
                const distance = Math.abs(angle);

                const x = sine * radiusX;
                const y = (1 - cosine) * radiusY;
                const z = cosine * radiusZ;

                const scale = 0.60 + depth * 0.40;
                const rotateY = -sine * 60;
                const rotateZ = sine * 5;

                const brightness = 0.67 + depth * 0.33;
                const saturation = 0.82 + depth * 0.18;

                const behind = cosine < -0.35;

                slide.style.transform =
                    `translate(-50%, -50%) ` +
                    `translate3d(${x.toFixed(2)}px, ${y.toFixed(2)}px, ${z.toFixed(2)}px) ` +
                    `rotateY(${rotateY.toFixed(2)}deg) ` +
                    `rotateZ(${rotateZ.toFixed(2)}deg) ` +
                    `scale(${scale.toFixed(3)})`;

                slide.style.opacity = behind ? "0" : "1";
                slide.style.visibility = behind ? "hidden" : "visible";
                slide.style.zIndex = String(Math.round(depth * 100) + 1);
                slide.style.pointerEvents = "none";

                slide.style.filter =
                    `brightness(${brightness.toFixed(3)}) ` +
                    `saturate(${saturation.toFixed(3)})`;

                if (distance < nearestDistance) {
                    nearestDistance = distance;
                    nearestIndex = index;
                }
            });

            if (nearestIndex !== currentIndex) {
                currentIndex = nearestIndex;

                slides.forEach((slide, index) => {
                    const isActive = index === currentIndex;

                    slide.classList.toggle("is-active", isActive);
                    slide.setAttribute("aria-hidden", String(!isActive));

                    slide.querySelectorAll("a, button").forEach(element => {
                        element.tabIndex = isActive ? 0 : -1;
                    });
                });

                if (currentLabel) {
                    currentLabel.textContent =
                        String(currentIndex + 1).padStart(2, "0");
                }
            }

            slides[currentIndex].style.pointerEvents = "auto";
        }

        function rotateBy(direction) {
            manualRotation += direction * step;
        }

        prevButton?.addEventListener("click", () => {
            rotateBy(-1);
        });

        nextButton?.addEventListener("click", () => {
            rotateBy(1);
        });

        stage.addEventListener("mouseenter", () => {
            pausedByHover = true;
        });

        stage.addEventListener("mouseleave", () => {
            pausedByHover = false;
        });

        section.addEventListener("focusin", () => {
            pausedByFocus = true;
        });

        section.addEventListener("focusout", event => {
            if (!section.contains(event.relatedTarget)) {
                pausedByFocus = false;
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
                rotateBy(distance < 0 ? 1 : -1);
            }
        }, { passive: true });

        section.addEventListener("keydown", event => {
            if (event.key === "ArrowRight") {
                event.preventDefault();
                rotateBy(1);
            } else if (event.key === "ArrowLeft") {
                event.preventDefault();
                rotateBy(-1);
            }
        });

        if ("IntersectionObserver" in window) {
            const observer = new IntersectionObserver(entries => {
                visible = entries[0].isIntersecting;
            }, { threshold: 0.1 });

            observer.observe(section);
        }

        function animate(time) {
            requestAnimationFrame(animate);

            if (lastTime === 0) {
                lastTime = time;
                render();
                return;
            }

            const delta = Math.min(time - lastTime, 50);
            lastTime = time;

            const paused =
                pausedByHover ||
                pausedByFocus ||
                document.hidden ||
                !visible ||
                reducedMotion.matches;

            if (!paused && Math.abs(manualRotation) < 0.001) {
                rotation += delta * rotationSpeed;
            }

            if (Math.abs(manualRotation) > 0.0001) {
                const amount =
                    Math.sign(manualRotation) *
                    Math.min(Math.abs(manualRotation), delta * 0.0035);

                rotation += amount;
                manualRotation -= amount;
            }

            if (Math.abs(rotation) > circle * 100) {
                rotation %= circle;
            }

            render();
        }

        render();
        requestAnimationFrame(animate);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initDestinationArc);
    } else {
        initDestinationArc();
    }
})();
