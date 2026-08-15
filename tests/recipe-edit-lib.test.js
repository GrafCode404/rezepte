const { test } = require("node:test");
const assert = require("node:assert/strict");
const Lib = require("../wwwroot/recipe-edit-lib.js");

// Diese Fälle müssen mit RecipeParserTests (C#) übereinstimmen.
const slugCases = [
    ["Hefezopf (Osterzopf)", "hefezopf-osterzopf"],
    ["Schnelle Grillbrötchen", "schnelle-grillbroetchen"],
    ["Roggenvollkorn mit Sesam", "roggenvollkorn-mit-sesam"],
    ["  Rand  Leerzeichen  ", "rand-leerzeichen"],
    ["ÄÖÜß Test", "aeoeuess-test"],
];

test("slugify bildet Slugs wie die C#-Implementierung", () => {
    for (const [input, expected] of slugCases) {
        assert.equal(Lib.slugify(input), expected, `slugify("${input}")`);
    }
});

test("slugify kollabiert mehrere Trennzeichen", () => {
    assert.equal(Lib.slugify("a!!b   c"), "a-b-c");
});

test("slugify leer und nur Sonderzeichen", () => {
    assert.equal(Lib.slugify(""), "");
    assert.equal(Lib.slugify("!!! ---"), "");
});

test("toBase64/fromBase64 runden UTF-8 (Umlaute) verlustfrei", () => {
    const s = "Ä Ö Ü ß 🍞 # Brot\nZeile2 „zitat“";
    assert.equal(Lib.fromBase64(Lib.toBase64(s)), s);
});

test("buildIndex ersetzt vorhandenen Eintrag", () => {
    const entries = [
        { name: "A.md", content: "alt-A" },
        { name: "B.md", content: "alt-B" },
    ];
    const next = Lib.buildIndex(entries, "B.md", "neu-B");
    assert.equal(next.length, 2);
    assert.equal(next[1].content, "neu-B");
    assert.equal(next[0].content, "alt-A");
});

test("buildIndex hängt neuen Eintrag an", () => {
    const entries = [{ name: "A.md", content: "a" }];
    const next = Lib.buildIndex(entries, "C.md", "c");
    assert.equal(next.length, 2);
    assert.deepEqual(next[1], { name: "C.md", content: "c" });
});

test("buildIndex mit leerem/undefiniertem entries", () => {
    assert.deepEqual(Lib.buildIndex(null, "X.md", "x"), [{ name: "X.md", content: "x" }]);
    assert.deepEqual(Lib.buildIndex([], "X.md", "x"), [{ name: "X.md", content: "x" }]);
});

test("buildIndex mit null-Einträgen überspringt diese", () => {
    const next = Lib.buildIndex([null, { name: "A.md", content: "a" }], "B.md", "b");
    assert.equal(next.length, 3);
    assert.deepEqual(next[2], { name: "B.md", content: "b" });
});

test("buildIndex erzeugt ein JSON, das wieder parst", () => {
    const next = Lib.buildIndex(
        [{ name: "Hefezopf.md", content: "# Hefezopf\n* **Menge:** 1" }],
        "Hefezopf.md",
        "# Hefezopf\n* **Menge:** 2"
    );
    const parsed = JSON.parse(JSON.stringify(next));
    assert.equal(parsed[0].content, "# Hefezopf\n* **Menge:** 2");
});
