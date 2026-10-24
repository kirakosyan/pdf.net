#!/usr/bin/env python3
"""Actual Chromium DOM/keyboard/AX checks against a labeled local fixture host.

Analysis-only Playwright is supplied by the caller; no browser is downloaded.
This exercises application integration, not PDF rendering fidelity.
"""
import argparse
import hashlib
import json
import pathlib
import subprocess
import threading

from playwright.sync_api import expect, sync_playwright


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--assembly", type=pathlib.Path, required=True)
    parser.add_argument("--browser", required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    args = parser.parse_args()
    process = subprocess.Popen(["dotnet", str(args.assembly.resolve()), "--self-test-host"],
                               stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    lines = []
    ready = threading.Event()

    def receive():
        for line in process.stdout:
            if line.startswith("APPLICATION_VIEW_FIXTURE_HOST;"):
                lines.append(line.strip().split(";", 1)[1])
                ready.set()

    threading.Thread(target=receive, daemon=True).start()
    try:
        if not ready.wait(30):
            raise RuntimeError("The explicitly labeled fixture host did not start.")
        with sync_playwright() as playwright:
            browser = playwright.chromium.launch(executable_path=args.browser, chromium_sandbox=True)
            page = browser.new_page()
            errors = []
            page.on("pageerror", lambda error: errors.append(str(error)))
            page.goto(lines[0])
            page.get_by_role("status").filter(has_text="Page 1 of 2").wait_for()
            assert page.get_by_label("Captured page text in source order").input_value() == "Original automation Ω"
            assert page.get_by_role("button", name="Previous page", exact=True).is_disabled()
            viewer = page.get_by_role("region", name="PDF page viewer")
            viewer.focus()
            viewer.press("PageDown")
            page.get_by_role("status").filter(has_text="Page 2 of 2").wait_for()
            assert page.get_by_label("Captured page text in source order").input_value() == "Second automation page"
            assert page.get_by_role("button", name="Next page", exact=True).is_disabled()
            viewer.press("Home")
            page.get_by_role("status").filter(has_text="Page 1 of 2").wait_for()
            width = page.get_by_role("img").evaluate("element => element.width")
            viewer.press("Control+=")
            expect(page.get_by_role("img")).not_to_have_attribute("width", str(width))
            expect(viewer).to_have_attribute("aria-busy", "false")
            viewer.press("Control+0")
            expect(page.get_by_role("img")).to_have_attribute("width", str(width))
            expect(viewer).to_have_attribute("aria-busy", "false")
            # Delay one response, cancel via the real key route, and ensure stale
            # completion cannot overwrite the cancellation status or current image.
            held = []
            page.route("**/api/page/*", lambda route: held.append(route))
            with page.expect_request("**/api/page/*"):
                viewer.press("PageDown")
            expect(viewer).to_have_attribute("aria-busy", "true")
            viewer.press("Escape")
            for route in held:
                route.fulfill(status=200, content_type="application/json", body="{}")
            page.get_by_role("status").filter(has_text="Rendering canceled").wait_for()
            assert page.get_by_role("img").get_attribute("alt") == "PDF page 1 of 2"
            viewer.press("Tab")
            assert page.get_by_label("Captured page text in source order").evaluate("element => element === document.activeElement")
            cdp = page.context.new_cdp_session(page)
            tree = cdp.send("Accessibility.getFullAXTree")["nodes"]
            named = {(node.get("role", {}).get("value"), node.get("name", {}).get("value"))
                     for node in tree if not node.get("ignored")}
            assert ("region", "PDF page viewer") in named
            assert ("textbox", "Captured page text in source order") in named
            assert ("button", "Cancel rendering") in named
            assert ("image", "PDF page 1 of 2") in named
            assert not errors
            result = dict(schemaVersion=1, assemblySha256=hashlib.sha256(args.assembly.read_bytes()).hexdigest(),
                          browser=browser.version, actualBrowser=True, keyboard=True, accessibilityTree=True,
                          privateAuthority=True, fixtureRenderer=True, manualScreenReaderQualified=False,
                          nativeRenderingQualified=False, productionQualified=False)
            args.output.parent.mkdir(parents=True, exist_ok=True)
            args.output.write_text(json.dumps(result, indent=2) + "\n")
            browser.close()
            print("BROWSER_VIEW_DOM_PASS;keyboard-and-AX;fixture-renderer")
    finally:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()


if __name__ == "__main__":
    main()
