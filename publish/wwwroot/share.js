(function () {
    "use strict";

    window.RecipeShare = {
        copy: function (text) {
            var button = document.getElementById("share-copy");
            var done = function () {
                if (button) {
                    var old = button.textContent;
                    button.textContent = "Kopiert!";
                    setTimeout(function () {
                        button.textContent = old;
                    }, 2000);
                }
            };
            var fallback = function () {
                var ta = document.createElement("textarea");
                ta.value = text;
                ta.style.position = "fixed";
                ta.style.opacity = "0";
                document.body.appendChild(ta);
                ta.select();
                var ok = false;
                try {
                    ok = document.execCommand("copy");
                } catch (e) { }
                document.body.removeChild(ta);
                if (ok) {
                    done();
                }
            };
            if (navigator.clipboard && window.isSecureContext) {
                navigator.clipboard.writeText(text).then(done, fallback);
            } else {
                fallback();
            }
        }
    };
})();