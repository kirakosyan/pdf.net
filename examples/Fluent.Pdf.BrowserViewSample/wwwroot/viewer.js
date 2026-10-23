"use strict";
const token = location.hash.slice(1);
const status = document.getElementById("status"), image = document.getElementById("page"), transcript = document.getElementById("text"), viewer = document.getElementById("viewer");
let page = 0, pages = 1, zoom = 1, generation = 0, active = null, imageUrl = null;
async function render() {
  const own = ++generation;
  if (active) active.abort();
  const request = active = new AbortController();
  status.textContent = "Rendering page " + (page + 1);
  viewer.setAttribute("aria-busy", "true");
  try {
    const response = await fetch("/api/page/" + page + "?zoom=" + zoom, { headers: { "X-Fluent-View-Token": token }, cache: "no-store", signal: request.signal });
    if (!response.ok) throw new Error("View request failed: " + response.status);
    const frame = await response.json();
    if (own !== generation || request.signal.aborted) return;
    const bytes = Uint8Array.from(atob(frame.png), value => value.charCodeAt(0));
    const next = URL.createObjectURL(new Blob([bytes], { type: "image/png" }));
    if (imageUrl) URL.revokeObjectURL(imageUrl);
    image.src = imageUrl = next; image.alt = "PDF page " + (frame.page + 1) + " of " + frame.pageCount;
    image.width = frame.width; image.height = frame.height; transcript.value = frame.capturedText;
    pages = frame.pageCount; page = frame.page; status.textContent = "Page " + (page + 1) + " of " + pages;
    document.getElementById("previous").disabled = document.getElementById("first").disabled = page === 0;
    document.getElementById("next").disabled = document.getElementById("last").disabled = page === pages - 1;
  } catch (error) {
    if (own === generation) status.textContent = error.name === "AbortError" ? "Rendering canceled" : "Page unavailable";
  } finally {
    if (own === generation) { viewer.setAttribute("aria-busy", "false"); active = null; }
  }
}
function command(name) {
  if (name === "cancel") { generation++; if (active) active.abort(); active = null; viewer.setAttribute("aria-busy", "false"); status.textContent = "Rendering canceled"; return; }
  if (name === "next") page = Math.min(pages - 1, page + 1);
  if (name === "previous") page = Math.max(0, page - 1);
  if (name === "first") page = 0;
  if (name === "last") page = pages - 1;
  if (name === "zoom-in") zoom = Math.min(4, zoom * 1.25);
  if (name === "zoom-out") zoom = Math.max(.25, zoom / 1.25);
  if (name === "actual") zoom = 1;
  render();
}
for (const name of ["first", "previous", "next", "last", "zoom-in", "zoom-out", "actual", "cancel"]) document.getElementById(name).addEventListener("click", () => command(name));
viewer.addEventListener("keydown", event => {
  const name = event.key === "PageDown" ? "next" : event.key === "PageUp" ? "previous" : event.key === "Home" ? "first" : event.key === "End" ? "last" : event.ctrlKey && (event.key === "+" || event.key === "=") ? "zoom-in" : event.ctrlKey && event.key === "-" ? "zoom-out" : event.ctrlKey && event.key === "0" ? "actual" : event.key === "Escape" ? "cancel" : null;
  if (name) { event.preventDefault(); command(name); }
});
window.addEventListener("pagehide", () => { if (active) active.abort(); if (imageUrl) URL.revokeObjectURL(imageUrl); });
if (/^[0-9a-f]{64}$/.test(token)) render(); else status.textContent = "Open the authorized local view address";
