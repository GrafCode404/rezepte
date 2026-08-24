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
        },
        download: function (text, fileName) {
            var blob = new Blob([text], { type: "text/markdown;charset=utf-8" });
            var url = URL.createObjectURL(blob);
            var a = document.createElement("a");
            a.href = url;
            a.download = fileName;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
        },
        print: function () {
            window.print();
        }
    };
})();