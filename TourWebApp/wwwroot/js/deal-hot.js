document.addEventListener("DOMContentLoaded", () => {
    const section = document.querySelector(".deal-hot");
    if (!section) return;

    const track = section.querySelector(".deal-hot-track");
    const prev = section.querySelector(".deal-hot-prev");
    const next = section.querySelector(".deal-hot-next");
    const dotsContainer = section.querySelector(".deal-hot-dots");

    if (!track || !prev || !next || !dotsContainer) return;

    const cards = Array.from(track.querySelectorAll(".deal-hot-card"));
    if (cards.length < 2) return;

    const INTERVAL = 4000;
    const reducedMotion = window.matchMedia(
        "(prefers-reduced-motion: reduce)"
    );

    let currentIndex = 0;
    let autoplay = null;
    let resumeTimer = null;
    let isDragging = false;

    const dots = cards.map((card, index) => {
        const dot = document.createElement("button");

        dot.type = "button";
        dot.className = "deal-hot-dot";
        dot.setAttribute("aria-label", `Xem banner ${index + 1}`);

        dot.addEventListener("click", () => {
            moveTo(index);
            restartAutoplay();
        });

        dotsContainer.appendChild(dot);
        return dot;
    });

    function getMaxScroll() {
        return Math.max(0, track.scrollWidth - track.clientWidth);
    }

    function getPosition(index) {
        return Math.min(
            cards[index].offsetLeft - cards[0].offsetLeft,
            getMaxScroll()
        );
    }

    function updateDots() {
        dots.forEach((dot, index) => {
            const active = index === currentIndex;

            dot.classList.toggle("active", active);
            dot.setAttribute(
                "aria-current",
                active ? "true" : "false"
            );
        });
    }

    function moveTo(index) {
        currentIndex = (index + cards.length) % cards.length;

        track.scrollTo({
            left: getPosition(currentIndex),
            behavior: reducedMotion.matches ? "instant" : "smooth"
        });

        updateDots();
    }

    function stopAutoplay() {
        if (autoplay !== null) {
            clearInterval(autoplay);
            autoplay = null;
        }
    }

    function startAutoplay() {
        stopAutoplay();

        if (document.hidden || reducedMotion.matches) return;

        autoplay = setInterval(() => {
            if (!isDragging) {
                moveTo(currentIndex + 1);
            }
        }, INTERVAL);
    }

    function restartAutoplay() {
        stopAutoplay();
        clearTimeout(resumeTimer);

        resumeTimer = setTimeout(() => {
            startAutoplay();
        }, INTERVAL);
    }

    prev.addEventListener("click", () => {
        moveTo(currentIndex - 1);
        restartAutoplay();
    });

    next.addEventListener("click", () => {
        moveTo(currentIndex + 1);
        restartAutoplay();
    });

    track.addEventListener("pointerdown", () => {
        isDragging = true;
        stopAutoplay();
    });

    window.addEventListener("pointerup", () => {
        if (!isDragging) return;

        isDragging = false;
        restartAutoplay();
    });

    track.addEventListener("scroll", () => {
        if (!isDragging) return;

        let closest = 0;
        let closestDistance = Infinity;

        cards.forEach((card, index) => {
            const distance = Math.abs(
                track.scrollLeft - getPosition(index)
            );

            if (distance < closestDistance) {
                closestDistance = distance;
                closest = index;
            }
        });

        currentIndex = closest;
        updateDots();
    }, { passive: true });

    document.addEventListener("visibilitychange", () => {
        if (document.hidden) {
            stopAutoplay();
        } else {
            startAutoplay();
        }
    });

    window.addEventListener("resize", () => {
        track.scrollTo({
            left: getPosition(currentIndex),
            behavior: "instant"
        });
    });

    updateDots();
    startAutoplay();
});
