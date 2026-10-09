// Tema attribute'u ilk boyamadan önce yazılır (FOUC önlenir); esas mantık src/theme.ts.
// index.html'de satır içi değil ayrı dosya: sunucunun CSP'si satır içi script'e izin vermez.
(function () {
  try {
    var m = localStorage.getItem("korkut:theme") || "light";
    var dark =
      m === "dark" ||
      (m !== "light" && matchMedia("(prefers-color-scheme: dark)").matches);
    document.documentElement.dataset.theme = dark ? "dark" : "light";
  } catch (e) {
    document.documentElement.dataset.theme = "light";
  }
})();
