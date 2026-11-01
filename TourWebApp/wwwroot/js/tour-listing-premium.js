"use strict";

(function () {
    function initPremiumTours() {
        const container = document.querySelector(".tours-container");
        if (!container || container.dataset.premiumInitialized === "true") {
            return;
        }

        const cards = Array.from(
            container.querySelectorAll(".tour-listing-grid .tour-list-card")
        );

        if (!cards.length) return;

        container.dataset.premiumInitialized = "true";

        const reducedMotion = window.matchMedia(
            "(prefers-reduced-motion: reduce)"
        );

        const finePointer = window.matchMedia(
            "(hover: hover) and (pointer: fine)"
        );

        cards.forEach((card, index) => {
            let frame = 0;
            let targetX = 0;
            let targetY = 0;
            let pointerInside = false;

            card.classList.add("ht-premium-card");

            card.style.setProperty("--ht-stagger", `${Math.min(index % 9, 8) * 65}ms`);

            function updateTilt() {
                frame = 0;

                if (!pointerInside) return;

                card.style.setProperty("--ht-rotate-x", `${targetX.toFixed(2)}deg`);
                card.style.setProperty("--ht-rotate-y", `${targetY.toFixed(2)}deg`);
            }

            card.addEventListener("pointerenter", event => {
                if (
                    !finePointer.matches ||
                    reducedMotion.matches ||
                    event.pointerType === "touch"
                ) {
                    return;
                }

                pointerInside = true;
                card.classList.add("ht-premium-hover");
            });

            card.addEventListener("pointermove", event => {
                if (
                    !pointerInside ||
                    !finePointer.matches ||
                    reducedMotion.matches
                ) {
                    return;
                }

                const rect = card.getBoundingClientRect();

                if (!rect.width || !rect.height) return;

                const x = (event.clientX - rect.left) / rect.width;
                const y = (event.clientY - rect.top) / rect.height;

                targetX = (0.5 - y) * 9;
                targetY = (x - 0.5) * 9;

                card.style.setProperty("--ht-mouse-x", `${x * 100}%`);
                card.style.setProperty("--ht-mouse-y", `${y * 100}%`);

                if (!frame) {
                    frame = requestAnimationFrame(updateTilt);
                }
            });

            function resetTilt() {
                pointerInside = false;
                card.classList.remove("ht-premium-hover");

                if (frame) {
                    cancelAnimationFrame(frame);
                    frame = 0;
                }

                card.style.setProperty("--ht-rotate-x", "0deg");
                card.style.setProperty("--ht-rotate-y", "0deg");
            }

            card.addEventListener("pointerleave", resetTilt);

            card.addEventListener("focusin", () => {
                resetTilt();
                card.classList.add("ht-premium-focused");
            });

            card.addEventListener("focusout", event => {
                if (!card.contains(event.relatedTarget)) {
                    card.classList.remove("ht-premium-focused");
                }
            });
        });

        if ("IntersectionObserver" in window && !reducedMotion.matches) {
            const observer = new IntersectionObserver(entries => {
                entries.forEach(entry => {
                    if (!entry.isIntersecting) return;

                    entry.target.classList.add("ht-premium-visible");
                    observer.unobserve(entry.target);
                });
            }, {
                threshold: 0.08,
                rootMargin: "0px 0px 35px 0px"
            });

            cards.forEach(card => observer.observe(card));
        } else {
            cards.forEach(card => {
                card.classList.add("ht-premium-visible");
            });
        }

        reducedMotion.addEventListener?.("change", event => {
            if (event.matches) {
                cards.forEach(card => {
                    card.style.setProperty("--ht-rotate-x", "0deg");
                    card.style.setProperty("--ht-rotate-y", "0deg");
                    card.classList.remove("ht-premium-hover");
                    card.classList.add("ht-premium-visible");
                });
            }
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initPremiumTours);
    } else {
        initPremiumTours();
    }
})();
