
"use strict";

document.addEventListener("DOMContentLoaded", function () {
    const sidebar = document.getElementById("adminSidebar")
        || document.querySelector(".admin-sidebar");

    const btnToggle = document.getElementById("btnToggleSidebar");
    const overlay = document.getElementById("adminOverlay");

    function closeSidebar() {
        if (!sidebar) return;

        sidebar.classList.remove("show");

        if (overlay) {
            overlay.classList.remove("show");
        }

        if (btnToggle) {
            btnToggle.setAttribute("aria-expanded", "false");
        }
    }

    function toggleSidebar() {
        if (!sidebar) return;

        const isOpen = sidebar.classList.toggle("show");

        if (overlay) {
            overlay.classList.toggle("show", isOpen);
        }

        if (btnToggle) {
            btnToggle.setAttribute(
                "aria-expanded",
                String(isOpen)
            );
        }
    }

    if (btnToggle && sidebar) {
        btnToggle.setAttribute("aria-expanded", "false");

        btnToggle.addEventListener("click", toggleSidebar);
    }

    if (overlay) {
        overlay.addEventListener("click", closeSidebar);
    }

    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape") {
            closeSidebar();
        }
    });

    function normalizePath(value) {
        let path = (value || "/")
            .split("?")[0]
            .split("#")[0]
            .toLowerCase()
            .replace(/\/+/g, "/");

        if (path.length > 1) {
            path = path.replace(/\/+$/, "");
        }

        return path || "/";
    }

    const currentPath = normalizePath(
        window.location.pathname
    );

    const menuLinks = document.querySelectorAll(
        ".admin-menu a[data-menu]"
    );

    let selectedLink = null;
    let selectedLength = -1;

    menuLinks.forEach(function (link) {
        link.classList.remove("active");
        link.removeAttribute("aria-current");

        const section = normalizePath(
            link.getAttribute("data-menu")
        );

        const isMatch =
            currentPath === section ||
            currentPath.startsWith(section + "/");

        if (isMatch && section.length > selectedLength) {
            selectedLink = link;
            selectedLength = section.length;
        }
    });

    if (selectedLink) {
        selectedLink.classList.add("active");
        selectedLink.setAttribute("aria-current", "page");
    }

    const groupButtons = document.querySelectorAll(
        ".admin-group-toggle[data-group-target]"
    );

    function getGroupPanel(button) {
        const targetId = button.getAttribute(
            "data-group-target"
        );

        if (!targetId) return null;

        return document.getElementById(targetId);
    }

    function setGroupOpen(button, isOpen) {
        const panel = getGroupPanel(button);

        if (!panel) return;

        button.classList.toggle("is-open", isOpen);
        button.setAttribute(
            "aria-expanded",
            String(isOpen)
        );

        panel.hidden = !isOpen;
        panel.classList.toggle("is-open", isOpen);
    }

    groupButtons.forEach(function (button) {
        const panel = getGroupPanel(button);

        if (!panel) return;

        setGroupOpen(button, false);

        button.addEventListener("click", function () {
            const shouldOpen =
                button.getAttribute("aria-expanded") !== "true";

            groupButtons.forEach(function (otherButton) {
                if (otherButton !== button) {
                    setGroupOpen(otherButton, false);
                }
            });

            setGroupOpen(button, shouldOpen);
        });
    });

    if (selectedLink) {
        const parentPanel = selectedLink.closest(
            ".admin-group-panel"
        );

        if (parentPanel) {
            const parentButton = Array.from(
                groupButtons
            ).find(function (button) {
                return getGroupPanel(button) === parentPanel;
            });

            if (parentButton) {
                setGroupOpen(parentButton, true);
            }
        }
    }

    if (sidebar) {
        sidebar.querySelectorAll(
            ".admin-menu a[href]"
        ).forEach(function (link) {
            link.addEventListener("click", function () {
                if (window.innerWidth <= 991) {
                    closeSidebar();
                }
            });
        });
    }

    window.addEventListener("resize", function () {
        if (window.innerWidth > 991) {
            closeSidebar();
        }
    });
});
