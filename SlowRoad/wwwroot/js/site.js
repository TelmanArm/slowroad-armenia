// Light / dark theme switch.
//
// The site is LIGHT by default — it does not follow the device setting. The
// reader turns it dark with the button in the header, and that choice is kept
// in their own browser (localStorage, never sent to the server). The matching
// inline script in _Layout.cshtml applies a stored choice before first paint.
(function () {
    "use strict";

    var KEY = "slowroad-theme";
    var root = document.documentElement;
    var button = document.querySelector("[data-theme-toggle]");
    if (!button) { return; }

    function isDark() {
        return root.getAttribute("data-theme") === "dark";
    }

    function describe() {
        var dark = isDark();
        button.setAttribute("aria-pressed", dark ? "true" : "false");
        button.setAttribute("aria-label", dark ? "Switch to light theme" : "Switch to dark theme");
    }

    button.addEventListener("click", function () {
        var goingDark = !isDark();

        if (goingDark) {
            root.setAttribute("data-theme", "dark");
        } else {
            root.removeAttribute("data-theme");
        }

        // Private browsing and blocked site data make this throw; the switch
        // still works for this page view, it just is not remembered.
        try {
            localStorage.setItem(KEY, goingDark ? "dark" : "light");
        } catch (e) { /* no storage available */ }

        describe();
    });

    describe();
})();
