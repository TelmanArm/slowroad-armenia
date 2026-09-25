// Photo slider for the destination detail page (Views/Destinations/Details.cshtml).
// The slider already works without this file (swipe / scroll with CSS
// scroll-snap). This adds: prev/next arrows, clickable thumbnails,
// the "2 / 5" counter, and left/right arrow keys.
(function () {
    document.querySelectorAll('[data-slider]').forEach(function (slider) {
        var track = slider.querySelector('.slider__track');
        var slides = track.querySelectorAll('.slider__slide');
        var prev = slider.querySelector('.slider__btn--prev');
        var next = slider.querySelector('.slider__btn--next');
        var counter = slider.querySelector('[data-slider-current]');
        var thumbsBox = slider.querySelector('.slider__thumbs');
        var thumbs = slider.querySelectorAll('.slider__thumb');
        var count = slides.length;
        if (count < 2) return;

        // Which slide is showing (each slide is exactly one track wide).
        function current() {
            return Math.round(track.scrollLeft / track.clientWidth);
        }

        // Go to slide i (wraps around: after the last comes the first).
        function go(i) {
            i = (i + count) % count;
            track.scrollTo({ left: i * track.clientWidth, behavior: 'smooth' });
        }

        // Keep counter + active thumbnail in sync with what's on screen.
        var ticking = false;
        function update() {
            ticking = false;
            var i = current();
            counter.textContent = i + 1;
            thumbs.forEach(function (t, n) {
                t.classList.toggle('is-active', n === i);
                if (n === i) t.setAttribute('aria-current', 'true');
                else t.removeAttribute('aria-current');
            });
        }
        track.addEventListener('scroll', function () {
            if (!ticking) { ticking = true; requestAnimationFrame(update); }
        });

        prev.addEventListener('click', function () { go(current() - 1); });
        next.addEventListener('click', function () { go(current() + 1); });
        thumbs.forEach(function (t) {
            t.addEventListener('click', function () { go(Number(t.dataset.slide)); });
        });
        track.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowLeft') { e.preventDefault(); go(current() - 1); }
            if (e.key === 'ArrowRight') { e.preventDefault(); go(current() + 1); }
        });

        // Show the JS-only controls.
        prev.hidden = false;
        next.hidden = false;
        thumbsBox.hidden = false;
        update();
    });
})();
